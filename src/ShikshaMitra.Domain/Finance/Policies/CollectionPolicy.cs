using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Collection Policy owns late fee + waiver rules directly (see
/// collection-policy-implementation-plan.md §0, decision 1). The retired
/// ILateFeePolicy type is not mirrored here — this is the only backend home
/// for late-fee/waiver configuration.
/// </summary>
public class CollectionPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }

	public string PolicyName { get; protected set; } = string.Empty;
	public string PolicyCode { get; protected set; } = string.Empty;
	public string Description { get; protected set; } = string.Empty;
	public CollectionPolicyStatus Status { get; protected set; }
	public bool IsDefault { get; protected set; }
	public DateTime? EffectiveFrom { get; protected set; }
	public DateTime? EffectiveTo { get; protected set; }

	// Late Fee
	public bool EnableLateFee { get; protected set; }
	public int GracePeriodDays { get; protected set; }
	public GracePeriodType GracePeriodType { get; protected set; }
	public string LateFeeApplyOnFeeHeadsJson { get; protected set; } = "[]";
	public LateFeeCalculationType LateFeeType { get; protected set; }
	public decimal? FlatAmount { get; protected set; }
	public decimal? Percentage { get; protected set; }
	public PercentageBaseType PercentageBase { get; protected set; }
	public decimal? AmountPerDay { get; protected set; }
	public decimal? AmountPerWeek { get; protected set; }
	public decimal? AmountPerMonth { get; protected set; }
	public CompoundFrequencyType CompoundFrequency { get; protected set; }
	public string EscalationRulesJson { get; protected set; } = "[]";
	public string FeeHeadOverridesJson { get; protected set; } = "[]";
	public RoundingMethodType RoundingMethod { get; protected set; }
	public bool ConsiderAsIncome { get; protected set; }
	public Guid? LateFeeIncomeHeadId { get; protected set; }
	/// <summary>Absolute ceiling on computed late fee, regardless of formula — closes the "runaway compounding" gap.</summary>
	public decimal? MaximumLateFeeAmount { get; protected set; }
	/// <summary>Stop accruing additional late fee once daysLate exceeds this many days — independent of the amount cap above.</summary>
	public int? MaximumChargingDays { get; protected set; }

	// Waiver
	public bool EnableWaiver { get; protected set; }
	public string WaiverApplyOnFeeHeadsJson { get; protected set; } = "[]";
	public decimal? MaximumWaiverAmount { get; protected set; }
	public decimal? MaximumWaiverPercentage { get; protected set; }
	public DateTime? WaiverExpiry { get; protected set; }
	public bool RequiresApproval { get; protected set; }
	public string ApprovalRoleIdsJson { get; protected set; } = "[]";
	public decimal? ApprovalThresholdAmount { get; protected set; }

	// Online Collection
	public bool AllowOnlineCollection { get; protected set; }

	public int Version { get; protected set; }

	protected CollectionPolicy()
	{
	}

	public CollectionPolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		CollectionPolicyUpdateModel model) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 0;
		Update(model);
	}

	public void Update(CollectionPolicyUpdateModel model)
	{
		if (string.IsNullOrWhiteSpace(model.PolicyName))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.NameRequired);
		}

		if (AcademicYearId == Guid.Empty)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearRequired);
		}

		if (model.IsDefault && model.Status != CollectionPolicyStatus.Active)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.DefaultPolicyMustBeActive);
		}

		if (model.LateFeeType == LateFeeCalculationType.Slab)
		{
			CollectionPolicySlabValidator.EnsureNoOverlapsOrGaps(model.EscalationRules);
		}

		ValidateFeeHeadOverrides(model.FeeHeadOverrides);

		PolicyName = model.PolicyName.Trim();
		PolicyCode = model.PolicyCode.Trim();
		Description = model.Description?.Trim() ?? string.Empty;
		Status = model.Status;
		IsDefault = model.IsDefault;
		EffectiveFrom = model.EffectiveFrom?.Date;
		EffectiveTo = model.EffectiveTo?.Date;

		EnableLateFee = model.EnableLateFee;
		GracePeriodDays = model.GracePeriodDays;
		GracePeriodType = model.GracePeriodType;
		LateFeeApplyOnFeeHeadsJson = SerializeGuids(model.LateFeeApplyOnFeeHeads);
		LateFeeType = model.LateFeeType;
		FlatAmount = model.FlatAmount;
		Percentage = model.Percentage;
		PercentageBase = model.PercentageBase;
		AmountPerDay = model.AmountPerDay;
		AmountPerWeek = model.AmountPerWeek;
		AmountPerMonth = model.AmountPerMonth;
		CompoundFrequency = model.CompoundFrequency;
		EscalationRulesJson = SerializeEscalationRules(model.EscalationRules);
		FeeHeadOverridesJson = SerializeFeeHeadOverrides(model.FeeHeadOverrides);
		RoundingMethod = model.RoundingMethod;
		ConsiderAsIncome = model.ConsiderAsIncome;
		LateFeeIncomeHeadId = model.LateFeeIncomeHeadId;
		MaximumLateFeeAmount = model.MaximumLateFeeAmount;
		MaximumChargingDays = model.MaximumChargingDays;

		EnableWaiver = model.EnableWaiver;
		WaiverApplyOnFeeHeadsJson = SerializeGuids(model.WaiverApplyOnFeeHeads);
		MaximumWaiverAmount = model.MaximumWaiverAmount;
		MaximumWaiverPercentage = model.MaximumWaiverPercentage;
		WaiverExpiry = model.WaiverExpiry?.Date;
		RequiresApproval = model.RequiresApproval;
		ApprovalRoleIdsJson = SerializeGuids(model.ApprovalRoleIds);
		ApprovalThresholdAmount = model.ApprovalThresholdAmount;

		AllowOnlineCollection = model.AllowOnlineCollection;

		Version++;
	}

	public void Archive()
	{
		if (IsDefault)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.DefaultPolicyMustBeActive)
				.WithData("reason", "cannot_archive_default");
		}

		Status = CollectionPolicyStatus.Archived;
		Version++;
	}

	public void Unarchive()
	{
		Status = CollectionPolicyStatus.Active;
		Version++;
	}

	public IReadOnlyList<Guid> GetLateFeeApplyOnFeeHeads() => DeserializeGuids(LateFeeApplyOnFeeHeadsJson);
	public IReadOnlyList<Guid> GetWaiverApplyOnFeeHeads() => DeserializeGuids(WaiverApplyOnFeeHeadsJson);
	public IReadOnlyList<Guid> GetApprovalRoleIds() => DeserializeGuids(ApprovalRoleIdsJson);

	public IReadOnlyList<CollectionPolicyEscalationRule> GetEscalationRules() =>
		string.IsNullOrWhiteSpace(EscalationRulesJson)
			? Array.Empty<CollectionPolicyEscalationRule>()
			: JsonSerializer.Deserialize<List<CollectionPolicyEscalationRule>>(EscalationRulesJson)
				?? new List<CollectionPolicyEscalationRule>();

	public IReadOnlyList<CollectionPolicyFeeHeadOverride> GetFeeHeadOverrides() =>
		string.IsNullOrWhiteSpace(FeeHeadOverridesJson)
			? Array.Empty<CollectionPolicyFeeHeadOverride>()
			: JsonSerializer.Deserialize<List<CollectionPolicyFeeHeadOverride>>(FeeHeadOverridesJson)
				?? new List<CollectionPolicyFeeHeadOverride>();

	public bool TryGetFeeHeadOverride(Guid feeHeadId, out CollectionPolicyFeeHeadOverride? feeHeadOverride)
	{
		feeHeadOverride = GetFeeHeadOverrides().FirstOrDefault(o => o.FeeHeadId == feeHeadId);
		return feeHeadOverride is not null;
	}

	private static void ValidateFeeHeadOverrides(IReadOnlyList<CollectionPolicyFeeHeadOverride>? overrides)
	{
		if (overrides is null || overrides.Count == 0)
		{
			return;
		}

		var duplicate = overrides.GroupBy(o => o.FeeHeadId).FirstOrDefault(g => g.Count() > 1);
		if (duplicate is not null)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.DuplicateFeeHeadOverride)
				.WithData("feeHeadId", duplicate.Key);
		}

		foreach (var feeHeadOverride in overrides)
		{
			if (feeHeadOverride.OverrideMode != FeeHeadOverrideMode.CustomRule)
			{
				continue;
			}

			if (feeHeadOverride.LateFeeRule is null)
			{
				throw new BusinessException(CollectionPolicyErrorCodes.CustomRuleRequiresLateFeeRule)
					.WithData("feeHeadId", feeHeadOverride.FeeHeadId);
			}

			if (feeHeadOverride.LateFeeRule.LateFeeType == LateFeeCalculationType.Slab)
			{
				CollectionPolicySlabValidator.EnsureNoOverlapsOrGaps(feeHeadOverride.LateFeeRule.EscalationRules);
			}
		}
	}

	private static string SerializeFeeHeadOverrides(IReadOnlyList<CollectionPolicyFeeHeadOverride>? overrides) =>
		overrides is null || overrides.Count == 0 ? "[]" : JsonSerializer.Serialize(overrides);

	private static string SerializeGuids(IReadOnlyList<Guid>? values) =>
		values is null || values.Count == 0 ? "[]" : JsonSerializer.Serialize(values);

	private static IReadOnlyList<Guid> DeserializeGuids(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return Array.Empty<Guid>();
		}

		return JsonSerializer.Deserialize<List<Guid>>(json) ?? (IReadOnlyList<Guid>)Array.Empty<Guid>();
	}

	private static string SerializeEscalationRules(IReadOnlyList<CollectionPolicyEscalationRule>? rules) =>
		rules is null || rules.Count == 0 ? "[]" : JsonSerializer.Serialize(rules.OrderBy(r => r.StartDay).ToList());
}

