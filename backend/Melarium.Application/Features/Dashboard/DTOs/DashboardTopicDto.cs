using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>One published Edukacija topic marked for the current month, unread first.</summary>
public record DashboardTopicDto(int Id, string Title, string Summary, LearningCategory Category);
