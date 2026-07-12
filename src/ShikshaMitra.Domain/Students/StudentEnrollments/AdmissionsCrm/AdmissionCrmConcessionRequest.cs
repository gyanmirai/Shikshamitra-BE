using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConcessionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid? QuotationId { get; private set; }
	public AdmissionCrmConcessionType Type { get; private set; }
	public AdmissionCrmConcessionStatus Status { get; private set; }
	public decimal RequestedAmount { get; private set; }
	public decimal RequestedPercentage { get; private set; }
	public decimal ApprovedAmount { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string? EvidenceReference { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public string? DecisionReason { get; private set; }
	public string? FinanceReference { get; private set; }
	public DateTime? FinanceAcknowledgedAt { get; private set; }

	protected AdmissionCrmConcessionRequest()
	{
	}

	public AdmissionCrmConcessionRequest(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid? quotationId,
		AdmissionCrmConcessionType type, decimal requestedAmount, decimal requestedPercentage,
		string reason, string? evidenceReference, Guid requestedByUserId, DateTime requestedAt) : base(id)
	{
		if (requestedAmount < 0 || requestedPercentage is < 0 or > 100 ||
			(requestedAmount == 0 && requestedPercentage == 0))
		{
			throw new BusinessException("AdmissionCrm:InvalidConcessionRequest");
		}
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		QuotationId = quotationId;
		Type = type;
		RequestedAmount = requestedAmount;
		RequestedPercentage = requestedPercentage;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		EvidenceReference = Normalize(evidenceReference, AdmissionCrmConsts.MaxFinanceReferenceLength);
		RequestedByUserId = requestedByUserId;
		RequestedAt = requestedAt.ToUniversalTime();
		Status = AdmissionCrmConcessionStatus.ApprovalPending;
	}

	public void Approve(decimal approvedAmount, string reason, Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmConcessionStatus.ApprovalPending)
		{
			throw new BusinessException("AdmissionCrm:ConcessionNotAwaitingApproval");
		}
		if (RequestedByUserId == userId)
		{
			throw new BusinessException("AdmissionCrm:DistinctConcessionApproverRequired");
		}
		if (approvedAmount <= 0 || RequestedAmount > 0 && approvedAmount > RequestedAmount)
		{
			throw new BusinessException("AdmissionCrm:InvalidApprovedConcessionAmount");
		}
		ApprovedAmount = approvedAmount;
		ApprovedByUserId = userId;
		ApprovedAt = at.ToUniversalTime();
		DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmConcessionStatus.ApprovedPendingFinance;
	}

	public void Reject(string reason, Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmConcessionStatus.ApprovalPending)
		{
			throw new BusinessException("AdmissionCrm:ConcessionNotAwaitingApproval");
		}
		if (RequestedByUserId == userId)
		{
			throw new BusinessException("AdmissionCrm:DistinctConcessionApproverRequired");
		}
		ApprovedByUserId = userId;
		ApprovedAt = at.ToUniversalTime();
		DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmConcessionStatus.Rejected;
	}

	public void AcknowledgeFinance(string financeReference, DateTime at)
	{
		if (Status != AdmissionCrmConcessionStatus.ApprovedPendingFinance)
		{
			throw new BusinessException("AdmissionCrm:ApprovedConcessionRequired");
		}
		FinanceReference = Check.NotNullOrWhiteSpace(financeReference, nameof(financeReference), AdmissionCrmConsts.MaxFinanceReferenceLength);
		FinanceAcknowledgedAt = at.ToUniversalTime();
		Status = AdmissionCrmConcessionStatus.FinanceAcknowledged;
	}

	private static string? Normalize(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), maxLength);
}
