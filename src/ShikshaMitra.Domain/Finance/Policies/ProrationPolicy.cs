using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ProrationPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public string PolicyName { get; protected set; } = string.Empty;
	public string PolicyCode { get; protected set; } = string.Empty;
	public string Description { get; protected set; } = string.Empty;
	public ProrationPolicyStatus Status { get; protected set; }
	public bool IsDefault { get; protected set; }
	public int Version { get; protected set; }
	public ICollection<ProrationPolicyRule> Rules { get; protected set; } = new List<ProrationPolicyRule>();

	protected ProrationPolicy() { }

	public ProrationPolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		ProrationPolicyUpdateModel model) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 0;
		Update(model);
	}

	public void Update(ProrationPolicyUpdateModel model)
	{
		if (IsDefault && (!model.IsDefault || model.Status != ProrationPolicyStatus.Active))
			throw new BusinessException(ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if (Status == ProrationPolicyStatus.Archived && Version > 0)
			throw new BusinessException(ProrationPolicyErrorCodes.ArchivedPolicyNotEditable);
		if (AcademicYearId == Guid.Empty)
			throw new BusinessException(ProrationPolicyErrorCodes.AcademicYearRequired);
		if (string.IsNullOrWhiteSpace(model.PolicyName))
			throw new BusinessException(ProrationPolicyErrorCodes.NameRequired);
		if (string.IsNullOrWhiteSpace(model.PolicyCode))
			throw new BusinessException(ProrationPolicyErrorCodes.CodeRequired);
		if (model.Status == ProrationPolicyStatus.Archived)
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidStatus);
		if (model.IsDefault && model.Status != ProrationPolicyStatus.Active)
			throw new BusinessException(ProrationPolicyErrorCodes.DefaultPolicyMustBeActive);

		ValidateRules(model.Rules);
		PolicyName = model.PolicyName.Trim();
		PolicyCode = model.PolicyCode.Trim().ToUpperInvariant();
		Description = model.Description?.Trim() ?? string.Empty;
		Status = model.Status;
		IsDefault = model.IsDefault;
		Rules.Clear();
		foreach (var rule in model.Rules)
		{
			Rules.Add(new ProrationPolicyRule(
				rule.Id, TenantId, Id, rule.FeeHeadId, rule.RuleType,
				rule.RoundingMode, rule.MinimumChargeDays, rule.CutoffDay, rule.AfterCutoffAction,
				rule.ApplyOnAdmission, rule.ApplyOnEarlyWithdrawal, rule.EarlyWithdrawalRuleType,
				rule.EarlyWithdrawalMinimumChargeDays, rule.EarlyWithdrawalCutoffDay, rule.EarlyWithdrawalBeforeCutoffAction));
		}
		Version++;
	}

	public void Activate()
	{
		if (Status == ProrationPolicyStatus.Archived)
			throw new BusinessException(ProrationPolicyErrorCodes.ArchivedPolicyNotEditable);
		ValidateRules(Rules.Select(x => x.ToModel()).ToList());
		Status = ProrationPolicyStatus.Active;
		Version++;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != ProrationPolicyStatus.Active)
			throw new BusinessException(ProrationPolicyErrorCodes.DefaultPolicyMustBeActive);
		if (IsDefault == isDefault) return;
		IsDefault = isDefault;
		Version++;
	}

	public void Archive()
	{
		if (IsDefault)
			throw new BusinessException(ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		Status = ProrationPolicyStatus.Archived;
		IsDefault = false;
		Version++;
	}

	public void Unarchive()
	{
		if (Status != ProrationPolicyStatus.Archived) return;
		Status = ProrationPolicyStatus.Draft;
		IsDefault = false;
		Version++;
	}

	public ProrationPolicySnapshot ToSnapshot() => new(
		Id, AcademicYearId, PolicyName, PolicyCode, Status, IsDefault, Version,
		Rules.OrderBy(x => x.FeeHeadId).Select(x => x.ToSnapshot()).ToList());

	private static void ValidateRules(IReadOnlyCollection<ProrationPolicyRuleModel> rules)
	{
		if (rules.Count == 0)
			throw new BusinessException(ProrationPolicyErrorCodes.FeeHeadRequired);
		if (rules.Any(x => x.FeeHeadId == Guid.Empty) || rules.GroupBy(x => x.FeeHeadId).Any(x => x.Count() > 1))
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
		foreach (var rule in rules)
		{
			if (!Enum.IsDefined(rule.RuleType) || !Enum.IsDefined(rule.RoundingMode) ||
				(rule.EarlyWithdrawalRuleType.HasValue && !Enum.IsDefined(rule.EarlyWithdrawalRuleType.Value)))
				throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
			if (rule.ApplyOnAdmission && rule.RuleType == ProrationRuleType.Daily && rule.MinimumChargeDays is < 0 or > 366)
				throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
			if (rule.ApplyOnAdmission && rule.RuleType == ProrationRuleType.MonthlyCutoff &&
				(rule.CutoffDay is null or < 1 or > 31 || !rule.AfterCutoffAction.HasValue || !Enum.IsDefined(rule.AfterCutoffAction.Value)))
				throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
			if (rule.ApplyOnEarlyWithdrawal && rule.EarlyWithdrawalRuleType == ProrationRuleType.Daily && rule.EarlyWithdrawalMinimumChargeDays is < 0 or > 366)
				throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
			if (rule.ApplyOnEarlyWithdrawal && rule.EarlyWithdrawalRuleType == ProrationRuleType.MonthlyCutoff &&
				(rule.EarlyWithdrawalCutoffDay is null or < 1 or > 31 || !rule.EarlyWithdrawalBeforeCutoffAction.HasValue || !Enum.IsDefined(rule.EarlyWithdrawalBeforeCutoffAction.Value)))
				throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
		}
	}
}

