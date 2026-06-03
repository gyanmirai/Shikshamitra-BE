using System;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

/// <summary>
/// Request DTO for creating or updating employee family information
/// </summary>
public class FamilyInfoRequest
{
	public Guid? Id { get; set; }

	public FamilyMemberInfo? FatherDetails { get; set; }

	public FamilyMemberInfo? MotherDetails { get; set; }

	public FamilyMemberInfo? SpouseDetails { get; set; }
}

/// <summary>
/// Response DTO for employee family information
/// </summary>
public class FamilyInfoResponse
{
	public Guid Id { get; set; }

	public FamilyMemberInfo? FatherDetails { get; set; }

	public FamilyMemberInfo? MotherDetails { get; set; }

	public FamilyMemberInfo? SpouseDetails { get; set; }
}

/// <summary>
/// Family member details
/// </summary>
public class FamilyMemberInfo
{
	public string? Name { get; set; }

	public string? Occupation { get; set; }

	public string? MobileNo { get; set; }

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public CountryInfo? Country { get; set; }

	public StateInfo? State { get; set; }

	public string? City { get; set; }

	public string? Pincode { get; set; }
}

/// <summary>
/// Country information
/// </summary>
public class CountryInfo
{
	public string? Name { get; set; }

	public string? IsoCode { get; set; }
}

/// <summary>
/// State information
/// </summary>
public class StateInfo
{
	public string? Name { get; set; }

	public string? IsoCode { get; set; }
}

public class FamilyInfoRequestValidator : AbstractValidator<FamilyInfoRequest>
{
	public FamilyInfoRequestValidator()
	{
		RuleFor(x => x.FatherDetails)
			.SetValidator(new FamilyMemberInfoValidator()!)
			.When(x => x.FatherDetails != null);

		RuleFor(x => x.MotherDetails)
			.SetValidator(new FamilyMemberInfoValidator()!)
			.When(x => x.MotherDetails != null);

		RuleFor(x => x.SpouseDetails)
			.SetValidator(new FamilyMemberInfoValidator()!)
			.When(x => x.SpouseDetails != null);
	}
}

public class FamilyMemberInfoValidator : AbstractValidator<FamilyMemberInfo>
{
	public FamilyMemberInfoValidator()
	{
		RuleFor(x => x.Name).MaximumLength(256);
		RuleFor(x => x.Occupation).MaximumLength(256);
		RuleFor(x => x.MobileNo).MaximumLength(20);
		RuleFor(x => x.Address1).MaximumLength(512);
		RuleFor(x => x.Address2).MaximumLength(512);
		RuleFor(x => x.Address3).MaximumLength(512);
		RuleFor(x => x.City).MaximumLength(128);
		RuleFor(x => x.Pincode).MaximumLength(10);

		RuleFor(x => x.Country)
			.SetValidator(new CountryInfoValidator()!)
			.When(x => x.Country != null);

		RuleFor(x => x.State)
			.SetValidator(new StateInfoValidator()!)
			.When(x => x.State != null);
	}
}

public class CountryInfoValidator : AbstractValidator<CountryInfo>
{
	public CountryInfoValidator()
	{
		RuleFor(x => x.Name).MaximumLength(128);
		RuleFor(x => x.IsoCode).MaximumLength(16);
	}
}

public class StateInfoValidator : AbstractValidator<StateInfo>
{
	public StateInfoValidator()
	{
		RuleFor(x => x.Name).MaximumLength(128);
		RuleFor(x => x.IsoCode).MaximumLength(16);
	}
}
