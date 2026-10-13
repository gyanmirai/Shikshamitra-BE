using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiConfigurationDraftDto
{
	public EsiCalculationSettings CalculationSettings { get; set; } = new();
	public string EffectiveMonth { get; set; } = string.Empty;
	public bool Enabled { get; set; }
	public string? RegistrationNumber { get; set; }
	public DateTime? CoveredSince { get; set; }
	public List<EsiTreatmentDraftDto> Treatments { get; set; } = [];
	public StatutoryImpactPreviewRequestDto? EmployeeQuery { get; set; }
}

public class EsiTreatmentDraftDto
{
	public Guid ComponentId { get; set; }
	public string Treatment { get; set; } = nameof(StatutoryWageTreatment.Unclassified);
}

public class SaveEsiConfigurationDto
{
	public string? EffectiveMonth { get; set; }
	public EsiCalculationSettings CalculationSettings { get; set; } = new();
	public bool Enabled { get; set; }
	public string? RegistrationNumber { get; set; }
	public DateTime? CoveredSince { get; set; }
	public List<EsiTreatmentDraftDto> Treatments { get; set; } = [];
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid IdempotencyKey { get; set; }
}

public class EsiConfigurationVersionDto
{
	public EsiCalculationSettings CalculationSettings { get; set; } = new();
	public Guid Id { get; set; }
	public string EffectiveMonth { get; set; } = string.Empty;
	public bool Enabled { get; set; }
	public string? RegistrationNumber { get; set; }
	public DateTime? CoveredSince { get; set; }
	public bool IsCancelled { get; set; }
	public string? CancellationReason { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class EsiConfigurationSettingsDto
{
	public bool HasSchoolWideScope { get; set; }
	public EsiConfigurationVersionDto? Current { get; set; }
	public List<EsiConfigurationVersionDto> History { get; set; } = [];
	public string SuggestedEffectiveMonth { get; set; } = string.Empty;
}

public interface IEsiConfigurationAppService : IApplicationService
{
	Task<EsiConfigurationSettingsDto> GetAsync();
	Task<EsiConfigurationSettingsDto> SaveAsync(SaveEsiConfigurationDto input);
}

public class EsiConfigurationDraftDtoValidator : AbstractValidator<EsiConfigurationDraftDto>
{
	public EsiConfigurationDraftDtoValidator()
	{
		RuleFor(x => x.CalculationSettings).NotNull();
		RuleFor(x => x.EffectiveMonth).Must(x => DateTime.TryParseExact(x, "yyyy-MM", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var month) && month.Year >= 2000 && month.Year < 9999);
		RuleFor(x => x.RegistrationNumber).MaximumLength(100);
		RuleFor(x => x.Treatments).Cascade(CascadeMode.Stop).NotNull()
			.Must(x => x.All(t => t != null) && x.Select(t => t.ComponentId).Distinct().Count() == x.Count);
		RuleForEach(x => x.Treatments).ChildRules(t =>
		{
			t.RuleFor(x => x.ComponentId).NotEmpty();
			t.RuleFor(x => x.Treatment).Must(x => Enum.TryParse<StatutoryWageTreatment>(x, out var value) && Enum.IsDefined(value));
		});
	}
}

public class SaveEsiConfigurationDtoValidator : AbstractValidator<SaveEsiConfigurationDto>
{
	public SaveEsiConfigurationDtoValidator()
	{
		RuleFor(x => x.EffectiveMonth).Must(x => x == null || DateTime.TryParseExact(x, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
		RuleFor(x => x.CalculationSettings).NotNull();
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(100);
		RuleFor(x => x.IdempotencyKey).NotEmpty();
		RuleFor(x => x.RegistrationNumber).MaximumLength(100);
		RuleFor(x => x.Treatments).NotNull().Must(x => x != null && x.All(t => t != null) && x.Select(t => t.ComponentId).Distinct().Count() == x.Count);
		RuleForEach(x => x.Treatments).ChildRules(t => {
			t.RuleFor(x => x.ComponentId).NotEmpty();
			t.RuleFor(x => x.Treatment).Must(x => Enum.TryParse<StatutoryWageTreatment>(x, out var value) && Enum.IsDefined(value));
		});
	}
}
