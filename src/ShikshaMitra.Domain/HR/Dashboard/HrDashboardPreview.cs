using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Dashboard;

public class HrDashboardPreview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string DashboardKey { get; private set; } = string.Empty;
	public string ScopeKey { get; private set; } = HrDashboardScopeKeys.HrWide;
	public string PayloadJson { get; private set; } = "{}";
	public int PayloadVersion { get; private set; } = 1;
	public string Status { get; private set; } = HrDashboardPreviewStatus.Empty;
	public DateTime? LastRefreshedAt { get; private set; }
	public DateTime? RefreshStartedAt { get; private set; }
	public Guid? RefreshRequestedByUserId { get; private set; }
	public string? LastError { get; private set; }

	private HrDashboardPreview()
	{
	}

	public HrDashboardPreview(Guid id, Guid? tenantId, string dashboardKey, string scopeKey) : base(id)
	{
		TenantId = tenantId;
		DashboardKey = dashboardKey;
		ScopeKey = string.IsNullOrWhiteSpace(scopeKey) ? HrDashboardScopeKeys.HrWide : scopeKey;
	}

	public void MarkRefreshing(Guid? requestedByUserId, DateTime startedAt)
	{
		Status = HrDashboardPreviewStatus.Refreshing;
		RefreshStartedAt = startedAt;
		RefreshRequestedByUserId = requestedByUserId;
		LastError = null;
	}

	public void MarkReady(string payloadJson, int payloadVersion, DateTime refreshedAt)
	{
		PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
		PayloadVersion = payloadVersion;
		Status = HrDashboardPreviewStatus.Ready;
		LastRefreshedAt = refreshedAt;
		RefreshStartedAt = null;
		LastError = null;
	}

	public void MarkFailed(string error, DateTime failedAt)
	{
		Status = string.IsNullOrWhiteSpace(PayloadJson) || PayloadJson == "{}"
			? HrDashboardPreviewStatus.Failed
			: HrDashboardPreviewStatus.Ready;
		LastError = string.IsNullOrWhiteSpace(error) ? "Dashboard refresh failed." : error;
		RefreshStartedAt = null;
	}
}