public class ProrationPolicyRule : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid ProrationPolicyId { get; protected set; }
	public Guid FeeHeadId { get; protected set; }
	public ProrationRuleType RuleType { get; protected set; }
	public ProrationRoundingMode RoundingMode { get; protected set; }
	public int? MinimumChargeDays { get; protected set; }
	public int? CutoffDay { get; protected set; }
	public ProrationAfterCutoffAction? AfterCutoffAction { get; protected set; }

	public bool ApplyOnAdmission { get; protected set; } = true;
	public bool ApplyOnEarlyWithdrawal { get; protected set; } = true;
	public ProrationRuleType? EarlyWithdrawalRuleType { get; protected set; }
	public int? EarlyWithdrawalMinimumChargeDays { get; protected set; }
	public int? EarlyWithdrawalCutoffDay { get; protected set; }
	public ProrationAfterCutoffAction? EarlyWithdrawalBeforeCutoffAction { get; protected set; }

	protected ProrationPolicyRule() { }

	public ProrationPolicyRule(
		Guid id,
		Guid? tenantId,
		Guid prorationPolicyId,
		Guid feeHeadId,
		ProrationRuleType ruleType,
		ProrationRoundingMode roundingMode,
		int? minimumChargeDays,
		int? cutoffDay,
		ProrationAfterCutoffAction? afterCutoffAction,
		bool applyOnAdmission = true, bool applyOnEarlyWithdrawal = true,
		ProrationRuleType? earlyWithdrawalRuleType = null, int? earlyWithdrawalMinimumChargeDays = null,
		int? earlyWithdrawalCutoffDay = null, ProrationAfterCutoffAction? earlyWithdrawalBeforeCutoffAction = null) : base(id)
	{
		TenantId = tenantId;
		ProrationPolicyId = prorationPolicyId;
		FeeHeadId = feeHeadId;
		ApplyOnAdmission = applyOnAdmission;
		ApplyOnEarlyWithdrawal = applyOnEarlyWithdrawal;
		EarlyWithdrawalRuleType = earlyWithdrawalRuleType;
		EarlyWithdrawalMinimumChargeDays = earlyWithdrawalRuleType == ProrationRuleType.Daily ? earlyWithdrawalMinimumChargeDays ?? 0 : null;
		EarlyWithdrawalCutoffDay = earlyWithdrawalRuleType == ProrationRuleType.MonthlyCutoff ? earlyWithdrawalCutoffDay : null;
		EarlyWithdrawalBeforeCutoffAction = earlyWithdrawalRuleType == ProrationRuleType.MonthlyCutoff ? earlyWithdrawalBeforeCutoffAction : null;
		RuleType = ruleType;
		RoundingMode = ProrationRoundingMode.RoundUpRupee;
		MinimumChargeDays = ruleType == ProrationRuleType.Daily ? minimumChargeDays ?? 0 : null;
		CutoffDay = ruleType == ProrationRuleType.MonthlyCutoff ? cutoffDay : null;
		AfterCutoffAction = ruleType == ProrationRuleType.MonthlyCutoff
			? afterCutoffAction ?? ProrationAfterCutoffAction.NoCharge
			: null;
	}

	public ProrationPolicyRuleModel ToModel() => new(
		Id, FeeHeadId, RuleType, RoundingMode, MinimumChargeDays, CutoffDay, AfterCutoffAction,
		ApplyOnAdmission, ApplyOnEarlyWithdrawal, EarlyWithdrawalRuleType,
		EarlyWithdrawalMinimumChargeDays, EarlyWithdrawalCutoffDay, EarlyWithdrawalBeforeCutoffAction);

	public ProrationPolicyRuleSnapshot ToSnapshot() => new(
		FeeHeadId, RuleType, RoundingMode, MinimumChargeDays, CutoffDay, AfterCutoffAction,
		ApplyOnAdmission, ApplyOnEarlyWithdrawal, EarlyWithdrawalRuleType,
		EarlyWithdrawalMinimumChargeDays, EarlyWithdrawalCutoffDay, EarlyWithdrawalBeforeCutoffAction);
}

