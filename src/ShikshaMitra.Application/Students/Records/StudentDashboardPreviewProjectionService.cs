using System;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Records;

public class StudentDashboardPreviewProjectionService : ITransientDependency
{
	private const int PayloadVersion = 1;
	private static readonly TimeSpan PreviewStaleAfter = TimeSpan.FromHours(24);

	public StudentDashboardPreviewDto ToDto(StudentDashboardPreview preview, DateTime now) =>
		new()
		{
			DashboardKey = preview.DashboardKey,
			ScopeKey = preview.ScopeKey,
			Status = preview.Status,
			PayloadJson = preview.PayloadJson,
			PayloadVersion = preview.PayloadVersion,
			IsStale = !preview.LastRefreshedAt.HasValue || now - preview.LastRefreshedAt.Value > PreviewStaleAfter,
			LastRefreshedAt = preview.LastRefreshedAt,
			LastSuccessfulRefreshAt = preview.LastRefreshedAt,
			NextScheduledRefreshAt = StudentDashboardPreviewSchedule.GetNextScheduledRefreshAt(now),
			RefreshStartedAt = preview.RefreshStartedAt,
			RefreshRequestedByUserId = preview.RefreshRequestedByUserId,
			LastError = preview.LastError
		};

	public StudentDashboardPreviewDto Empty(string dashboardKey, string scopeKey, DateTime now) =>
		new()
		{
			DashboardKey = dashboardKey,
			ScopeKey = scopeKey,
			Status = StudentDashboardPreviewStatus.Empty,
			PayloadJson = "{}",
			PayloadVersion = PayloadVersion,
			IsStale = true,
			NextScheduledRefreshAt = StudentDashboardPreviewSchedule.GetNextScheduledRefreshAt(now)
		};
}
