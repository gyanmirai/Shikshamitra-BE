using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ProrationPolicyRuleDto
{
	public Guid FeeHeadId { get; set; }
	public string? FeeHeadName { get; set; }
	public string RuleType { get; set; } = "FullCharge";
	public string RoundingMode { get; set; } = "RoundUpRupee";
	public int? MinimumChargeDays { get; set; }
	public int? CutoffDay { get; set; }
	public string? AfterCutoffAction { get; set; }
	public bool ApplyOnAdmission { get; set; } = true;
	public bool ApplyOnEarlyWithdrawal { get; set; } = true;
	public string? EarlyWithdrawalRuleType { get; set; }
	public int? EarlyWithdrawalMinimumChargeDays { get; set; }
	public int? EarlyWithdrawalCutoffDay { get; set; }
	public string? EarlyWithdrawalBeforeCutoffAction { get; set; }
}

public class ProrationPolicyDto : EntityDto<Guid>, ICurrentPolicyDto
{
	public Guid? CurrentVersionId { get; set; }
	public int CurrentVersionNumber => Version;
	public bool IsAttachable => Status == "Active" && CurrentVersionId.HasValue;
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = "Draft";
	public string PolicyType { get; set; } = "proration";
	public bool IsDefault { get; set; }
	public int Version { get; set; }
	public bool IsUsedInDownstream { get; set; }
	public List<ProrationPolicyRuleDto> FeeHeadRules { get; set; } = [];
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class CreateUpdateProrationPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string? PolicyCode { get; set; }
	public string? Description { get; set; }
	public string Status { get; set; } = "Draft";
	public bool IsDefault { get; set; }
	public List<ProrationPolicyRuleDto> FeeHeadRules { get; set; } = [];
	public string? ChangeReason { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class CreateUpdateProrationPolicyDtoValidator : AbstractValidator<CreateUpdateProrationPolicyDto>
{
	public CreateUpdateProrationPolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.PolicyName).NotEmpty().MinimumLength(3).MaximumLength(128);
		RuleFor(x => x.PolicyCode).MaximumLength(32);
		RuleFor(x => x.Description).MaximumLength(500);
		RuleFor(x => x.ChangeReason).MaximumLength(500);
		RuleFor(x => x.Status).Must(x => EnumValue(x, "Draft", "Active"))
			.WithMessage("Status must be Draft or Active. Use the archive action for Archived.");
		RuleFor(x => x).Must(x => !x.IsDefault || EnumValue(x.Status, "Active"))
			.WithMessage("Only an Active proration policy can be the default.");
		RuleFor(x => x.FeeHeadRules).NotEmpty();
		RuleFor(x => x.FeeHeadRules)
			.Must(rules => rules.Any(rule =>
				(rule.ApplyOnAdmission && EnumValue(rule.RuleType, "Daily", "MonthlyCutoff", "FullMonthCharge")) ||
				(rule.ApplyOnEarlyWithdrawal && EnumValue(rule.EarlyWithdrawalRuleType ?? rule.RuleType, "Daily", "MonthlyCutoff", "FullMonthCharge"))))
			.WithMessage("At least one Fee Head must use Daily, MonthlyCutoff or FullMonthCharge proration.");
		RuleFor(x => x.FeeHeadRules).Must(x => x.Select(r => r.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("A Fee Head can have only one proration rule.");
		RuleForEach(x => x.FeeHeadRules).SetValidator(new ProrationPolicyRuleDtoValidator());
	}

	private static bool EnumValue(string? value, params string[] allowed) =>
		allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public class ProrationPolicyRuleDtoValidator : AbstractValidator<ProrationPolicyRuleDto>
{
	public ProrationPolicyRuleDtoValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.RuleType).Must(x => EnumValue(x, "FullCharge", "Daily", "MonthlyCutoff", "FullMonthCharge"));
		RuleFor(x => x.RoundingMode).Must(x => EnumValue(x, "Currency", "NearestRupee", "RoundUpRupee", "RoundDownRupee"));
		RuleFor(x => x.MinimumChargeDays).InclusiveBetween(0, 366)
			.When(x => x.ApplyOnAdmission && EnumValue(x.RuleType, "Daily"));
		RuleFor(x => x.CutoffDay).NotNull().InclusiveBetween(1, 31)
			.When(x => x.ApplyOnAdmission && EnumValue(x.RuleType, "MonthlyCutoff"));
		RuleFor(x => x.AfterCutoffAction).NotNull().Must(x => EnumValue(x, "NoCharge", "HalfCharge"))
			.When(x => x.ApplyOnAdmission && EnumValue(x.RuleType, "MonthlyCutoff"));
		RuleFor(x => x.EarlyWithdrawalRuleType).Must(x => x == null || EnumValue(x, "FullCharge", "Daily", "MonthlyCutoff", "FullMonthCharge"));
		RuleFor(x => x.EarlyWithdrawalMinimumChargeDays).InclusiveBetween(0, 366)
			.When(x => x.ApplyOnEarlyWithdrawal && EnumValue(x.EarlyWithdrawalRuleType, "Daily"));
		RuleFor(x => x.EarlyWithdrawalCutoffDay).NotNull().InclusiveBetween(1, 31)
			.When(x => x.ApplyOnEarlyWithdrawal && EnumValue(x.EarlyWithdrawalRuleType, "MonthlyCutoff"));
		RuleFor(x => x.EarlyWithdrawalBeforeCutoffAction).NotNull().Must(x => EnumValue(x, "NoCharge", "HalfCharge"))
			.When(x => x.ApplyOnEarlyWithdrawal && EnumValue(x.EarlyWithdrawalRuleType, "MonthlyCutoff"));
	}

	private static bool EnumValue(string? value, params string[] allowed) =>
		allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public class ProrationPolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public bool IncludeArchived { get; set; }
	public Guid AcademicYearId { get; set; }
	public string? Status { get; set; }
	public string? Search { get; set; }
	public string? PolicyNameFilter { get; set; }
	public string? PolicyNameOperator { get; set; }
	public string? RuleCountOperator { get; set; }
	public int? RuleCountValue { get; set; }
	public int? RuleCountMax { get; set; }
	public string? UpdatedAtOperator { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public DateTime? UpdatedAtEnd { get; set; }
}

public class ProrationPolicyListRequestDtoValidator : AbstractValidator<ProrationPolicyListRequestDto>
{
	public ProrationPolicyListRequestDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.SkipCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaxResultCount).InclusiveBetween(1, 100);
		RuleFor(x => x.Status).Must(x => x is null ||
			string.Equals(x, "Draft", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(x, "Active", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(x, "Archived", StringComparison.OrdinalIgnoreCase));
	}
}

public class ProrationPolicyCloneRequestDto
{
	public string? Name { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
}

public class ProrationPolicySimulateRequestDto
{
	public Guid FeeHeadId { get; set; }
	public decimal ScheduledAmount { get; set; }
	public DateTime CoverageStartDate { get; set; }
	public DateTime CoverageEndDate { get; set; }
	public DateTime ChargeStartDate { get; set; }
	public DateTime ChargeEndDate { get; set; }
}

public class ProrationPolicySimulateRequestDtoValidator : AbstractValidator<ProrationPolicySimulateRequestDto>
{
	public ProrationPolicySimulateRequestDtoValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.ScheduledAmount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.CoverageEndDate).GreaterThanOrEqualTo(x => x.CoverageStartDate);
		RuleFor(x => x.ChargeEndDate).GreaterThanOrEqualTo(x => x.ChargeStartDate);
	}
}

public class ProrationCalculationResultDto
{
	public Guid FeeHeadId { get; set; }
	public decimal ScheduledAmount { get; set; }
	public decimal ProratedAmount { get; set; }
	public decimal ReductionAmount { get; set; }
	public decimal RawAmount { get; set; }
	public decimal Factor { get; set; }
	public DateTime CoverageStartDate { get; set; }
	public DateTime CoverageEndDate { get; set; }
	public DateTime? EligibleStartDate { get; set; }
	public DateTime? EligibleEndDate { get; set; }
	public int CoverageDays { get; set; }
	public int ActualEligibleDays { get; set; }
	public int ChargedDays { get; set; }
	public string RuleType { get; set; } = string.Empty;
	public string RoundingMode { get; set; } = string.Empty;
	public string Explanation { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public int PolicyVersion { get; set; }
}

public class ProrationPolicyImpactDto
{
	public Guid PolicyId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public int CurrentVersion { get; set; }
	public int AttachedFeeStructureCount { get; set; }
	public int StudentAssignmentCount { get; set; }
	public bool ChangeReasonRequired { get; set; }
}
