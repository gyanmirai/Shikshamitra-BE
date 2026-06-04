using System;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardPreviewProjectionService : ITransientDependency
{
	private const int PayloadVersion = 1;
	private static readonly TimeSpan PreviewStaleAfter = TimeSpan.FromHours(24);

	public HRDashboardPreviewDto Empty(string dashboardKey, string scopeKey, DateTime now) =>
		new()
		{
			DashboardKey = dashboardKey,
			ScopeKey = scopeKey,
			Status = HrDashboardPreviewStatus.Empty,
			PayloadJson = "{}",
			PayloadVersion = PayloadVersion,
			IsStale = true,
			NextScheduledRefreshAt = HRDashboardPreviewSchedule.GetNextScheduledRefreshAt(now)
		};

	public HRDashboardPreviewDto ToDto(HrDashboardPreview preview, DateTime now) =>
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
			NextScheduledRefreshAt = HRDashboardPreviewSchedule.GetNextScheduledRefreshAt(now),
			RefreshStartedAt = preview.RefreshStartedAt,
			RefreshRequestedByUserId = preview.RefreshRequestedByUserId,
			LastError = preview.LastError
		};
}
