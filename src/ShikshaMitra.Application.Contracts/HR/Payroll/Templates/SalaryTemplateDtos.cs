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
	public Guid ComponentId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string LineKind { get; set; } = "EARNING";
}

public class SalaryTemplateComponentCatalogDto
{
	public List<SalaryTemplateComponentCatalogItemDto> Items { get; set; } = [];
}

public class CreateSalaryTemplateDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
}

public class UpdateSalaryTemplateDraftDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public List<SalaryTemplateRuleDto> Rules { get; set; } = [];
}

public class SalaryTemplateVersionDto : EntityDto<Guid>
{
	public int VersionNumber { get; set; }
	public string Status { get; set; } = "DRAFT";
	public DateTime? PublishedAt { get; set; }
	public List<SalaryTemplateRuleDto> Rules { get; set; } = [];
}

public class SalaryTemplateDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Status { get; set; } = "DRAFT";
	public bool IsDefault { get; set; }
	public int LatestVersion { get; set; }
	public int? PublishedVersion { get; set; }
	public SalaryTemplateVersionDto Latest { get; set; } = new();
	public SalaryTemplateVersionDto? Published { get; set; }
}

public class SalaryTemplateListDto
{
	public List<SalaryTemplateDto> Items { get; set; } = [];
}

public class CalculateSalaryTemplateRequestDto
{
	public string SalaryBasis { get; set; } = "GROSS";
	public decimal MonthlyAmount { get; set; }
	public Guid? PfRuleId { get; set; }
	public string StatutoryProfile { get; set; } = "IN-GJ";
}

public class CalculatedSalaryLineDto
{
	public Guid ComponentId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string SemanticType { get; set; } = "ORDINARY";
	public string FormulaType { get; set; } = "FIXED_AMOUNT";
	public bool IsStatutory { get; set; }
}

public class SalaryTemplateCalculationDto
{
	public Guid TemplateId { get; set; }
	public Guid TemplateVersionId { get; set; }
	public int TemplateVersion { get; set; }
	public string SalaryBasis { get; set; } = "GROSS";
	public decimal QuotedMonthlyAmount { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal BasicSalary { get; set; }
	public decimal TotalDeductions { get; set; }
	public decimal NetSalary { get; set; }
	public decimal EmployerContributionTotal { get; set; }
	public decimal EmployerCost { get; set; }
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
	Task<SalaryTemplateListDto> GetListAsync();
	Task<SalaryTemplateListDto> GetPublishedListAsync();
	Task<SalaryTemplateComponentCatalogDto> GetComponentCatalogAsync();
	Task<SalaryTemplateDto> GetAsync(Guid id);
	Task<SalaryTemplateDto> CreateAsync(CreateSalaryTemplateDto input);
	Task<SalaryTemplateDto> CreateNextVersionAsync(Guid id);
	Task<SalaryTemplateDto> UpdateDraftAsync(Guid id, Guid versionId, UpdateSalaryTemplateDraftDto input);
	Task<SalaryTemplateDto> PublishAsync(Guid id, Guid versionId);
	Task<SalaryTemplateDto> CloneAsync(Guid id, string name);
	Task ArchiveAsync(Guid id);
	Task<SalaryTemplateDto> SetDefaultAsync(Guid id);
	Task<SalaryTemplateCalculationDto> CalculateAsync(Guid templateVersionId, CalculateSalaryTemplateRequestDto input);
}

public class CreateSalaryTemplateDtoValidator : AbstractValidator<CreateSalaryTemplateDto>
{
	public CreateSalaryTemplateDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(500);
	}
}

public class UpdateSalaryTemplateDraftDtoValidator : AbstractValidator<UpdateSalaryTemplateDraftDto>
{
	public UpdateSalaryTemplateDraftDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Description).MaximumLength(500);
		RuleForEach(x => x.Rules).SetValidator(new SalaryTemplateRuleDtoValidator());
	}
}

public class SalaryTemplateRuleDtoValidator : AbstractValidator<SalaryTemplateRuleDto>
{
	public SalaryTemplateRuleDtoValidator()
	{
		RuleFor(x => x.Code)
			.MaximumLength(64)
			.Matches("^[A-Z][A-Z0-9_]*$")
			.When(x => !string.IsNullOrWhiteSpace(x.Code));
		RuleFor(x => x.ComponentId).NotEmpty();
		RuleFor(x => x.LineKind).NotEmpty().Must(BeLineKind).WithMessage("LineKind must be EARNING or DEDUCTION.");
		RuleFor(x => x.FormulaType).NotEmpty().Must(BeFormulaType).WithMessage("FormulaType is invalid.");
		RuleFor(x => x.SemanticType).NotEmpty().Must(BeSemanticType).WithMessage("SemanticType is invalid.");
		RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
	}

	private static bool BeLineKind(string value) =>
		Enum.TryParse<SalaryTemplateLineKind>(Normalize(value), true, out _);

	private static bool BeFormulaType(string value) =>
		Enum.TryParse<SalaryTemplateFormulaType>(Normalize(value), true, out _);

	private static bool BeSemanticType(string value) =>
		Enum.TryParse<PayrollComponentSemanticType>(Normalize(value), true, out _);

	private static string Normalize(string? value) => value?.Replace("_", string.Empty).Replace("-", string.Empty) ?? string.Empty;
}

public class CalculateSalaryTemplateRequestDtoValidator : AbstractValidator<CalculateSalaryTemplateRequestDto>
{
	public CalculateSalaryTemplateRequestDtoValidator()
	{
		RuleFor(x => x.SalaryBasis).Must(x => x is "GROSS" or "TAKE_HOME");
		RuleFor(x => x.MonthlyAmount).GreaterThan(0m).LessThanOrEqualTo(100_000_000m);
		RuleFor(x => x.StatutoryProfile).Equal("IN-GJ");
	}
}
