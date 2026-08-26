using System.Net;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// One retry + logging layer shared by every Groq typed client.
///
/// <para>It exists because of two gaps that together made a healthy feature look broken. Nothing
/// survived a transient Groq failure: every client calls <c>EnsureSuccessStatusCode()</c>, so a
/// single 429 or 503 became a red error in a beekeeper's hand — which is exactly the reported
/// "voice note failed the first time, worked on the second". That retry now belongs to us, not to
/// the user. And <c>EnsureSuccessStatusCode()</c> throws before anyone reads the body, which is the
/// only place Groq puts the reason (<c>rate_limit_exceeded</c>, <c>model_not_found</c>, …) — a spent
/// quota and a retired model were the same line in the log. Both are read and logged here.</para>
///
/// <para>Retrying a POST is safe for these calls specifically: Groq inference has no side effect
/// beyond spending quota.</para>
/// </summary>
public sealed class GroqRetryHandler : DelegatingHandler
{
    /// One retry, not more. It covers the transient blip this exists for, while a second one would
    /// mostly add tail latency to a request a beekeeper is already staring at a spinner for.
    private const int MaxAttempts = 2;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(400);

    /// Groq answers a spent per-minute quota with a Retry-After of a second or two, and a spent
    /// daily quota with one of hours. Waiting out the first inside the request is the point;
    /// waiting out the second is not, so anything longer fails fast instead.
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(3);

    /// Groq's error bodies are short; the cap is only here so a stray HTML error page from a proxy
    /// cannot dump kilobytes into the log on every failed call.
    private const int MaxLoggedBodyChars = 500;

    private readonly ILogger<GroqRetryHandler> _logger;
    private readonly TimeSpan _attemptTimeout;

    /// <param name="attemptTimeout">
    /// Budget for a single attempt. <c>HttpClient.Timeout</c> covers the whole pipeline including
    /// retries, so without a per-attempt cap one hung attempt eats the entire budget and the retry
    /// never happens. Keep <c>MaxAttempts × attemptTimeout</c> (plus <see cref="MaxRetryAfter"/>)
    /// inside the calling client's own <c>Timeout</c>.
    /// </param>
    public GroqRetryHandler(ILogger<GroqRetryHandler> logger, TimeSpan attemptTimeout)
    {
        _logger = logger;
        _attemptTimeout = attemptTimeout;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Materialised once up front: a retry has to send the same bytes again, and the audio
        // upload's StreamContent can only be read a single time. The controller caps the upload at
        // 15 MB, so this is bounded.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var path = request.RequestUri?.AbsolutePath ?? "?";

        for (var attempt = 1; ; attempt++)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(_attemptTimeout);

            HttpResponseMessage response;
            try
            {
                using var attemptRequest = Clone(request, body);
                response = await base.SendAsync(attemptRequest, attemptCts.Token);
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken) && attempt < MaxAttempts)
            {
                _logger.LogWarning(ex,
                    "Groq {Path} attempt {Attempt} failed before any response ({Reason}); retrying.",
                    path, attempt, ex.GetType().Name);
                await Task.Delay(RetryDelay, cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return response;

            var reason     = await ReadReasonAsync(response, cancellationToken);
            var retryAfter = response.Headers.RetryAfter?.Delta;

            var retryable = attempt < MaxAttempts
                && IsTransientStatus(response.StatusCode)
                && (retryAfter is null || retryAfter <= MaxRetryAfter);

            if (!retryable)
            {
                // Left as-is for the caller: EnsureSuccessStatusCode() still throws, the log now
                // says why. Error level because at this point a user is seeing a failure.
                _logger.LogError(
                    "Groq {Path} returned {Status} after {Attempts} attempt(s). Retry-After: {RetryAfter}. Body: {Body}",
                    path, (int)response.StatusCode, attempt, retryAfter, reason);
                return response;
            }

            _logger.LogWarning(
                "Groq {Path} returned transient {Status} on attempt {Attempt}; retrying. Body: {Body}",
                path, (int)response.StatusCode, attempt, reason);

            response.Dispose();
            await Task.Delay(retryAfter ?? RetryDelay, cancellationToken);
        }
    }

    /// <summary>
    /// A fresh message per attempt: an <see cref="HttpRequestMessage"/> and its content cannot be
    /// sent twice. Default request headers (the Groq bearer token) are already merged onto
    /// <paramref name="request"/> by <c>HttpClient</c> before the pipeline runs, so they come along.
    /// </summary>
    private static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content!.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    // A cancelled outer token is the caller giving up (HttpClient.Timeout, or the beekeeper closing
    // the screen) — retrying then would only burn quota on an answer nobody is waiting for.
    private static bool IsTransientFailure(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && ex is HttpRequestException or OperationCanceledException;

    private static bool IsTransientStatus(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static async Task<string> ReadReasonAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            // Buffers the content, so the caller can still read it afterwards.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Length > MaxLoggedBodyChars ? body[..MaxLoggedBodyChars] + "…" : body;
        }
        catch (Exception ex)
        {
            // Never let diagnostics be the thing that breaks the request.
            return $"<unreadable: {ex.GetType().Name}>";
        }
    }
}
