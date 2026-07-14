using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFeePolicyApplicabilityDto
{
	public string? Campus { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public string? DesiredClass { get; set; }
	public string? AdmissionRoute { get; set; }
	public string? AdmissionRound { get; set; }
	public string? ApplicantCategory { get; set; }
}

public class AdmissionCrmFeeComponentDto
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string OwnerModule { get; set; } = "Finance";
	public string? AccountingReference { get; set; }
	public string? DueMilestone { get; set; }
	public bool IsRefundable { get; set; }
	public string? ParentDescription { get; set; }
}

public class CreateAdmissionCrmFeePolicyDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmFeePolicyApplicabilityDto Applicability { get; set; } = new();
	public IReadOnlyList<AdmissionCrmFeeComponentDto> Components { get; set; } = [];
	public string Currency { get; set; } = "INR";
	public AdmissionCrmPaymentGateType GateType { get; set; }
	public AdmissionCrmPaymentGateMilestone GateMilestone { get; set; }
	public decimal RequiredAmount { get; set; }
	public decimal RequiredPercentage { get; set; }
	public int GracePeriodHours { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
}

public class AdmissionCrmFeePolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmFeePolicyStatus Status { get; set; }
	public AdmissionCrmFeePolicyApplicabilityDto Applicability { get; set; } = new();
	public IReadOnlyList<AdmissionCrmFeeComponentDto> Components { get; set; } = [];
	public string Currency { get; set; } = "INR";
	public AdmissionCrmPaymentGateType GateType { get; set; }
	public AdmissionCrmPaymentGateMilestone GateMilestone { get; set; }
	public decimal RequiredAmount { get; set; }
	public decimal RequiredPercentage { get; set; }
	public int GracePeriodHours { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public Guid? SubmittedByUserId { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public DateTime? PublishedAt { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class GetAdmissionCrmFeePolicyListInput : PagedAndSortedResultRequestDto
{
	public AdmissionCrmFeePolicyStatus? Status { get; set; }
	public string? Code { get; set; }
}

public class IssueAdmissionCrmQuotationDto
{
	public Guid FeePolicyId { get; set; }
	public DateTime ValidUntil { get; set; }
}

public class AdmissionCrmAdmissionQuotationDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid FeePolicyId { get; set; }
	public int FeePolicyVersion { get; set; }
	public string QuotationNumber { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmQuotationStatus Status { get; set; }
	public string Currency { get; set; } = "INR";
	public IReadOnlyList<AdmissionCrmFeeComponentDto> Components { get; set; } = [];
	public decimal GrossAmount { get; set; }
	public decimal ConcessionAmount { get; set; }
	public decimal NetAmount { get; set; }
	public AdmissionCrmPaymentGateType GateType { get; set; }
	public AdmissionCrmPaymentGateMilestone GateMilestone { get; set; }
	public decimal RequiredAmount { get; set; }
	public AdmissionCrmFinanceState FinanceState { get; set; }
	public decimal ConfirmedAmount { get; set; }
	public string? FinanceReference { get; set; }
	public string? FinanceStateSource { get; set; }
	public DateTime? FinanceConfirmedAt { get; set; }
	public DateTime ValidUntil { get; set; }
	public DateTime? IssuedAt { get; set; }
	public bool GateOverrideUsed { get; set; }
	public string? GateOverrideReason { get; set; }
	public DateTime? GateOverrideUntil { get; set; }
	public bool IsGateSatisfied { get; set; }
}

public class RecordAdmissionCrmFinanceStateDto
{
	public AdmissionCrmFinanceState State { get; set; }
	public decimal ConfirmedAmount { get; set; }
	public string? FinanceReference { get; set; }
	public string Source { get; set; } = "Finance";
	public DateTime ConfirmedAt { get; set; }
}

public class OverrideAdmissionCrmPaymentGateDto
{
	public string Reason { get; set; } = string.Empty;
	public DateTime? Until { get; set; }
}

public class CreateAdmissionCrmConcessionRequestDto
{
	public Guid? QuotationId { get; set; }
	public AdmissionCrmConcessionType Type { get; set; }
	public decimal RequestedAmount { get; set; }
	public decimal RequestedPercentage { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? EvidenceReference { get; set; }
}

public class DecideAdmissionCrmConcessionDto
{
	public bool Approve { get; set; }
	public decimal ApprovedAmount { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class AcknowledgeAdmissionCrmConcessionFinanceDto
{
	public string FinanceReference { get; set; } = string.Empty;
}

public class AdmissionCrmConcessionRequestDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid? QuotationId { get; set; }
	public AdmissionCrmConcessionType Type { get; set; }
	public AdmissionCrmConcessionStatus Status { get; set; }
	public decimal RequestedAmount { get; set; }
	public decimal RequestedPercentage { get; set; }
	public decimal ApprovedAmount { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? EvidenceReference { get; set; }
	public Guid RequestedByUserId { get; set; }
	public DateTime RequestedAt { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public string? DecisionReason { get; set; }
	public string? FinanceReference { get; set; }
	public DateTime? FinanceAcknowledgedAt { get; set; }
}

public class AdmissionCrmFinancialReadinessDto
{
	public Guid LeadId { get; set; }
	public AdmissionCrmAdmissionQuotationDto? CurrentQuotation { get; set; }
	public IReadOnlyList<AdmissionCrmAdmissionQuotationDto> QuotationHistory { get; set; } = [];
	public IReadOnlyList<AdmissionCrmConcessionRequestDto> Concessions { get; set; } = [];
	public bool CanAcceptOffer { get; set; }
	public bool CanApproveAdmission { get; set; }
	public bool CanActivateStudent { get; set; }
	public string? BlockingReason { get; set; }
}
