using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class DevelopmentPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public string Goal { get; private set; } = string.Empty;
	public string? ActionPlan { get; private set; }
	public Guid? OwnerEmployeeId { get; private set; }
	public DateTime? TargetDate { get; private set; }
	public DevelopmentPlanStatus Status { get; private set; }

	protected DevelopmentPlan()
	{
	}

	public DevelopmentPlan(Guid id, Guid? tenantId, Guid reviewAssignmentId, string goal, string? actionPlan, Guid? ownerEmployeeId, DateTime? targetDate) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		Update(goal, actionPlan, ownerEmployeeId, targetDate, DevelopmentPlanStatus.Open);
	}

	public void Update(string goal, string? actionPlan, Guid? ownerEmployeeId, DateTime? targetDate, DevelopmentPlanStatus status)
	{
		Goal = goal.Trim();
		ActionPlan = string.IsNullOrWhiteSpace(actionPlan) ? null : actionPlan.Trim();
		OwnerEmployeeId = ownerEmployeeId;
		TargetDate = targetDate?.Date;
		Status = status;
	}
}
