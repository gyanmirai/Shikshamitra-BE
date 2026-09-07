using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionPolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = "Draft";
	public string PolicyType { get; set; } = "concession";
	public string ValueSource { get; set; } = "RequestEnteredWithinLimits";
	public string CalculationMethod { get; set; } = "Percentage";
	public decimal? PercentageValue { get; set; }
	public decimal? FixedAmount { get; set; }
	public decimal? MaximumPercentage { get; set; }
	public decimal? MaximumFixedAmount { get; set; }
	public string ApplyOn { get; set; } = "AllOutstanding";
	public string RoundingMethod { get; set; } = "None";
	public int Version { get; set; }
	public bool IsUsedInDownstream { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateConcessionPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string? PolicyCode { get; set; }
	public string? Description { get; set; }
	public string Status { get; set; } = "Draft";
	public string ValueSource { get; set; } = "RequestEnteredWithinLimits";
	public string CalculationMethod { get; set; } = "Percentage";
	public decimal? PercentageValue { get; set; }
	public decimal? FixedAmount { get; set; }
	public decimal? MaximumPercentage { get; set; }
	public decimal? MaximumFixedAmount { get; set; }
	public string ApplyOn { get; set; } = "AllOutstanding";
	public string RoundingMethod { get; set; } = "None";
}

public class CreateUpdateConcessionPolicyDtoValidator : AbstractValidator<CreateUpdateConcessionPolicyDto>
{
	public CreateUpdateConcessionPolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.PolicyName).NotEmpty().MinimumLength(3).MaximumLength(128);
		RuleFor(x => x.PolicyCode).MaximumLength(32);
		RuleFor(x => x.Description).MaximumLength(500);
		RuleFor(x => x.Status).Must(x =>
			string.Equals(x, "Draft", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(x, "Active", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Status must be Draft or Active. Use the archive action for Archived.");
		RuleFor(x => x.ValueSource).Must(x => EnumValue(x, "PolicyFixed", "RequestEnteredWithinLimits"));
		RuleFor(x => x.CalculationMethod).Must(x => EnumValue(x, "Percentage", "FixedAmount", "FullWaiver"));
		RuleFor(x => x.ApplyOn).Must(x => EnumValue(x, "AllOutstanding", "CurrentInstallment", "FutureInstallments"));
		RuleFor(x => x.RoundingMethod).Must(x => EnumValue(x, "None", "RoundUp", "RoundDown", "NearestRupee"));
		RuleFor(x => x).Must(x => !string.Equals(x.ValueSource, "RequestEnteredWithinLimits", StringComparison.OrdinalIgnoreCase) ||
			!string.Equals(x.CalculationMethod, "FullWaiver", StringComparison.OrdinalIgnoreCase))
			.WithMessage("FullWaiver must use PolicyFixed value source.");
		RuleFor(x => x.PercentageValue).InclusiveBetween(0.01m, 100m)
			.When(x => string.Equals(x.ValueSource, "PolicyFixed", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(x.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase));
		RuleFor(x => x.FixedAmount).GreaterThan(0)
			.When(x => string.Equals(x.ValueSource, "PolicyFixed", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(x.CalculationMethod, "FixedAmount", StringComparison.OrdinalIgnoreCase));
		RuleFor(x => x.MaximumPercentage).NotNull().InclusiveBetween(0.01m, 100m)
			.When(x => string.Equals(x.ValueSource, "RequestEnteredWithinLimits", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(x.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase));
		RuleFor(x => x.MaximumFixedAmount).NotNull().GreaterThan(0)
			.When(x => string.Equals(x.ValueSource, "RequestEnteredWithinLimits", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(x.CalculationMethod, "FixedAmount", StringComparison.OrdinalIgnoreCase));
	}

	private static bool EnumValue(string? value, params string[] allowed) =>
		allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public class ConcessionPolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Status { get; set; }
	public string? Search { get; set; }
}

public class ConcessionPolicyListRequestDtoValidator : AbstractValidator<ConcessionPolicyListRequestDto>
{
	public ConcessionPolicyListRequestDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.SkipCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaxResultCount).InclusiveBetween(1, 100);
		RuleFor(x => x.Status).Must(x => x is null || EnumValue(x, "Draft", "Active", "Archived"))
			.WithMessage("Status must be Draft, Active, or Archived.");
	}

	private static bool EnumValue(string value, params string[] allowed) =>
		allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public class ConcessionPolicyCloneRequestDto
{
	public string? Name { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
}

public class ConcessionPolicySimulateRequestDto
{
	public decimal EligibleBase { get; set; }
	public string? RequestedBasis { get; set; }
	public decimal? RequestedValue { get; set; }
}

public class ConcessionPolicySimulateResponseDto
{
	public decimal EligibleBase { get; set; }
	public decimal ConcessionAmount { get; set; }
	public decimal NewOutstanding { get; set; }
	public string Explanation { get; set; } = string.Empty;
}

public class ConcessionPolicySimulateRequestDtoValidator : AbstractValidator<ConcessionPolicySimulateRequestDto>
{
	public ConcessionPolicySimulateRequestDtoValidator()
	{
		RuleFor(x => x.EligibleBase).GreaterThan(0);
		RuleFor(x => x.RequestedValue).GreaterThan(0).When(x => x.RequestedValue.HasValue);
		RuleFor(x => x.RequestedBasis).Must(x => x is null ||
			string.Equals(x, "Percentage", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(x, "FixedAmount", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(x, "Flat", StringComparison.OrdinalIgnoreCase));
	}
}
