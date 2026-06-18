using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentDashboardPreview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string DashboardKey { get; private set; } = string.Empty;
	public string ScopeKey { get; private set; } = StudentDashboardScopeKeys.StudentWide;
	public string PayloadJson { get; private set; } = "{}";
	public int PayloadVersion { get; private set; } = 1;
	public string Status { get; private set; } = StudentDashboardPreviewStatus.Empty;
	public DateTime? LastRefreshedAt { get; private set; }
	public DateTime? RefreshStartedAt { get; private set; }
	public Guid? RefreshRequestedByUserId { get; private set; }
	public string? LastError { get; private set; }

	private StudentDashboardPreview()
	{
	}

	public StudentDashboardPreview(Guid id, Guid? tenantId, string dashboardKey, string scopeKey) : base(id)
	{
		TenantId = tenantId;
		DashboardKey = dashboardKey;
		ScopeKey = string.IsNullOrWhiteSpace(scopeKey) ? StudentDashboardScopeKeys.StudentWide : scopeKey;
	}

	public void MarkRefreshing(Guid? requestedByUserId, DateTime startedAt)
	{
		Status = StudentDashboardPreviewStatus.Refreshing;
		RefreshStartedAt = startedAt;
		RefreshRequestedByUserId = requestedByUserId;
		LastError = null;
	}

	public void MarkReady(string payloadJson, int payloadVersion, DateTime refreshedAt)
	{
		PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
		PayloadVersion = payloadVersion;
		Status = StudentDashboardPreviewStatus.Ready;
		LastRefreshedAt = refreshedAt;
		RefreshStartedAt = null;
		LastError = null;
	}

	public void MarkFailed(string error)
	{
		Status = string.IsNullOrWhiteSpace(PayloadJson) || PayloadJson == "{}"
			? StudentDashboardPreviewStatus.Failed
			: StudentDashboardPreviewStatus.Ready;
		LastError = string.IsNullOrWhiteSpace(error)
			? "Student dashboard refresh failed."
			: error;
		RefreshStartedAt = null;
	}
}
