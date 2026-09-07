using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Compensation;

public class CompensationRevisionQuoteDto
{
	public Guid TemplateVersionId { get; set; }
	public string SalaryBasis { get; set; } = "EMPLOYER_COST";
	public string ProtectionPolicy { get; set; } = "PROTECT_EMPLOYER_COST";
	public decimal MonthlyAmount { get; set; }
	public DateTime EffectiveMonth { get; set; }
	public Guid? PfRuleId { get; set; }
	public string StatutoryProfile { get; set; } = "IN-GJ";
}

public class CreateCompensationRevisionDto : CompensationRevisionQuoteDto
{
}

public class UpdateCompensationRevisionDto : CompensationRevisionQuoteDto
{
}

public class RejectCompensationRevisionDto
{
	public string Reason { get; set; } = string.Empty;
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
	public string Status { get; set; } = "DRAFT";
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
	public string? CalculationSnapshotJson { get; set; }
	public Guid? CreatorId { get; set; }
	public Guid? SubmittedByUserId { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public Guid? RejectedByUserId { get; set; }
	public DateTime? RejectedAt { get; set; }
	public string? RejectionReason { get; set; }
	public List<CompensationRevisionLineDto> Lines { get; set; } = [];
}

public class CompensationRevisionListDto
{
	public List<CompensationRevisionDto> Items { get; set; } = [];
}

public interface ICompensationRevisionAppService : IApplicationService
{
	Task<CompensationRevisionListDto> GetListAsync(Guid employeeId);
	Task<CompensationRevisionDto?> GetEffectiveAsync(Guid employeeId, DateTime month);
	Task<CompensationRevisionDto> CreateAsync(Guid employeeId, CreateCompensationRevisionDto input);
	Task<CompensationRevisionDto> UpdateAsync(Guid employeeId, Guid revisionId, UpdateCompensationRevisionDto input);
	Task<CompensationRevisionDto> SubmitAsync(Guid employeeId, Guid revisionId);
	Task<CompensationRevisionDto> ApproveAsync(Guid employeeId, Guid revisionId);
	Task<CompensationRevisionDto> RejectAsync(Guid employeeId, Guid revisionId, RejectCompensationRevisionDto input);
}

public abstract class CompensationRevisionQuoteDtoValidator<T> : AbstractValidator<T>
	where T : CompensationRevisionQuoteDto
{
	protected CompensationRevisionQuoteDtoValidator()
	{
		RuleFor(x => x.TemplateVersionId).NotEmpty();
		RuleFor(x => x.SalaryBasis).Must(x => x is "GROSS" or "TAKE_HOME" or "EMPLOYER_COST");
		RuleFor(x => x.ProtectionPolicy).Must(x => x is "PROTECT_GROSS" or "PROTECT_TAKE_HOME" or "PROTECT_EMPLOYER_COST");
		RuleFor(x => x.MonthlyAmount).GreaterThan(0m).LessThanOrEqualTo(100_000_000m);
		RuleFor(x => x.EffectiveMonth).Must(x => x.Day == 1);
		RuleFor(x => x.StatutoryProfile).Equal("IN-GJ");
	}
}

public class CreateCompensationRevisionDtoValidator : CompensationRevisionQuoteDtoValidator<CreateCompensationRevisionDto>
{
}

public class UpdateCompensationRevisionDtoValidator : CompensationRevisionQuoteDtoValidator<UpdateCompensationRevisionDto>
{
}

public class RejectCompensationRevisionDtoValidator : AbstractValidator<RejectCompensationRevisionDto>
{
	public RejectCompensationRevisionDtoValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}
