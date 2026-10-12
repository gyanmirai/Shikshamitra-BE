using System;
using FluentValidation;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class PfCoverageDto
{
	public Guid Id { get; set; }
	public DateTime EffectiveMonth { get; set; }
	public bool IsCovered { get; set; }
	public string EvidenceReference { get; set; } = string.Empty;
	public bool IsCancelled { get; set; }
}
public class PfCoverageSettingsDto
{
	public bool HasSchoolWideScope { get; set; }
	public List<PfCoverageDto> History { get; set; } = [];
}
public class SavePfCoverageDto
{
	public DateTime EffectiveMonth { get; set; }
	public bool IsCovered { get; set; }
	public string EvidenceReference { get; set; } = string.Empty;
	public Guid? ExpectedConfigurationId { get; set; }
	public Guid IdempotencyKey { get; set; }
}
public class SavePfCoverageDtoValidator : AbstractValidator<SavePfCoverageDto>
{
	public SavePfCoverageDtoValidator()
	{
		RuleFor(x => x.EffectiveMonth).Must(x => x.Year >= 2000 && x.Year < 9999 && x.Day == 1);
		RuleFor(x => x.EvidenceReference).NotEmpty().MaximumLength(1000);
		RuleFor(x => x.IdempotencyKey).NotEmpty();
	}
}
public interface IPfCoverageAppService : IApplicationService
{
	Task<PfCoverageSettingsDto> GetAsync();
	Task<PfCoverageSettingsDto> SaveAsync(SavePfCoverageDto input);
}
