namespace Melarium.Application.Common.Models;

/// <summary>A notification email waiting to be delivered by the background email worker.</summary>
public sealed record QueuedEmail(int UserId, string Title, string Message);
