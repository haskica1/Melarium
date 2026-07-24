using Melarium.Application.Common.Models;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Hands notification emails off to a background worker so SMTP latency/failures
/// never block or fail the HTTP request that produced the notification.
/// </summary>
public interface IEmailQueue
{
    /// <summary>Enqueues an email for background delivery. Never blocks the caller.</summary>
    void Enqueue(QueuedEmail email);

    /// <summary>Dequeues the next email, waiting until one is available.</summary>
    ValueTask<QueuedEmail> DequeueAsync(CancellationToken cancellationToken);
}
