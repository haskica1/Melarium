using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Melarium.Application.Features.Ai;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The retry layer in front of every Groq client. What matters here is what a beekeeper feels:
/// a transient 429/503 must be absorbed silently (with the *same* audio sent again), and a failure
/// that retrying cannot fix — a bad request, or a quota that resets in an hour — must not make them
/// wait for a second identical answer.
/// </summary>
public class GroqRetryHandlerTests
{
    [Fact]
    public async Task TransientStatus_IsRetried_AndTheSecondAttemptWins()
    {
        var stub = new StubHandler(
            Respond(HttpStatusCode.TooManyRequests),
            Respond(HttpStatusCode.OK));

        var response = await SendAsync(stub);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Calls);
    }

    [Fact]
    public async Task RetriedRequest_SendsTheSameBodyAgain()
    {
        var stub = new StubHandler(
            Respond(HttpStatusCode.ServiceUnavailable),
            Respond(HttpStatusCode.OK));

        // StreamContent is single-use — exactly what the audio upload uses, and the reason the
        // handler has to materialise the body before the first attempt.
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions")
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("audio-bytes"))),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");

        await SendAsync(stub, request);

        Assert.Equal(2, stub.Calls);
        Assert.Equal(new[] { "audio-bytes", "audio-bytes" }, stub.ReceivedBodies);
        Assert.All(stub.ReceivedContentTypes, ct => Assert.Equal("audio/webm", ct));
    }

    [Fact]
    public async Task NonTransientStatus_IsNotRetried()
    {
        var stub = new StubHandler(Respond(HttpStatusCode.BadRequest));

        var response = await SendAsync(stub);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, stub.Calls);
    }

    [Fact]
    public async Task RetryAfterLongerThanTheCap_FailsFast()
    {
        // A spent *daily* quota. Sleeping an hour inside the request would be worse than the error.
        var stub = new StubHandler(Respond(HttpStatusCode.TooManyRequests, TimeSpan.FromHours(1)));

        var response = await SendAsync(stub);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1, stub.Calls);
    }

    [Fact]
    public async Task StillFailingAfterTheRetry_ReturnsTheResponse_SoTheCallerStillThrows()
    {
        var stub = new StubHandler(
            Respond(HttpStatusCode.ServiceUnavailable),
            Respond(HttpStatusCode.ServiceUnavailable));

        var response = await SendAsync(stub);

        Assert.Equal(2, stub.Calls);
        Assert.False(response.IsSuccessStatusCode);
        Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode());
    }

    [Fact]
    public async Task HungAttempt_IsAbandonedAtTheAttemptBudget_AndRetried()
    {
        var stub = new StubHandler(
            async (_, ct) => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return new HttpResponseMessage(HttpStatusCode.OK); },
            Respond(HttpStatusCode.OK));

        var response = await SendAsync(stub, attemptTimeout: TimeSpan.FromMilliseconds(100));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Calls);
    }

    [Fact]
    public async Task CallerGivingUp_IsNotRetried()
    {
        var stub = new StubHandler(
            async (_, ct) => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return new HttpResponseMessage(HttpStatusCode.OK); });

        using var callerGaveUp = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SendAsync(stub, cancellationToken: callerGaveUp.Token));
        Assert.Equal(1, stub.Calls);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> SendAsync(
        StubHandler stub,
        HttpRequestMessage? request = null,
        TimeSpan? attemptTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var handler = new GroqRetryHandler(
            Substitute.For<ILogger<GroqRetryHandler>>(),
            attemptTimeout ?? TimeSpan.FromSeconds(30))
        {
            InnerHandler = stub,
        };

        using var invoker = new HttpMessageInvoker(handler);
        return await invoker.SendAsync(
            request ?? new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions"),
            cancellationToken);
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond(
        HttpStatusCode status, TimeSpan? retryAfter = null) =>
        (_, _) =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent("{\"error\":{\"code\":\"test\"}}") };
            if (retryAfter is not null)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter.Value);
            return Task.FromResult(response);
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses;

        public StubHandler(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses)
            => _responses = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(responses);

        public int Calls { get; private set; }
        public List<string> ReceivedBodies { get; } = [];
        public List<string?> ReceivedContentTypes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.Content is not null)
            {
                ReceivedBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                ReceivedContentTypes.Add(request.Content.Headers.ContentType?.MediaType);
            }

            // The last configured response repeats, so a test only lists what it cares about.
            var next = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return await next(request, cancellationToken);
        }
    }
}
