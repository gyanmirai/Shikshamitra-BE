using System;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class ConcessionPolicyMapping
{
	public static ConcessionPolicyStatus ParseStatus(string? value) => Normalize(value) switch
	{
		"draft" => ConcessionPolicyStatus.Draft,
		"active" => ConcessionPolicyStatus.Active,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidStatus)
	};

	public static ConcessionValueSource ParseValueSource(string? value) => Normalize(value) switch
	{
		"policyfixed" => ConcessionValueSource.PolicyFixed,
		"requestenteredwithinlimits" => ConcessionValueSource.RequestEnteredWithinLimits,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
	};

	public static ConcessionCalculationMethod ParseCalculationMethod(string? value) => Normalize(value) switch
	{
		"percentage" => ConcessionCalculationMethod.Percentage,
		"fixedamount" => ConcessionCalculationMethod.FixedAmount,
		"fullwaiver" => ConcessionCalculationMethod.FullWaiver,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
	};

	public static ConcessionApplyOn ParseApplyOn(string? value) => Normalize(value) switch
	{
		"currentinstallment" => ConcessionApplyOn.CurrentInstallment,
		"futureinstallments" => ConcessionApplyOn.FutureInstallments,
		"alloutstanding" => ConcessionApplyOn.AllOutstanding,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
	};

	public static RoundingMethodType ParseRounding(string? value) => Normalize(value) switch
	{
		"roundup" => RoundingMethodType.RoundUp,
		"rounddown" => RoundingMethodType.RoundDown,
		"nearestrupee" => RoundingMethodType.NearestRupee,
		"none" => RoundingMethodType.None,
		_ => throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule)
	};

	public static DiscountBasis? ParseRequestedBasis(string? value) => Normalize(value) switch
	{
		"percentage" => DiscountBasis.Percentage,
		"fixedamount" or "flat" => DiscountBasis.FlatAmount,
		_ => null
	};

	public static ConcessionPolicyUpdateModel ToUpdateModel(CreateUpdateConcessionPolicyDto input, string policyCode) => new(
		input.PolicyName,
		policyCode,
		input.Description,
		ParseStatus(input.Status),
		input.IsDefault,
		ParseValueSource(input.ValueSource),
		ParseCalculationMethod(input.CalculationMethod),
		input.PercentageValue,
		input.FixedAmount,
		input.MaximumPercentage,
		input.MaximumFixedAmount,
		ParseApplyOn(input.ApplyOn),
		ParseRounding(input.RoundingMethod));

	public static ConcessionPolicyDto ToDto(ConcessionPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		PolicyName = entity.PolicyName,
		PolicyCode = entity.PolicyCode,
		Description = entity.Description,
		Status = entity.Status.ToString(),
		IsDefault = entity.IsDefault,
		ValueSource = entity.ValueSource.ToString(),
		CalculationMethod = entity.CalculationMethod.ToString(),
		PercentageValue = entity.PercentageValue,
		FixedAmount = entity.FixedAmount,
		MaximumPercentage = entity.MaximumPercentage,
		MaximumFixedAmount = entity.MaximumFixedAmount,
		ApplyOn = entity.ApplyOn.ToString(),
		RoundingMethod = entity.RoundingMethod.ToString(),
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId,
		ConcurrencyStamp = entity.ConcurrencyStamp
	};

	public static string ToSnapshotJson(ConcessionPolicy entity) => JsonSerializer.Serialize(entity.ToSnapshot());

	private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
