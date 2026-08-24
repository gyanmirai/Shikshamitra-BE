using System;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Compensation;

public class EmployeeCompensationRevision : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public int RevisionNumber { get; private set; }
	public DateTime EffectiveMonth { get; private set; }
	public CompensationRevisionStatus Status { get; private set; }
	public SalaryInputBasis SalaryBasis { get; private set; }
	public CompensationProtectionPolicy ProtectionPolicy { get; private set; }
	public decimal QuotedMonthlyAmount { get; private set; }
	public decimal ProtectedMonthlyAmount { get; private set; }
	public decimal GrossSalary { get; private set; }
	public decimal NetSalary { get; private set; }
	public decimal EmployerCost { get; private set; }
	public Guid? SourceSalaryTemplateId { get; private set; }
	public Guid? SourceSalaryTemplateVersionId { get; private set; }
	public int? SourceSalaryTemplateVersion { get; private set; }
	public Guid? PfRuleId { get; private set; }
	public string StatutoryProfile { get; private set; } = "IN-GJ";
	public string CalculationSnapshotJson { get; private set; } = "{}";
	public Guid? SubmittedByUserId { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public Guid? RejectedByUserId { get; private set; }
	public DateTime? RejectedAt { get; private set; }
	public string? RejectionReason { get; private set; }

	protected EmployeeCompensationRevision()
	{
	}

	public EmployeeCompensationRevision(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		int revisionNumber,
		DateTime effectiveMonth) : base(id)
	{
		if (employeeId == Guid.Empty || revisionNumber <= 0 || effectiveMonth.Day != 1)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionInvalid");
		}

		TenantId = tenantId;
		EmployeeId = employeeId;
		RevisionNumber = revisionNumber;
		EffectiveMonth = DateTime.SpecifyKind(effectiveMonth.Date, DateTimeKind.Utc);
		Status = CompensationRevisionStatus.Draft;
	}

	public void ReplaceDraft(
		DateTime effectiveMonth,
		SalaryInputBasis salaryBasis,
		CompensationProtectionPolicy protectionPolicy,
		decimal quotedMonthlyAmount,
		decimal protectedMonthlyAmount,
		decimal grossSalary,
		decimal netSalary,
		decimal employerCost,
		Guid? sourceSalaryTemplateId,
		Guid? sourceSalaryTemplateVersionId,
		int? sourceSalaryTemplateVersion,
		Guid? pfRuleId,
		string statutoryProfile,
		string calculationSnapshotJson)
	{
		EnsureDraft();
		if (effectiveMonth.Day != 1 || quotedMonthlyAmount <= 0m || grossSalary <= 0m || protectedMonthlyAmount < 0m || netSalary < 0m || employerCost < grossSalary)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionInvalid");
		}

		EffectiveMonth = DateTime.SpecifyKind(effectiveMonth.Date, DateTimeKind.Utc);
		SalaryBasis = salaryBasis;
		ProtectionPolicy = protectionPolicy;
		QuotedMonthlyAmount = quotedMonthlyAmount;
		ProtectedMonthlyAmount = protectedMonthlyAmount;
		GrossSalary = grossSalary;
		NetSalary = netSalary;
		EmployerCost = employerCost;
		SourceSalaryTemplateId = sourceSalaryTemplateId;
		SourceSalaryTemplateVersionId = sourceSalaryTemplateVersionId;
		SourceSalaryTemplateVersion = sourceSalaryTemplateVersion;
		PfRuleId = pfRuleId;
		StatutoryProfile = Check.NotNullOrWhiteSpace(statutoryProfile, nameof(statutoryProfile), 16);
		CalculationSnapshotJson = Check.NotNullOrWhiteSpace(calculationSnapshotJson, nameof(calculationSnapshotJson));
	}

	public void Submit(Guid submittedByUserId, DateTime submittedAt)
	{
		EnsureDraft();
		Status = CompensationRevisionStatus.PendingApproval;
		SubmittedByUserId = submittedByUserId;
		SubmittedAt = submittedAt;
		RejectedByUserId = null;
		RejectedAt = null;
		RejectionReason = null;
	}

	public void Approve(Guid approvedByUserId, DateTime approvedAt)
	{
		if (Status != CompensationRevisionStatus.PendingApproval)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionNotPending");
		}
		if (approvedByUserId == CreatorId || approvedByUserId == SubmittedByUserId)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionSelfApprovalDenied");
		}

		Status = CompensationRevisionStatus.Approved;
		ApprovedByUserId = approvedByUserId;
		ApprovedAt = approvedAt;
	}

	public void Reject(Guid rejectedByUserId, DateTime rejectedAt, string reason)
	{
		if (Status != CompensationRevisionStatus.PendingApproval)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionNotPending");
		}
		if (rejectedByUserId == CreatorId || rejectedByUserId == SubmittedByUserId)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionSelfApprovalDenied");
		}

		Status = CompensationRevisionStatus.Rejected;
		RejectedByUserId = rejectedByUserId;
		RejectedAt = rejectedAt;
		RejectionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 500).Trim();
	}

	public void Supersede()
	{
		if (Status != CompensationRevisionStatus.Approved)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionNotApproved");
		}

		Status = CompensationRevisionStatus.Superseded;
	}

	private void EnsureDraft()
	{
		if (Status != CompensationRevisionStatus.Draft)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionNotDraft");
		}
	}
}

public class EmployeeCompensationRevisionLine : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RevisionId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid? ComponentId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public decimal Amount { get; private set; }
	public bool IsEarning { get; private set; }
	public SalaryComponentType ComponentType { get; private set; }
	public SalaryComponentDependency DependOn { get; private set; }
	public PayrollComponentSemanticType SemanticType { get; private set; }
	public CompensationLineSource Source { get; private set; }
	public int DisplayOrder { get; private set; }

	protected EmployeeCompensationRevisionLine()
	{
	}

	public EmployeeCompensationRevisionLine(
		Guid id,
		Guid? tenantId,
		Guid revisionId,
		Guid employeeId,
		Guid? componentId,
		string name,
		string? description,
		decimal amount,
		bool isEarning,
		PayrollComponentSemanticType semanticType,
		CompensationLineSource source,
		int displayOrder) : base(id)
	{
		if (revisionId == Guid.Empty || employeeId == Guid.Empty || amount < 0m || displayOrder < 0)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionLineInvalid");
		}

		TenantId = tenantId;
		RevisionId = revisionId;
		EmployeeId = employeeId;
		ComponentId = componentId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100).Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		Amount = amount;
		IsEarning = isEarning;
		ComponentType = SalaryComponentType.Fixed;
		DependOn = SalaryComponentDependency.None;
		SemanticType = semanticType;
		Source = source;
		DisplayOrder = displayOrder;
	}
}
