using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Templates;

public class SalaryTemplateRuleDto
{
	public string Code { get; set; } = string.Empty;
	public Guid ComponentId { get; set; }
	public string LineKind { get; set; } = "EARNING";
	public string FormulaType { get; set; } = "FIXED_AMOUNT";
	public string SemanticType { get; set; } = "ORDINARY";
	public decimal? Value { get; set; }
	public string? BasisCode { get; set; }
	public Guid? BasisComponentId { get; set; }
	public int DisplayOrder { get; set; }
}

public class SalaryTemplateComponentCatalogItemDto
{
	public string CalculationType { get; set; } = "AMOUNT";
	public string? CalculationBase { get; set; }
	public decimal? DefaultValue { get; set; }
	public Guid? BasisComponentId { get; set; }
	public Guid ComponentId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string LineKind { get; set; } = "EARNING";
}

public class SalaryTemplateComponentCatalogDto
{
	public List<SalaryTemplateComponentCatalogItemDto> Items { get; set; } = [];
}

public class SaveSalaryTemplateDto
{
	public string PreviewToken { get; set; } = string.Empty;
	public decimal SampleMonthlyAmount { get; set; }
	public DateTime SampleEffectiveMonth { get; set; }

	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string DefaultSalaryBasis { get; set; } = "EMPLOYER_COST";
	[System.Text.Json.Serialization.JsonIgnore]
	public string DefaultProtectionPolicy { get => DefaultSalaryBasis == "EMPLOYER_COST" ? "PROTECT_EMPLOYER_COST" : "PROTECT_GROSS"; set { } }
	public bool DefaultPfApplicable { get; set; }
	public Guid? DefaultPfRuleId { get; set; }
	public List<SalaryTemplateRuleDto> Rules { get; set; } = [];
	public string IdempotencyKey { get; set; } = string.Empty;
	public Guid? ExpectedActiveVersionId { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class ArchiveSalaryTemplateDto
{
	public Guid? ReplacementDefaultTemplateId { get; set; }
}

public class SalaryTemplateVersionDto : EntityDto<Guid>
{
	public int VersionNumber { get; set; }
	public string Status { get; set; } = "ACTIVE";
	public DateTime? ActivatedAt { get; set; }
	public string DefaultSalaryBasis { get; set; } = "GROSS";
	public string DefaultProtectionPolicy { get; set; } = "PROTECT_GROSS";
	public bool DefaultPfApplicable { get; set; }
	public Guid? DefaultPfRuleId { get; set; }
	public List<SalaryTemplateRuleDto> Rules { get; set; } = [];
}

public class SalaryTemplateDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Status { get; set; } = "NEEDS_SETUP";
	public bool IsDefault { get; set; }
	public int? ActiveVersion { get; set; }
	public int VersionCount { get; set; }
	public DateTime UpdatedAt { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public SalaryTemplateVersionDto EditableVersion { get; set; } = new();
}

public class SalaryTemplateListDto
{
	public List<SalaryTemplateDto> Items { get; set; } = [];
}

public class SalaryTemplateVersionListDto
{
	public List<SalaryTemplateVersionDto> Items { get; set; } = [];
}

public class SalaryRuleOverrideDto
{
	public Guid ComponentId { get; set; }
	public string FormulaType { get; set; } = "FIXED_AMOUNT";
	public decimal? Value { get; set; }
	public Guid? BasisComponentId { get; set; }
}

public class CalculateSalaryTemplateRequestDto
{
	public string SalaryBasis { get; set; } = "EMPLOYER_COST";
	[System.Text.Json.Serialization.JsonIgnore]
	public string ProtectionPolicy { get => SalaryBasis == "EMPLOYER_COST" ? "PROTECT_EMPLOYER_COST" : "PROTECT_GROSS"; set { } }
	public decimal MonthlyAmount { get; set; }
	public DateTime EffectiveMonth { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
	public Guid? EmployeeId { get; set; }
	public bool PfApplicable { get; set; }
	public Guid? PfRuleId { get; set; }
	public string StatutoryProfile { get; set; } = "IN-GJ";
	public List<SalaryRuleOverrideDto> Overrides { get; set; } = [];
	public string? CustomizationReason { get; set; }
}

public class PreviewSalaryTemplateRequestDto
{
	public Guid? TemplateId { get; set; }
	public Guid? ExpectedActiveVersionId { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public List<SalaryTemplateRuleDto> Rules { get; set; } = [];
	public string SalaryBasis { get; set; } = "EMPLOYER_COST";
	[System.Text.Json.Serialization.JsonIgnore]
	public string ProtectionPolicy { get => SalaryBasis == "EMPLOYER_COST" ? "PROTECT_EMPLOYER_COST" : "PROTECT_GROSS"; set { } }
	public decimal MonthlyAmount { get; set; }
	public DateTime EffectiveMonth { get; set; }
	public bool PfApplicable { get; set; }
	public Guid? PfRuleId { get; set; }
}

public class SalaryTemplatePreviewDto
{
	public bool IsSample { get; set; } = true;
	public string PreviewToken { get; set; } = string.Empty;
	public DateTime ExpiresAt { get; set; }
	public SalaryTemplateCalculationDto Calculation { get; set; } = new();
}

public class PreviewSalaryTemplateRequestDtoValidator : AbstractValidator<PreviewSalaryTemplateRequestDto>
{
	public PreviewSalaryTemplateRequestDtoValidator()
	{
		RuleFor(x => x.SalaryBasis).Must(x => x is "GROSS" or "EMPLOYER_COST");
		RuleFor(x => x.MonthlyAmount).GreaterThan(0m).LessThanOrEqualTo(100_000_000m);
		RuleFor(x => x.EffectiveMonth).Must(x => x.Year >= 1900 && x.Day == 1);
		RuleFor(x => x.PfRuleId).NotEmpty().When(x => x.PfApplicable);
		RuleFor(x => x.Rules).NotEmpty();
		RuleForEach(x => x.Rules).SetValidator(new SalaryTemplateRuleDtoValidator());
	}
}

public class CalculatedSalaryLineDto
{
	public decimal? FormulaValue { get; set; }
	public Guid? BasisComponentId { get; set; }
	public Guid ComponentId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string SemanticType { get; set; } = "ORDINARY";
	public string FormulaType { get; set; } = "FIXED_AMOUNT";
	public bool IsStatutory { get; set; }
	public string LineSource { get; set; } = "TEMPLATE";
	public bool Persistable { get; set; } = true;
}

public class SalaryTemplateTargetResultDto
{
	public decimal Requested { get; set; }
	public decimal Achieved { get; set; }
	public decimal Difference { get; set; }
	public bool IsExact { get; set; }
}

public class SalaryTemplateEsiPreviewDto
{
	public Guid? ConfigurationId { get; set; }
	public bool IsConfigured { get; set; }
	public bool IsCovered { get; set; }
	public bool CanCalculate { get; set; }
	public bool RegistrationPending { get; set; }
	public string? CoverageDecision { get; set; }
	public decimal StatutoryWages { get; set; }
	public decimal WageCeiling { get; set; }
	public decimal EmployeeContribution { get; set; }
	public decimal EmployerContribution { get; set; }
	public decimal GovernmentContribution { get; set; }
	public Guid? RuleId { get; set; }
	public int? RuleVersion { get; set; }
	public List<string> IssueCodes { get; set; } = [];
}

public class SalaryTemplateCalculationDto
{
	public Guid TemplateId { get; set; }
	public Guid TemplateVersionId { get; set; }
	public int TemplateVersion { get; set; }
	public string SalaryBasis { get; set; } = "GROSS";
	public string ProtectionPolicy { get; set; } = "PROTECT_GROSS";
	public decimal QuotedMonthlyAmount { get; set; }
	public DateTime EffectiveMonth { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal BasicSalary { get; set; }
	public decimal TotalDeductions { get; set; }
	public decimal NetSalary { get; set; }
	public decimal EmployerContributionTotal { get; set; }
	public decimal EmployerCost { get; set; }
	public decimal ProtectedMonthlyAmount { get; set; }
	public SalaryTemplateTargetResultDto TargetResult { get; set; } = new();
	public SalaryTemplateEsiPreviewDto EmployeeStateInsurance { get; set; } = new();
	public List<CalculatedSalaryLineDto> Earnings { get; set; } = [];
	public List<CalculatedSalaryLineDto> Deductions { get; set; } = [];
	public List<CalculatedSalaryLineDto> EmployerContributions { get; set; } = [];
	public List<string> FormulaTrace { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
	public Guid? PfRuleId { get; set; }
	public int? PfRuleVersion { get; set; }
	public string ProfessionalTaxProfile { get; set; } = "IN-GJ";
	public DateTime GeneratedAt { get; set; }
}

public interface ISalaryTemplateAppService : IApplicationService
{
	Task<SalaryTemplatePreviewDto> PreviewAsync(PreviewSalaryTemplateRequestDto input);
	Task<SalaryTemplateListDto> GetListAsync();
	Task<SalaryTemplateListDto> GetActiveListAsync();
	Task<SalaryTemplateComponentCatalogDto> GetComponentCatalogAsync();
	Task<SalaryTemplateDto> GetAsync(Guid id);
	Task<SalaryTemplateVersionListDto> GetVersionsAsync(Guid id);
	Task<SalaryTemplateDto> CreateAsync(SaveSalaryTemplateDto input);
	Task<SalaryTemplateDto> UpdateAsync(Guid id, SaveSalaryTemplateDto input);
	Task ArchiveAsync(Guid id, ArchiveSalaryTemplateDto input);
	Task<SalaryTemplateDto> SetDefaultAsync(Guid id);
	Task<SalaryTemplateCalculationDto> CalculateAsync(Guid templateVersionId, CalculateSalaryTemplateRequestDto input);
}

public class SaveSalaryTemplateDtoValidator : AbstractValidator<SaveSalaryTemplateDto>
{
	public SaveSalaryTemplateDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(500);
		RuleFor(x => x.DefaultSalaryBasis).Must(BeSalaryBasis);
		RuleFor(x => x.DefaultPfRuleId).NotEmpty().When(x => x.DefaultPfApplicable)
			.WithMessage("Choose an effective PF rule when PF applies by default.");
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
		RuleForEach(x => x.Rules).SetValidator(new SalaryTemplateRuleDtoValidator());
	}

	private static bool BeSalaryBasis(string value) => value is "GROSS" or "EMPLOYER_COST";
	private static bool BeProtectionPolicy(string value) => Enum.TryParse<CompensationProtectionPolicy>(Normalize(value), true, out _);
	private static string Normalize(string? value) => value?.Replace("_", string.Empty).Replace("-", string.Empty) ?? string.Empty;
}

public class SalaryTemplateRuleDtoValidator : AbstractValidator<SalaryTemplateRuleDto>
{
	public SalaryTemplateRuleDtoValidator()
	{
		RuleFor(x => x.Code).MaximumLength(64).Matches("^[A-Z][A-Z0-9_]*$")
			.When(x => !string.IsNullOrWhiteSpace(x.Code));
		RuleFor(x => x.ComponentId).NotEmpty();
		RuleFor(x => x.LineKind).NotEmpty().Must(BeLineKind).WithMessage("LineKind must be EARNING or DEDUCTION.");
		RuleFor(x => x.FormulaType).NotEmpty().Must(BeFormulaType).WithMessage("FormulaType is invalid.");
		RuleFor(x => x.SemanticType).NotEmpty().Must(BeSemanticType).WithMessage("SemanticType is invalid.");
		RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
	}

	private static bool BeLineKind(string value) => Enum.TryParse<SalaryTemplateLineKind>(Normalize(value), true, out _);
	private static bool BeFormulaType(string value) => Enum.TryParse<SalaryTemplateFormulaType>(Normalize(value), true, out _);
	private static bool BeSemanticType(string value) => Enum.TryParse<PayrollComponentSemanticType>(Normalize(value), true, out _);
	private static string Normalize(string? value) => value?.Replace("_", string.Empty).Replace("-", string.Empty) ?? string.Empty;
}

public class SalaryRuleOverrideDtoValidator : AbstractValidator<SalaryRuleOverrideDto>
{
	public SalaryRuleOverrideDtoValidator()
	{
		RuleFor(x => x.ComponentId).NotEmpty();
		RuleFor(x => x.FormulaType).NotEmpty();
	}
}

public class CalculateSalaryTemplateRequestDtoValidator : AbstractValidator<CalculateSalaryTemplateRequestDto>
{
	public CalculateSalaryTemplateRequestDtoValidator()
	{
		RuleFor(x => x.SalaryBasis).Must(x => x is "GROSS" or "EMPLOYER_COST");
		RuleFor(x => x.MonthlyAmount).GreaterThan(0m).LessThanOrEqualTo(100_000_000m);
		RuleFor(x => x.EffectiveMonth).Must(x => x.Day == 1).WithMessage("EffectiveMonth must be the first day of a month.");
		RuleFor(x => x.PfRuleId).NotEmpty().When(x => x.PfApplicable);
		RuleFor(x => x.StatutoryProfile).Equal("IN-GJ");
		RuleFor(x => x.CustomizationReason).NotEmpty().MaximumLength(500).When(x => x.Overrides.Count > 0);
		RuleForEach(x => x.Overrides).SetValidator(new SalaryRuleOverrideDtoValidator());
	}
}
