using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Compensation;

public class SaveEmployeeCompensationDto
{
	public Guid TemplateVersionId { get; set; }
	public string SalaryBasis { get; set; } = "EMPLOYER_COST";
	[System.Text.Json.Serialization.JsonIgnore]
	public string ProtectionPolicy { get => SalaryBasis == "EMPLOYER_COST" ? "PROTECT_EMPLOYER_COST" : "PROTECT_GROSS"; set { } }
	public decimal MonthlyAmount { get; set; }
	public DateTime EffectiveMonth { get; set; }
	public bool PfApplicable { get; set; }
	public Guid? PfRuleId { get; set; }
	public string StatutoryProfile { get; set; } = "IN-GJ";
	public List<SalaryRuleOverrideDto> Overrides { get; set; } = [];
	public string? CustomizationReason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public int? ExpectedRevision { get; set; }
}

public class CompensationRevisionLineDto : EntityDto<Guid>
{
	public Guid? ComponentId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Amount { get; set; }
	public bool IsEarning { get; set; }
	public string SemanticType { get; set; } = "ORDINARY";
	public string Source { get; set; } = "TEMPLATE";
	public int DisplayOrder { get; set; }
}

public class CompensationRevisionDto : EntityDto<Guid>
{
	public Guid EmployeeId { get; set; }
	public int RevisionNumber { get; set; }
	public DateTime EffectiveMonth { get; set; }
	public string Status { get; set; } = "EFFECTIVE";
	public bool IsCurrent { get; set; }
	public bool IsScheduled { get; set; }
	public string SalaryBasis { get; set; } = "EMPLOYER_COST";
	public string ProtectionPolicy { get; set; } = "PROTECT_EMPLOYER_COST";
	public decimal QuotedMonthlyAmount { get; set; }
	public decimal ProtectedMonthlyAmount { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal NetSalary { get; set; }
	public decimal EmployerCost { get; set; }
	public Guid? SourceSalaryTemplateId { get; set; }
	public Guid? SourceSalaryTemplateVersionId { get; set; }
	public int? SourceSalaryTemplateVersion { get; set; }
	public Guid? PfRuleId { get; set; }
	public string StatutoryProfile { get; set; } = "IN-GJ";
	public bool IsCustomized { get; set; }
	public List<SalaryRuleOverrideDto> Overrides { get; set; } = [];
	public string? CustomizationReason { get; set; }
	public List<string> Warnings { get; set; } = [];
	public EmployeeActivationReadinessDto ActivationReadiness { get; set; } = new();
	public string? CalculationSnapshotJson { get; set; }
	public Guid? CreatorId { get; set; }
	public Guid? SavedByUserId { get; set; }
	public DateTime? SavedAt { get; set; }
	public List<CompensationRevisionLineDto> Lines { get; set; } = [];
	// Saved calculation amounts for display only; never persisted as manual salary components.
	public List<CalculatedSalaryLineDto> EstimatedStatutoryDeductions { get; set; } = [];
}

public class CompensationRevisionListDto
{
	public List<CompensationRevisionDto> Items { get; set; } = [];
}

public interface ICompensationRevisionAppService : IApplicationService
{
	Task<CompensationRevisionListDto> GetListAsync(Guid employeeId);
	Task<CompensationRevisionDto?> GetEffectiveAsync(Guid employeeId, DateTime month);
	Task<CompensationRevisionDto> SaveAsync(Guid employeeId, SaveEmployeeCompensationDto input);
}

public class SaveEmployeeCompensationDtoValidator : AbstractValidator<SaveEmployeeCompensationDto>
{
	public SaveEmployeeCompensationDtoValidator()
	{
		RuleFor(x => x.TemplateVersionId).NotEmpty();
		RuleFor(x => x.SalaryBasis).Must(x => x is "GROSS" or "EMPLOYER_COST");
		RuleFor(x => x.MonthlyAmount).GreaterThan(0m).LessThanOrEqualTo(100_000_000m);
		RuleFor(x => x.EffectiveMonth).Must(x => x.Day == 1);
		RuleFor(x => x.PfRuleId).NotEmpty().When(x => x.PfApplicable);
		RuleFor(x => x.StatutoryProfile).Equal("IN-GJ");
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
		RuleFor(x => x.CustomizationReason).NotEmpty().MaximumLength(500).When(x => x.Overrides.Count > 0);
		RuleForEach(x => x.Overrides).SetValidator(new SalaryRuleOverrideDtoValidator());
	}
}
