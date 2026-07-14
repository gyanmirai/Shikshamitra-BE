using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionQuotation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid FeePolicyId { get; private set; }
	public int FeePolicyVersion { get; private set; }
	public string QuotationNumber { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmQuotationStatus Status { get; private set; }
	public string Currency { get; private set; } = "INR";
	public string ComponentsSnapshotJson { get; private set; } = "[]";
	public decimal GrossAmount { get; private set; }
	public decimal ConcessionAmount { get; private set; }
	public decimal NetAmount { get; private set; }
	public AdmissionCrmPaymentGateType GateType { get; private set; }
	public AdmissionCrmPaymentGateMilestone GateMilestone { get; private set; }
	public decimal RequiredAmount { get; private set; }
	public AdmissionCrmFinanceState FinanceState { get; private set; }
	public decimal ConfirmedAmount { get; private set; }
	public string? FinanceReference { get; private set; }
	public string? FinanceStateSource { get; private set; }
	public DateTime? FinanceConfirmedAt { get; private set; }
	public DateTime ValidUntil { get; private set; }
	public DateTime? IssuedAt { get; private set; }
	public bool GateOverrideUsed { get; private set; }
	public string? GateOverrideReason { get; private set; }
	public Guid? GateOverrideByUserId { get; private set; }
	public DateTime? GateOverrideAt { get; private set; }
	public DateTime? GateOverrideUntil { get; private set; }

	public bool IsGateSatisfiedAt(DateTime at) => GateOverrideUsed && (!GateOverrideUntil.HasValue || GateOverrideUntil.Value >= at.ToUniversalTime()) ||
		GateType == AdmissionCrmPaymentGateType.None ||
		FinanceState is AdmissionCrmFinanceState.NotRequired or AdmissionCrmFinanceState.Waived or AdmissionCrmFinanceState.Sponsored ||
		GateType == AdmissionCrmPaymentGateType.ReferenceRecorded && !string.IsNullOrWhiteSpace(FinanceReference) ||
		GateType == AdmissionCrmPaymentGateType.PaymentInitiated && FinanceState is not (AdmissionCrmFinanceState.NotStarted or AdmissionCrmFinanceState.Failed) ||
		GateType == AdmissionCrmPaymentGateType.PartialAmountConfirmed && ConfirmedAmount > 0 ||
		GateType == AdmissionCrmPaymentGateType.RequiredAmountConfirmed && ConfirmedAmount >= RequiredAmount ||
		GateType == AdmissionCrmPaymentGateType.FinanceCleared && FinanceState == AdmissionCrmFinanceState.Confirmed;

	protected AdmissionCrmAdmissionQuotation()
	{
	}

	public AdmissionCrmAdmissionQuotation(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid feePolicyId,
		int feePolicyVersion, string quotationNumber, int version, string currency,
		string componentsSnapshotJson, decimal grossAmount, decimal concessionAmount,
		AdmissionCrmPaymentGateType gateType, AdmissionCrmPaymentGateMilestone gateMilestone,
		decimal requiredAmount, DateTime validUntil) : base(id)
	{
		if (feePolicyVersion <= 0 || version <= 0 || grossAmount < 0 || concessionAmount < 0 ||
			concessionAmount > grossAmount || requiredAmount < 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionQuotation");
		}
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		FeePolicyId = feePolicyId;
		FeePolicyVersion = feePolicyVersion;
		QuotationNumber = Check.NotNullOrWhiteSpace(quotationNumber, nameof(quotationNumber), AdmissionCrmConsts.MaxQuotationNumberLength);
		Version = version;
		Currency = Check.NotNullOrWhiteSpace(currency, nameof(currency), AdmissionCrmConsts.MaxCurrencyCodeLength).ToUpperInvariant();
		ComponentsSnapshotJson = ValidateComponents(componentsSnapshotJson);
		GrossAmount = grossAmount;
		ConcessionAmount = concessionAmount;
		NetAmount = grossAmount - concessionAmount;
		GateType = gateType;
		GateMilestone = gateMilestone;
		RequiredAmount = requiredAmount;
		ValidUntil = validUntil.ToUniversalTime();
		FinanceState = gateType == AdmissionCrmPaymentGateType.None
			? AdmissionCrmFinanceState.NotRequired
			: AdmissionCrmFinanceState.NotStarted;
		Status = AdmissionCrmQuotationStatus.Draft;
	}

	public void Issue(DateTime at)
	{
		if (Status != AdmissionCrmQuotationStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:QuotationNotDraft");
		}
		var issuedAt = at.ToUniversalTime();
		if (ValidUntil <= issuedAt)
		{
			throw new BusinessException("AdmissionCrm:QuotationExpiredBeforeIssue");
		}
		IssuedAt = issuedAt;
		Status = AdmissionCrmQuotationStatus.Issued;
	}

	public void Supersede()
	{
		if (Status is not (AdmissionCrmQuotationStatus.Draft or AdmissionCrmQuotationStatus.Issued))
		{
			throw new BusinessException("AdmissionCrm:QuotationCannotBeSuperseded");
		}
		Status = AdmissionCrmQuotationStatus.Superseded;
	}

	public void RecordFinanceState(
		AdmissionCrmFinanceState state, decimal confirmedAmount, string? reference,
		string source, DateTime confirmedAt)
	{
		if (Status != AdmissionCrmQuotationStatus.Issued)
		{
			throw new BusinessException("AdmissionCrm:IssuedQuotationRequired");
		}
		if (confirmedAmount < 0 || confirmedAmount > NetAmount)
		{
			throw new BusinessException("AdmissionCrm:InvalidConfirmedAmount");
		}
		if (state == AdmissionCrmFinanceState.Confirmed && confirmedAmount <= 0 && NetAmount > 0)
		{
			throw new BusinessException("AdmissionCrm:ConfirmedFinanceAmountRequired");
		}
		FinanceState = state;
		ConfirmedAmount = confirmedAmount;
		FinanceReference = Normalize(reference, AdmissionCrmConsts.MaxFinanceReferenceLength);
		FinanceStateSource = Check.NotNullOrWhiteSpace(source, nameof(source), AdmissionCrmConsts.MaxNameLength);
		FinanceConfirmedAt = confirmedAt.ToUniversalTime();
	}

	public void OverrideGate(string reason, Guid userId, DateTime at, DateTime? until)
	{
		var overriddenAt = at.ToUniversalTime();
		var overriddenUntil = until?.ToUniversalTime();
		if (overriddenUntil.HasValue && overriddenUntil.Value <= overriddenAt)
		{
			throw new BusinessException("AdmissionCrm:InvalidPaymentGateOverrideExpiry");
		}
		GateOverrideReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		GateOverrideByUserId = userId;
		GateOverrideAt = overriddenAt;
		GateOverrideUntil = overriddenUntil;
		GateOverrideUsed = true;
	}

	public void Expire(DateTime at)
	{
		if (Status != AdmissionCrmQuotationStatus.Issued || at.ToUniversalTime() < ValidUntil)
		{
			throw new BusinessException("AdmissionCrm:QuotationNotExpirable");
		}
		Status = AdmissionCrmQuotationStatus.Expired;
	}

	private static string ValidateComponents(string json)
	{
		var value = Check.NotNullOrWhiteSpace(json, nameof(json), AdmissionCrmConsts.MaxFinancialPolicyJsonLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != JsonValueKind.Array)
		{
			throw new BusinessException("AdmissionCrm:InvalidQuotationComponents");
		}
		return value;
	}

	private static string? Normalize(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), maxLength);
}
