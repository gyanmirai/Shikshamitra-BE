using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class ProrationPolicyMapping
{
	public static ProrationPolicyStatus ParseStatus(string? value) => Normalize(value) switch
	{
		"draft" => ProrationPolicyStatus.Draft,
		"active" => ProrationPolicyStatus.Active,
		_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidStatus)
	};

	public static ProrationRuleType ParseRuleType(string? value) => Normalize(value) switch
	{
		"fullcharge" or "noproration" => ProrationRuleType.FullCharge,
		"fullmonthcharge" => ProrationRuleType.FullMonthCharge,
		"daily" or "dailyproration" => ProrationRuleType.Daily,
		"monthlycutoff" or "monthlyproration" => ProrationRuleType.MonthlyCutoff,
		_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule)
	};

	public static ProrationRoundingMode ParseRounding(string? value) => Normalize(value) switch
	{
		"currency" or "noroundoff" => ProrationRoundingMode.Currency,
		"nearestrupee" => ProrationRoundingMode.NearestRupee,
		"rounduprupee" or "roundup" => ProrationRoundingMode.RoundUpRupee,
		"rounddownrupee" or "rounddown" => ProrationRoundingMode.RoundDownRupee,
		_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule)
	};

	public static ProrationAfterCutoffAction? ParseAfterCutoff(string? value, ProrationRuleType ruleType)
	{
		if (ruleType != ProrationRuleType.MonthlyCutoff)
			return null;
		return Normalize(value) switch
		{
			"nocharge" or "chargenextmonth" => ProrationAfterCutoffAction.NoCharge,
			"halfcharge" or "halfmonthrule" => ProrationAfterCutoffAction.HalfCharge,
			_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule)
		};
	}

	public static ProrationPolicyUpdateModel ToUpdateModel(
		CreateUpdateProrationPolicyDto input,
		string policyCode,
		Func<Guid> newId) => new(
		input.PolicyName,
		policyCode,
		input.Description,
		ParseStatus(input.Status),
		input.IsDefault,
		input.FeeHeadRules.Select(x =>
		{
			var type = ParseRuleType(x.RuleType);
			return new ProrationPolicyRuleModel(
				newId(), x.FeeHeadId, type, ParseRounding(x.RoundingMode),
				x.MinimumChargeDays, x.CutoffDay, x.ApplyOnAdmission ? ParseAfterCutoff(x.AfterCutoffAction, type) : null,
				x.ApplyOnAdmission, x.ApplyOnEarlyWithdrawal,
				x.EarlyWithdrawalRuleType == null ? null : ParseRuleType(x.EarlyWithdrawalRuleType),
				x.EarlyWithdrawalMinimumChargeDays, x.EarlyWithdrawalCutoffDay,
				x.ApplyOnEarlyWithdrawal && x.EarlyWithdrawalRuleType != null ? ParseAfterCutoff(x.EarlyWithdrawalBeforeCutoffAction, ParseRuleType(x.EarlyWithdrawalRuleType)) : null);
		}).ToList());

	public static ProrationPolicyDto ToDto(
		ProrationPolicy entity,
		IReadOnlyDictionary<Guid, string>? feeHeadNames = null) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		PolicyName = entity.PolicyName,
		PolicyCode = entity.PolicyCode,
		Description = entity.Description,
		Status = entity.Status.ToString(),
		IsDefault = entity.IsDefault,
		Version = entity.Version,
		FeeHeadRules = entity.Rules.OrderBy(x => x.FeeHeadId).Select(x => new ProrationPolicyRuleDto
		{
			FeeHeadId = x.FeeHeadId,
			FeeHeadName = feeHeadNames?.GetValueOrDefault(x.FeeHeadId),
			RuleType = x.RuleType.ToString(),
			RoundingMode = x.RoundingMode.ToString(),
			MinimumChargeDays = x.MinimumChargeDays,
			CutoffDay = x.CutoffDay,
			AfterCutoffAction = x.AfterCutoffAction?.ToString(),
			ApplyOnAdmission = x.ApplyOnAdmission,
			ApplyOnEarlyWithdrawal = x.ApplyOnEarlyWithdrawal,
			EarlyWithdrawalRuleType = x.EarlyWithdrawalRuleType?.ToString(),
			EarlyWithdrawalMinimumChargeDays = x.EarlyWithdrawalMinimumChargeDays,
			EarlyWithdrawalCutoffDay = x.EarlyWithdrawalCutoffDay,
			EarlyWithdrawalBeforeCutoffAction = x.EarlyWithdrawalBeforeCutoffAction?.ToString()
		}).ToList(),
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId,
		ConcurrencyStamp = entity.ConcurrencyStamp
	};

	public static string ToSnapshotJson(ProrationPolicy entity) => JsonSerializer.Serialize(entity.ToSnapshot());

	private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
