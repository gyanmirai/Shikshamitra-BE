using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.HR.Dashboard;

public class HrDashboardRefreshRun : FullAuditedAggregateRoot<Guid>
{
	public string RunKey { get; private set; } = string.Empty;
	public string Status { get; private set; } = HrDashboardRefreshRunStatus.Running;
	public DateTime StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public int TenantCount { get; private set; }
	public string? LastError { get; private set; }

	private HrDashboardRefreshRun()
	{
	}

	public HrDashboardRefreshRun(Guid id, string runKey, DateTime startedAt) : base(id)
	{
		RunKey = runKey;
		StartedAt = startedAt;
		Status = HrDashboardRefreshRunStatus.Running;
	}

	public void MarkCompleted(int tenantCount, DateTime completedAt)
	{
		Status = HrDashboardRefreshRunStatus.Completed;
		TenantCount = tenantCount;
		CompletedAt = completedAt;
		LastError = null;
	}

	public void MarkFailed(string error, DateTime failedAt)
	{
		Status = HrDashboardRefreshRunStatus.Failed;
		CompletedAt = failedAt;
		LastError = string.IsNullOrWhiteSpace(error) ? "Dashboard refresh run failed." : error;
	}

	public void Restart(DateTime startedAt)
	{
		Status = HrDashboardRefreshRunStatus.Running;
		StartedAt = startedAt;
		CompletedAt = null;
		LastError = null;
		TenantCount = 0;
	}
}
