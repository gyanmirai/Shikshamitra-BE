using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFeePolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmFeePolicyStatus Status { get; private set; }
	public string ApplicabilityJson { get; private set; } = "{}";
	public string ComponentsJson { get; private set; } = "[]";
	public string Currency { get; private set; } = "INR";
	public AdmissionCrmPaymentGateType GateType { get; private set; }
	public AdmissionCrmPaymentGateMilestone GateMilestone { get; private set; }
	public decimal RequiredAmount { get; private set; }
	public decimal RequiredPercentage { get; private set; }
	public int GracePeriodHours { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public Guid? SubmittedByUserId { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public DateTime? PublishedAt { get; private set; }

	protected AdmissionCrmFeePolicy()
	{
	}

	public AdmissionCrmFeePolicy(
		Guid id, Guid? tenantId, Guid academicYearId, string code, string name, int version,
		string applicabilityJson, string componentsJson, string currency,
		AdmissionCrmPaymentGateType gateType, AdmissionCrmPaymentGateMilestone gateMilestone,
		decimal requiredAmount, decimal requiredPercentage, int gracePeriodHours,
		DateTime effectiveFrom, DateTime? effectiveUntil) : base(id)
	{
		if (version <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidFeePolicyVersion");
		}
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPlaybookCodeLength);
		Version = version;
		Status = AdmissionCrmFeePolicyStatus.Draft;
		UpdateDraft(name, applicabilityJson, componentsJson, currency, gateType, gateMilestone,
			requiredAmount, requiredPercentage, gracePeriodHours, effectiveFrom, effectiveUntil);
	}

	public void UpdateDraft(
		string name, string applicabilityJson, string componentsJson, string currency,
		AdmissionCrmPaymentGateType gateType, AdmissionCrmPaymentGateMilestone gateMilestone,
		decimal requiredAmount, decimal requiredPercentage, int gracePeriodHours,
		DateTime effectiveFrom, DateTime? effectiveUntil)
	{
		if (Status != AdmissionCrmFeePolicyStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:OnlyDraftFeePolicyCanBeChanged");
		}
		var from = effectiveFrom.ToUniversalTime();
		var until = effectiveUntil?.ToUniversalTime();
		if (until.HasValue && until.Value <= from)
		{
			throw new BusinessException("AdmissionCrm:InvalidFeePolicyEffectivePeriod");
		}
		if (requiredAmount < 0 || requiredPercentage is < 0 or > 100 || gracePeriodHours < 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidPaymentGate");
		}
		if (gateType == AdmissionCrmPaymentGateType.RequiredAmountConfirmed && requiredAmount <= 0)
		{
			throw new BusinessException("AdmissionCrm:PaymentGateRequiredAmountMissing");
		}

		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		ApplicabilityJson = ValidateJson(applicabilityJson, JsonValueKind.Object, nameof(applicabilityJson));
		ComponentsJson = ValidateJson(componentsJson, JsonValueKind.Array, nameof(componentsJson));
		Currency = Check.NotNullOrWhiteSpace(currency, nameof(currency), AdmissionCrmConsts.MaxCurrencyCodeLength).ToUpperInvariant();
		GateType = gateType;
		GateMilestone = gateMilestone;
		RequiredAmount = requiredAmount;
		RequiredPercentage = requiredPercentage;
		GracePeriodHours = gracePeriodHours;
		EffectiveFrom = from;
		EffectiveUntil = until;
	}

	public void Submit(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmFeePolicyStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:FeePolicyNotDraft");
		}
		SubmittedByUserId = userId;
		SubmittedAt = at.ToUniversalTime();
		Status = AdmissionCrmFeePolicyStatus.ApprovalPending;
	}

	public void Approve(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmFeePolicyStatus.ApprovalPending)
		{
			throw new BusinessException("AdmissionCrm:FeePolicyNotAwaitingApproval");
		}
		if (SubmittedByUserId == userId)
		{
			throw new BusinessException("AdmissionCrm:DistinctFeePolicyApproverRequired");
		}
		ApprovedByUserId = userId;
		ApprovedAt = at.ToUniversalTime();
		Status = AdmissionCrmFeePolicyStatus.Approved;
	}

	public void Publish(DateTime at)
	{
		if (Status != AdmissionCrmFeePolicyStatus.Approved)
		{
			throw new BusinessException("AdmissionCrm:ApprovedFeePolicyRequired");
		}
		PublishedAt = at.ToUniversalTime();
		Status = AdmissionCrmFeePolicyStatus.Published;
	}

	public void Retire()
	{
		if (Status != AdmissionCrmFeePolicyStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:PublishedFeePolicyRequired");
		}
		Status = AdmissionCrmFeePolicyStatus.Retired;
	}

	private static string ValidateJson(string json, JsonValueKind kind, string name)
	{
		var value = Check.NotNullOrWhiteSpace(json, name, AdmissionCrmConsts.MaxFinancialPolicyJsonLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != kind)
		{
			throw new BusinessException("AdmissionCrm:InvalidFinancialPolicyJson").WithData("Field", name);
		}
		return value;
	}
}