public sealed record ProrationPolicyUpdateModel(
	string PolicyName,
	string PolicyCode,
	string? Description,
	ProrationPolicyStatus Status,
	bool IsDefault,
	IReadOnlyCollection<ProrationPolicyRuleModel> Rules);

public sealed record ProrationPolicyRuleModel(
	Guid Id,
	Guid FeeHeadId,
	ProrationRuleType RuleType,
	ProrationRoundingMode RoundingMode,
	int? MinimumChargeDays,
	int? CutoffDay,
	ProrationAfterCutoffAction? AfterCutoffAction,
	bool ApplyOnAdmission = true,
	bool ApplyOnEarlyWithdrawal = true,
	ProrationRuleType? EarlyWithdrawalRuleType = null,
	int? EarlyWithdrawalMinimumChargeDays = null,
	int? EarlyWithdrawalCutoffDay = null,
	ProrationAfterCutoffAction? EarlyWithdrawalBeforeCutoffAction = null);

public sealed record ProrationPolicySnapshot(
	Guid PolicyId,
	Guid AcademicYearId,
	string PolicyName,
	string PolicyCode,
	ProrationPolicyStatus Status,
	bool IsDefault,
	int Version,
	IReadOnlyList<ProrationPolicyRuleSnapshot> Rules);

public sealed record ProrationPolicyRuleSnapshot(
	Guid FeeHeadId,
	ProrationRuleType RuleType,
	ProrationRoundingMode RoundingMode,
	int? MinimumChargeDays,
	int? CutoffDay,
	ProrationAfterCutoffAction? AfterCutoffAction,
	bool ApplyOnAdmission = true,
	bool ApplyOnEarlyWithdrawal = true,
	ProrationRuleType? EarlyWithdrawalRuleType = null,
	int? EarlyWithdrawalMinimumChargeDays = null,
	int? EarlyWithdrawalCutoffDay = null,
	ProrationAfterCutoffAction? EarlyWithdrawalBeforeCutoffAction = null);
