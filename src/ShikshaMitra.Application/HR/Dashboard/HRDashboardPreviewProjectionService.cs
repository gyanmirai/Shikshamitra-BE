using System;
using System.Text.Json;
using System.Text.Json.Nodes;
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
			PayloadJson = preview.DashboardKey == HrDashboardKeys.Attendance
				? RemoveLegacyAttendanceIdentities(preview.PayloadJson)
				: preview.PayloadJson,
			PayloadVersion = preview.PayloadVersion,
			IsStale = !preview.LastRefreshedAt.HasValue || now - preview.LastRefreshedAt.Value > PreviewStaleAfter,
			LastRefreshedAt = preview.LastRefreshedAt,
			LastSuccessfulRefreshAt = preview.LastRefreshedAt,
			NextScheduledRefreshAt = HRDashboardPreviewSchedule.GetNextScheduledRefreshAt(now),
			RefreshStartedAt = preview.RefreshStartedAt,
			RefreshRequestedByUserId = preview.RefreshRequestedByUserId,
			LastError = preview.LastError
		};

	private static string RemoveLegacyAttendanceIdentities(string payload)
	{
		try
		{
			if (JsonNode.Parse(payload) is not JsonObject attendance) return "{}";
			if (attendance.ContainsKey("topDefaulters")) attendance["topDefaulters"] = new JsonArray();
			return attendance.ToJsonString();
		}
		catch (JsonException)
		{
			return "{}";
		}
	}
}
