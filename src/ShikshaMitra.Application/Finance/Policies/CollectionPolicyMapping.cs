using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class CollectionPolicyMapping
{
	public static CollectionPolicyStatus ParseStatus(string value) => Normalize(value) switch
	{
		"draft" => CollectionPolicyStatus.Draft,
		"active" => CollectionPolicyStatus.Active,
		"archived" => CollectionPolicyStatus.Archived,
		_ => throw new BusinessException(CollectionPolicyErrorCodes.InvalidStatus).WithData("field", "status")
	};

	public static string MapStatus(CollectionPolicyStatus value) => value switch
	{
		CollectionPolicyStatus.Draft => "Draft",
		CollectionPolicyStatus.Archived => "Archived",
		_ => "Active"
	};

	public static LateFeeCalculationType ParseLateFeeType(string value) => Normalize(value) switch
	{
		"flat" => LateFeeCalculationType.Flat,
		"percentage" => LateFeeCalculationType.Percentage,
		"perday" => LateFeeCalculationType.PerDay,
		"perweek" => LateFeeCalculationType.PerWeek,
		"permonth" => LateFeeCalculationType.PerMonth,
		"compound" => LateFeeCalculationType.Compound,
		"slab" => LateFeeCalculationType.Slab,
		_ => throw InvalidEnum("lateFeeType")
	};

	public static string MapLateFeeType(LateFeeCalculationType value) => value.ToString();

	public static PercentageBaseType ParsePercentageBase(string value) => Normalize(value) switch
	{
		"originalfee" => PercentageBaseType.OriginalFee,
		"remainingbalance" => PercentageBaseType.RemainingBalance,
		"outstandingamount" => PercentageBaseType.OutstandingAmount,
		_ => throw InvalidEnum("percentageBase")
	};

	public static string MapPercentageBase(PercentageBaseType value) => value.ToString();

	public static CompoundFrequencyType ParseCompoundFrequency(string value) => Normalize(value) switch
	{
		"weekly" => CompoundFrequencyType.Weekly,
		"monthly" => CompoundFrequencyType.Monthly,
		"daily" => CompoundFrequencyType.Daily,
		_ => throw InvalidEnum("compoundFrequency")
	};

	public static string MapCompoundFrequency(CompoundFrequencyType value) => value.ToString();

	public static RoundingMethodType ParseRoundingMethod(string value) => Normalize(value) switch
	{
		"roundup" => RoundingMethodType.RoundUp,
		"rounddown" => RoundingMethodType.RoundDown,
		"nearestrupee" => RoundingMethodType.NearestRupee,
		"none" => RoundingMethodType.None,
		_ => throw InvalidEnum("roundingMethod")
	};

	public static string MapRoundingMethod(RoundingMethodType value) => value.ToString();

	public static GracePeriodType ParseGracePeriodType(string value) => Normalize(value) switch
	{
		"calendar" => GracePeriodType.Calendar,
		"workingday" => GracePeriodType.WorkingDay,
		_ => throw InvalidEnum("gracePeriodType")
	};

	public static string MapGracePeriodType(GracePeriodType value) => value.ToString();

	public static EscalationCalculationType ParseEscalationCalculationType(string value) => Normalize(value) switch
	{
		"flat" => EscalationCalculationType.Flat,
		"percentage" => EscalationCalculationType.Percentage,
		_ => throw InvalidEnum("escalationCalculationType")
	};

	public static string MapEscalationCalculationType(EscalationCalculationType value) => value.ToString();

	public static FeeHeadOverrideMode ParseFeeHeadOverrideMode(string value) => Normalize(value) switch
	{
		"inherit" => FeeHeadOverrideMode.Inherit,
		"nolatefee" => FeeHeadOverrideMode.NoLateFee,
		"customrule" => FeeHeadOverrideMode.CustomRule,
		_ => throw InvalidEnum("feeHeadOverrideMode")
	};

	public static string MapFeeHeadOverrideMode(FeeHeadOverrideMode value) => value.ToString();

	private static List<CollectionPolicyEscalationRule> ToEscalationRules(List<CollectionPolicyEscalationRuleDto>? rules) =>
		(rules ?? new List<CollectionPolicyEscalationRuleDto>())
			.Select(r => new CollectionPolicyEscalationRule(
				r.StartDay, r.EndDay, ParseEscalationCalculationType(r.CalculationType), r.Value, r.MaxFee))
			.ToList();

	private static List<CollectionPolicyEscalationRuleDto> ToEscalationRuleDtos(IReadOnlyList<CollectionPolicyEscalationRule> rules) =>
		rules.Select(r => new CollectionPolicyEscalationRuleDto
		{
			StartDay = r.StartDay,
			EndDay = r.EndDay,
			CalculationType = MapEscalationCalculationType(r.CalculationType),
			Value = r.Value,
			MaxFee = r.MaxFee
		}).ToList();

	private static LateFeeRuleOverride ToLateFeeRuleOverride(LateFeeRuleOverrideDto dto) => new(
		ParseLateFeeType(dto.LateFeeType),
		dto.FlatAmount,
		dto.Percentage,
		ParsePercentageBase(dto.PercentageBase),
		dto.AmountPerDay,
		dto.AmountPerWeek,
		dto.AmountPerMonth,
		ParseCompoundFrequency(dto.CompoundFrequency),
		ToEscalationRules(dto.EscalationRules),
		ParseRoundingMethod(dto.RoundingMethod),
		dto.MaximumLateFeeAmount,
		dto.MaximumChargingDays);

	private static LateFeeRuleOverrideDto ToLateFeeRuleOverrideDto(LateFeeRuleOverride rule) => new()
	{
		LateFeeType = MapLateFeeType(rule.LateFeeType),
		FlatAmount = rule.FlatAmount,
		Percentage = rule.Percentage,
		PercentageBase = MapPercentageBase(rule.PercentageBase),
		AmountPerDay = rule.AmountPerDay,
		AmountPerWeek = rule.AmountPerWeek,
		AmountPerMonth = rule.AmountPerMonth,
		CompoundFrequency = MapCompoundFrequency(rule.CompoundFrequency),
		EscalationRules = ToEscalationRuleDtos(rule.EscalationRules),
		RoundingMethod = MapRoundingMethod(rule.RoundingMethod),
		MaximumLateFeeAmount = rule.MaximumLateFeeAmount,
		MaximumChargingDays = rule.MaximumChargingDays
	};

	private static List<CollectionPolicyFeeHeadOverride> ToFeeHeadOverrides(List<CollectionPolicyFeeHeadOverrideDto>? overrides) =>
		(overrides ?? new List<CollectionPolicyFeeHeadOverrideDto>())
			.Select(o => new CollectionPolicyFeeHeadOverride(
				o.FeeHeadId,
				ParseFeeHeadOverrideMode(o.OverrideMode),
				o.LateFeeRule is null ? null : ToLateFeeRuleOverride(o.LateFeeRule)))
			.ToList();

	private static List<CollectionPolicyFeeHeadOverrideDto> ToFeeHeadOverrideDtos(IReadOnlyList<CollectionPolicyFeeHeadOverride> overrides) =>
		overrides.Select(o => new CollectionPolicyFeeHeadOverrideDto
		{
			FeeHeadId = o.FeeHeadId,
			OverrideMode = MapFeeHeadOverrideMode(o.OverrideMode),
			LateFeeRule = o.LateFeeRule is null ? null : ToLateFeeRuleOverrideDto(o.LateFeeRule)
		}).ToList();

	public static CollectionPolicyUpdateModel ToUpdateModel(
		CreateUpdateCollectionPolicyDto input,
		DateTime effectiveFrom,
		DateTime effectiveTo) =>
		new(
			input.PolicyName,
			input.PolicyCode,
			input.Description,
			ParseStatus(input.Status),
			input.IsDefault,
			effectiveFrom,
			effectiveTo,
			input.EnableLateFee,
			input.GracePeriodDays,
			ParseGracePeriodType(input.GracePeriodType),
			input.LateFeeApplyOnFeeHeads,
			ParseLateFeeType(input.LateFeeType),
			input.FlatAmount,
			input.Percentage,
			ParsePercentageBase(input.PercentageBase),
			input.AmountPerDay,
			input.AmountPerWeek,
			input.AmountPerMonth,
			ParseCompoundFrequency(input.CompoundFrequency),
			ToEscalationRules(input.EscalationRules),
			ToFeeHeadOverrides(input.FeeHeadOverrides),
			ParseRoundingMethod(input.RoundingMethod),
			input.ConsiderAsIncome,
			input.LateFeeIncomeHeadId,
			input.MaximumLateFeeAmount,
			input.MaximumChargingDays,
			input.EnableWaiver,
			input.WaiverApplyOnFeeHeads,
			input.MaximumWaiverAmount,
			input.MaximumWaiverPercentage,
			input.WaiverExpiry,
			input.RequiresApproval,
			input.ApprovalRoleIds,
			input.ApprovalThresholdAmount,
			input.AllowOnlineCollection);

	public static CollectionPolicyDto ToDto(CollectionPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		PolicyName = entity.PolicyName,
		PolicyCode = entity.PolicyCode,
		Description = entity.Description,
		Status = MapStatus(entity.Status),
		IsDefault = entity.IsDefault,
		EnableLateFee = entity.EnableLateFee,
		GracePeriodDays = entity.GracePeriodDays,
		GracePeriodType = MapGracePeriodType(entity.GracePeriodType),
		LateFeeApplyOnFeeHeads = entity.GetLateFeeApplyOnFeeHeads().ToList(),
		LateFeeType = MapLateFeeType(entity.LateFeeType),
		FlatAmount = entity.FlatAmount,
		Percentage = entity.Percentage,
		PercentageBase = MapPercentageBase(entity.PercentageBase),
		AmountPerDay = entity.AmountPerDay,
		AmountPerWeek = entity.AmountPerWeek,
		AmountPerMonth = entity.AmountPerMonth,
		CompoundFrequency = MapCompoundFrequency(entity.CompoundFrequency),
		EscalationRules = ToEscalationRuleDtos(entity.GetEscalationRules()),
		FeeHeadOverrides = ToFeeHeadOverrideDtos(entity.GetFeeHeadOverrides()),
		RoundingMethod = MapRoundingMethod(entity.RoundingMethod),
		ConsiderAsIncome = entity.ConsiderAsIncome,
		LateFeeIncomeHeadId = entity.LateFeeIncomeHeadId,
		MaximumLateFeeAmount = entity.MaximumLateFeeAmount,
		MaximumChargingDays = entity.MaximumChargingDays,
		EnableWaiver = entity.EnableWaiver,
		WaiverApplyOnFeeHeads = entity.GetWaiverApplyOnFeeHeads().ToList(),
		MaximumWaiverAmount = entity.MaximumWaiverAmount,
		MaximumWaiverPercentage = entity.MaximumWaiverPercentage,
		WaiverExpiry = entity.WaiverExpiry,
		RequiresApproval = entity.RequiresApproval,
		ApprovalRoleIds = entity.GetApprovalRoleIds().ToList(),
		ApprovalThresholdAmount = entity.ApprovalThresholdAmount,
		AllowOnlineCollection = entity.AllowOnlineCollection,
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static string ToSnapshotJson(CollectionPolicyDto dto) => JsonSerializer.Serialize(dto);

	private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

	private static BusinessException InvalidEnum(string field) =>
		new BusinessException(CollectionPolicyErrorCodes.InvalidLateFeeRule).WithData("field", field);
}