/// <summary>
/// Groups the (many) fields Create/Update share, so the aggregate's public API isn't a
/// 25-parameter constructor call — mirrors the shape of ICollectionPolicyFormValues on
/// the frontend field-for-field.
/// </summary>
public sealed record CollectionPolicyUpdateModel(
	string PolicyName,
	string PolicyCode,
	string? Description,
	CollectionPolicyStatus Status,
	bool IsDefault,
	DateTime? EffectiveFrom,
	DateTime? EffectiveTo,
	bool EnableLateFee,
	int GracePeriodDays,
	GracePeriodType GracePeriodType,
	IReadOnlyList<Guid> LateFeeApplyOnFeeHeads,
	LateFeeCalculationType LateFeeType,
	decimal? FlatAmount,
	decimal? Percentage,
	PercentageBaseType PercentageBase,
	decimal? AmountPerDay,
	decimal? AmountPerWeek,
	decimal? AmountPerMonth,
	CompoundFrequencyType CompoundFrequency,
	IReadOnlyList<CollectionPolicyEscalationRule> EscalationRules,
	IReadOnlyList<CollectionPolicyFeeHeadOverride> FeeHeadOverrides,
	RoundingMethodType RoundingMethod,
	bool ConsiderAsIncome,
	Guid? LateFeeIncomeHeadId,
	decimal? MaximumLateFeeAmount,
	int? MaximumChargingDays,
	bool EnableWaiver,
	IReadOnlyList<Guid> WaiverApplyOnFeeHeads,
	decimal? MaximumWaiverAmount,
	decimal? MaximumWaiverPercentage,
	DateTime? WaiverExpiry,
	bool RequiresApproval,
	IReadOnlyList<Guid> ApprovalRoleIds,
	decimal? ApprovalThresholdAmount,
	bool AllowOnlineCollection);
