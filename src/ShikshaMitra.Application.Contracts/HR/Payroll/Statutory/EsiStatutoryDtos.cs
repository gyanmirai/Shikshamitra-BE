using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class StatutoryRuleVersionDto
{
	public Guid Id { get; set; }
	public string SchemeCode { get; set; } = StatutorySchemeCodes.EmployeeStateInsurance;
	public string JurisdictionCode { get; set; } = "IN-GJ";
	public int Version { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public int EstablishmentThreshold { get; set; }
	public decimal StandardWageCeiling { get; set; }
	public decimal DisabilityWageCeiling { get; set; }
	public decimal EmployeePercentage { get; set; }
	public decimal EmployerPercentage { get; set; }
	public decimal ExclusionCapPercentage { get; set; }
	public decimal InKindCapPercentage { get; set; }
	public string RoundingPolicy { get; set; } = nameof(StatutoryRoundingPolicy.NextHigherRupee);
	public string SourceUrl { get; set; } = string.Empty;
	public DateTime SourceVerifiedOn { get; set; }
}

public class StatutoryEstablishmentDto
{
	public Guid? Id { get; set; }
	public string SchemeCode { get; set; } = StatutorySchemeCodes.EmployeeStateInsurance;
	public string JurisdictionCode { get; set; } = "IN-GJ";
	public string? RegistrationNumber { get; set; }
	public int EmployeeCount { get; set; }
	public DateTime? CoveredSince { get; set; }
	public DateTime? BenefitsActiveFrom { get; set; }
	public string Status { get; set; } = nameof(StatutoryEstablishmentStatus.Draft);
	public string? EvidenceReference { get; set; }
	public Guid? ConfirmedByUserId { get; set; }
	public DateTime? ConfirmedAt { get; set; }
	public bool CanActivate { get; set; }
}

public class ConfigureStatutoryEstablishmentDto
{
	public string JurisdictionCode { get; set; } = "IN-GJ";
	public string? RegistrationNumber { get; set; }
	public int EmployeeCount { get; set; }
	public DateTime? CoveredSince { get; set; }
	public DateTime? BenefitsActiveFrom { get; set; }
	public string? EvidenceReference { get; set; }
}

public class PayrollComponentStatutoryTreatmentDto
{
	public Guid ComponentId { get; set; }
	public string ComponentName { get; set; } = string.Empty;
	public string Treatment { get; set; } = nameof(StatutoryWageTreatment.Unclassified);
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
}

public class UpdatePayrollComponentStatutoryTreatmentsDto
{
	public List<PayrollComponentStatutoryTreatmentDto> Items { get; set; } = [];
}

public class EmployeeStatutoryEnrollmentDto
{
	public Guid? Id { get; set; }
	public Guid EmployeeId { get; set; }
	public string SchemeCode { get; set; } = StatutorySchemeCodes.EmployeeStateInsurance;
	public string Status { get; set; } = nameof(EmployeeStatutoryEnrollmentStatus.PendingReview);
	public string WageCeilingCategory { get; set; } = nameof(StatutoryWageCeilingCategory.Standard);
	public string? InsuranceNumber { get; set; }
	public DateTime? CoveredFrom { get; set; }
	public DateTime? CoveredTo { get; set; }
	public DateTime? ContributionPeriodEnd { get; set; }
	public string? StatusReason { get; set; }
	public bool EmployerShareReliefEligible { get; set; }
	public DateTime? EmployerShareReliefFrom { get; set; }
	public DateTime? EmployerShareReliefTo { get; set; }
	public string? DisabilityEvidenceReference { get; set; }
}

public class UpdateEmployeeStatutoryEnrollmentDto
{
	public string Status { get; set; } = nameof(EmployeeStatutoryEnrollmentStatus.PendingReview);
	public string WageCeilingCategory { get; set; } = nameof(StatutoryWageCeilingCategory.Standard);
	public string? InsuranceNumber { get; set; }
	public DateTime? CoveredFrom { get; set; }
	public DateTime? CoveredTo { get; set; }
	public DateTime? ContributionPeriodEnd { get; set; }
	public string? StatusReason { get; set; }
	public bool EmployerShareReliefEligible { get; set; }
	public DateTime? EmployerShareReliefFrom { get; set; }
	public DateTime? EmployerShareReliefTo { get; set; }
	public string? DisabilityEvidenceReference { get; set; }
}

public class CreateStatutoryOverrideDto
{
	public string OverrideType { get; set; } = nameof(StatutoryOverrideType.ForceCovered);
	public decimal? Amount { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? EvidenceReference { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime EffectiveTo { get; set; }
}

public class StatutoryOverrideDto
{
	public Guid Id { get; set; }
	public Guid EmployeeId { get; set; }
	public string SchemeCode { get; set; } = StatutorySchemeCodes.EmployeeStateInsurance;
	public string OverrideType { get; set; } = string.Empty;
	public decimal? Amount { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? EvidenceReference { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime EffectiveTo { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime ApprovedAt { get; set; }
}

public class StatutoryImpactPreviewRequestDto
{
	public string MonthYear { get; set; } = string.Empty;
}

public class EmployeeStatutoryImpactDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string EmployeeName { get; set; } = string.Empty;
	public bool IsCovered { get; set; }
	public bool CanCalculate { get; set; }
	public string CoverageDecision { get; set; } = string.Empty;
	public decimal StatutoryWages { get; set; }
	public decimal WageCeiling { get; set; }
	public decimal EmployeeContribution { get; set; }
	public decimal EmployerContribution { get; set; }
	public decimal GovernmentContribution { get; set; }
	public string EnrollmentStatus { get; set; } = nameof(EmployeeStatutoryEnrollmentStatus.PendingReview);
	public string WageCeilingCategory { get; set; } = nameof(StatutoryWageCeilingCategory.Standard);
	public string? InsuranceNumber { get; set; }
	public DateTime? ContributionPeriodEnd { get; set; }
	public List<string> IssueCodes { get; set; } = [];
}

public class StatutoryImpactPreviewDto
{
	public string SchemeCode { get; set; } = StatutorySchemeCodes.EmployeeStateInsurance;
	public string MonthYear { get; set; } = string.Empty;
	public int EmployeeCount { get; set; }
	public int CoveredEmployeeCount { get; set; }
	public decimal EmployeeContributionTotal { get; set; }
	public decimal EmployerContributionTotal { get; set; }
	public decimal GovernmentContributionTotal { get; set; }
	public bool CanActivate { get; set; }
	public List<string> BlockingIssueCodes { get; set; } = [];
	public List<EmployeeStatutoryImpactDto> Employees { get; set; } = [];
}

public interface IEsiStatutoryAppService : IApplicationService
{
	Task<StatutoryRuleVersionDto> GetCurrentRuleAsync(DateTime? effectiveOn = null);
	Task<StatutoryEstablishmentDto> GetEstablishmentAsync();
	Task<StatutoryEstablishmentDto> ConfigureEstablishmentAsync(ConfigureStatutoryEstablishmentDto input);
	Task<List<PayrollComponentStatutoryTreatmentDto>> GetComponentTreatmentsAsync(DateTime? effectiveOn = null);
	Task<List<PayrollComponentStatutoryTreatmentDto>> UpdateComponentTreatmentsAsync(UpdatePayrollComponentStatutoryTreatmentsDto input);
	Task<EmployeeStatutoryEnrollmentDto> GetEnrollmentAsync(Guid employeeId);
	Task<EmployeeStatutoryEnrollmentDto> UpdateEnrollmentAsync(Guid employeeId, UpdateEmployeeStatutoryEnrollmentDto input);
	Task<List<StatutoryOverrideDto>> GetOverridesAsync(Guid employeeId);
	Task CreateOverrideAsync(Guid employeeId, CreateStatutoryOverrideDto input);
	Task<StatutoryImpactPreviewDto> PreviewAsync(StatutoryImpactPreviewRequestDto input);
	Task<StatutoryEstablishmentDto> ActivateAsync(StatutoryImpactPreviewRequestDto input);
}

public class ConfigureStatutoryEstablishmentDtoValidator : AbstractValidator<ConfigureStatutoryEstablishmentDto>
{
	public ConfigureStatutoryEstablishmentDtoValidator()
	{
		RuleFor(x => x.JurisdictionCode).Equal("IN-GJ");
		RuleFor(x => x.RegistrationNumber).MaximumLength(100);
		RuleFor(x => x.EmployeeCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.EvidenceReference).MaximumLength(1000);
	}
}

public class UpdatePayrollComponentStatutoryTreatmentsDtoValidator : AbstractValidator<UpdatePayrollComponentStatutoryTreatmentsDto>
{
	public UpdatePayrollComponentStatutoryTreatmentsDtoValidator()
	{
		RuleFor(x => x.Items).NotEmpty();
		RuleFor(x => x.Items)
			.Must(items => items.Select(x => new { x.ComponentId, EffectiveFrom = x.EffectiveFrom.Date }).Distinct().Count() == items.Count)
			.WithMessage("A component can have only one treatment for an effective date.");
		RuleForEach(x => x.Items).ChildRules(item =>
		{
			item.RuleFor(x => x.ComponentId).NotEmpty();
			item.RuleFor(x => x.Treatment).Must(value => Enum.TryParse<StatutoryWageTreatment>(value, true, out _));
			item.RuleFor(x => x.EffectiveFrom).NotEmpty();
			item.RuleFor(x => x.EffectiveTo)
				.GreaterThanOrEqualTo(x => x.EffectiveFrom)
				.When(x => x.EffectiveTo.HasValue);
		});
	}
}

public class UpdateEmployeeStatutoryEnrollmentDtoValidator : AbstractValidator<UpdateEmployeeStatutoryEnrollmentDto>
{
	public UpdateEmployeeStatutoryEnrollmentDtoValidator()
	{
		RuleFor(x => x.Status).Must(value => Enum.TryParse<EmployeeStatutoryEnrollmentStatus>(value, true, out _));
		RuleFor(x => x.WageCeilingCategory).Must(value => Enum.TryParse<StatutoryWageCeilingCategory>(value, true, out _));
		RuleFor(x => x.InsuranceNumber).MaximumLength(100);
		RuleFor(x => x.StatusReason).MaximumLength(500);
		RuleFor(x => x.DisabilityEvidenceReference).MaximumLength(1000);
		RuleFor(x => x.CoveredTo).GreaterThanOrEqualTo(x => x.CoveredFrom!.Value).When(x => x.CoveredFrom.HasValue && x.CoveredTo.HasValue);
		RuleFor(x => x.EmployerShareReliefFrom).NotNull().When(x => x.EmployerShareReliefEligible);
		RuleFor(x => x.DisabilityEvidenceReference).NotEmpty().When(x => x.EmployerShareReliefEligible);
		RuleFor(x => x.WageCeilingCategory)
			.Equal(nameof(StatutoryWageCeilingCategory.PersonWithDisability))
			.When(x => x.EmployerShareReliefEligible)
			.WithMessage("Employer share relief requires the person with disability wage ceiling category.");
		RuleFor(x => x.EmployerShareReliefTo)
			.GreaterThanOrEqualTo(x => x.EmployerShareReliefFrom!.Value)
			.When(x => x.EmployerShareReliefFrom.HasValue && x.EmployerShareReliefTo.HasValue);
	}
}

public class CreateStatutoryOverrideDtoValidator : AbstractValidator<CreateStatutoryOverrideDto>
{
	public CreateStatutoryOverrideDtoValidator()
	{
		RuleFor(x => x.OverrideType).Must(value => Enum.TryParse<StatutoryOverrideType>(value, true, out _));
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
		RuleFor(x => x.EvidenceReference).MaximumLength(1000);
		RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom);
		RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m).When(x => x.Amount.HasValue);
	}
}

public class StatutoryImpactPreviewRequestDtoValidator : AbstractValidator<StatutoryImpactPreviewRequestDto>
{
	public StatutoryImpactPreviewRequestDtoValidator()
	{
		RuleFor(x => x.MonthYear).Matches("^(0[1-9]|1[0-2])-\\d{4}$");
	}
}
