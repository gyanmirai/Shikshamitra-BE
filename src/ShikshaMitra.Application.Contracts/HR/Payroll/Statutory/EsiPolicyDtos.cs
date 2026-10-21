using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiPolicyVersionInput
{
	public DateTime EffectiveFrom { get; set; }
	public decimal EmployeePercentage { get; set; } = 0.75m;
	public decimal EmployerPercentage { get; set; } = 3.25m;
	public decimal StandardWageCeiling { get; set; } = 21000m;
	public decimal DisabilityWageCeiling { get; set; } = 25000m;
	public decimal EmployeeShareDailyWageExemption { get; set; } = 176m;
	public int EstablishmentThreshold { get; set; } = 10;
	public decimal ExclusionCapPercentage { get; set; } = 50m;
	public decimal InKindCapPercentage { get; set; } = 15m;
	public string RoundingPolicy { get; set; } = "NextHigherRupee";
	public string SourceUrl { get; set; } = string.Empty;
	public DateTime SourceVerifiedOn { get; set; }
	public EsiCalculationSettings CalculationSettings { get; set; } = new();
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid IdempotencyKey { get; set; }
}
public class CreateEsiPolicyDto : EsiPolicyVersionInput
{
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
}
public class EsiPolicyVersionDto : StatutoryRuleVersionDto
{
	public DateTime? AppliesUntil { get; set; }
	public EsiCalculationSettings? CalculationSettings { get; set; }
}
public class EsiPolicyDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public bool IsDefault { get; set; }
	public bool IsArchived { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public List<EsiPolicyVersionDto> Versions { get; set; } = [];
}
public interface IEsiPolicyAppService : IApplicationService
{
	Task<List<EsiPolicyDto>> GetListAsync();
	Task<List<EsiPolicyDto>> GetOptionsAsync();
	Task<EsiPolicyDto> CreateAsync(CreateEsiPolicyDto input);
	Task<EsiPolicyDto> AddVersionAsync(Guid id, EsiPolicyVersionInput input);
	Task ArchiveAsync(Guid id, string concurrencyStamp);
}
public class EsiPolicyVersionInputValidator : AbstractValidator<EsiPolicyVersionInput>
{
	public EsiPolicyVersionInputValidator()
	{
		RuleFor(x => x.EffectiveFrom).Must(x => x.Year >= 1900 && x.Day == 1 && x.TimeOfDay == TimeSpan.Zero);
		RuleFor(x => x.EmployeePercentage).InclusiveBetween(0m, 100m);
		RuleFor(x => x.EmployerPercentage).InclusiveBetween(0m, 100m);
		RuleFor(x => x.StandardWageCeiling).GreaterThan(0m);
		RuleFor(x => x.DisabilityWageCeiling).GreaterThanOrEqualTo(x => x.StandardWageCeiling);
		RuleFor(x => x.EmployeeShareDailyWageExemption).GreaterThanOrEqualTo(0m);
		RuleFor(x => x.EstablishmentThreshold).GreaterThan(0);
		RuleFor(x => x.ExclusionCapPercentage).InclusiveBetween(0m, 100m);
		RuleFor(x => x.InKindCapPercentage).InclusiveBetween(0m, 100m);
		RuleFor(x => x.RoundingPolicy).Must(x => Enum.TryParse<StatutoryRoundingPolicy>(x, out var policy) && Enum.IsDefined(policy));
		RuleFor(x => x.SourceUrl).NotEmpty().MaximumLength(1000).Must(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https");
		RuleFor(x => x.SourceVerifiedOn).Must(x => x.Year >= 1900);
		RuleFor(x => x.CalculationSettings).NotNull();
		RuleFor(x => x.IdempotencyKey).NotEmpty();
	}
}
