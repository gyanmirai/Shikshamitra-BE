using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollApprovalSettings : AggregateRoot<Guid>, IMultiTenant
{
	public const string Legacy = "Legacy";
	public const string Active = "Active";
	public const string Paused = "Paused";

	public Guid? TenantId { get; private set; }
	public Guid? ApproverId { get; private set; }
	public bool AllowSelfFinalization { get; private set; }
	public int Version { get; private set; }
	public string Mode { get; private set; } = Legacy;

	private PayrollApprovalSettings() { }
	public PayrollApprovalSettings(Guid tenantId) : base(tenantId) => TenantId = tenantId;

	public void Configure(Guid approverId, bool allowSelfFinalization)
	{
		if (approverId == Guid.Empty) throw new BusinessException("PAYROLL_APPROVER_REQUIRED");
		ApproverId = approverId;
		AllowSelfFinalization = allowSelfFinalization;
		Version++;
	}

	public void Transition(string action)
	{
		Mode = (Mode, action, ApproverId.HasValue) switch
		{
			(Legacy, "activate", true) or (Paused, "resume", true) => Active,
			(Active, "pause", _) => Paused,
			_ => throw new BusinessException("PAYROLL_WORKFLOW_TRANSITION_INVALID")
		};
		Version++;
	}
}
