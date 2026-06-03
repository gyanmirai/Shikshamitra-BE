using FluentValidation;
using System;

namespace ShikshaMitra.HR.Employees.WorkExperienceContracts;

public class WorkExperienceContract
{
	public Guid? Id { get; set; }

	public string? MediumStageName { get; set; }

	public string? Designation { get; set; }

	public string? Department { get; set; }

	public CountryInfo? Country { get; set; }

	public StateInfo? State { get; set; }

	public string? City { get; set; }

	public DateTime? StartDate { get; set; }

	public DateTime? EndDate { get; set; }
}

public class CountryInfo
{
	public string Name { get; set; } = string.Empty;

	public string IsoCode { get; set; } = string.Empty;
}

public class StateInfo
{
	public string Name { get; set; } = string.Empty;

	public string IsoCode { get; set; } = string.Empty;
}

public class WorkExperienceContractValidator : AbstractValidator<WorkExperienceContract>
{
	public WorkExperienceContractValidator()
	{
		RuleFor(x => x.MediumStageName).MaximumLength(256);
		RuleFor(x => x.Designation).MaximumLength(256);
		RuleFor(x => x.Department).MaximumLength(256);
		RuleFor(x => x.City).MaximumLength(128);
		RuleFor(x => x.Country).SetValidator(new CountryInfoValidator()!).When(x => x.Country != null);
		RuleFor(x => x.State).SetValidator(new StateInfoValidator()!).When(x => x.State != null);
	}
}

public class CountryInfoValidator : AbstractValidator<CountryInfo>
{
	public CountryInfoValidator()
	{
		RuleFor(x => x.Name).NotEmpty();
		RuleFor(x => x.IsoCode).NotEmpty();
	}
}

public class StateInfoValidator : AbstractValidator<StateInfo>
{
	public StateInfoValidator()
	{
		RuleFor(x => x.Name).NotEmpty();
		RuleFor(x => x.IsoCode).NotEmpty();
	}
}
