using FluentValidation;
using System;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Employees;

public class UpdateEmployeeBasicInfoDtoValidator : AbstractValidator<UpdateEmployeeBasicInfoDto>
{
	public UpdateEmployeeBasicInfoDtoValidator(IClock clock)
	{
		RuleFor(x => x.FirstName).MaximumLength(256);
		RuleFor(x => x.LastName).MaximumLength(256);
		RuleFor(x => x.MiddleName).MaximumLength(256);
		RuleFor(x => x.ShortName).MaximumLength(128);
		RuleFor(x => x.FamilyCast).MaximumLength(128);
		RuleFor(x => x.Religion).MaximumLength(128);
		RuleFor(x => x.BloodGroup).MaximumLength(20);

		RuleFor(x => x.DateOfBirth)
			.Must(x => !x.HasValue || x.Value < DateOnly.FromDateTime(clock.Now))
			.WithMessage("Date of birth cannot be in the future.");

		RuleFor(x => x)
			.Must(x => !x.DateOfJoining.HasValue ||
			           !x.DateOfBirth.HasValue ||
			           CalculateAge(x.DateOfBirth.Value, x.DateOfJoining.Value) >= 18)
			.WithMessage("Employee must be at least 18 years old at the time of joining.");

		RuleFor(x => x.MobileNo)
			.MaximumLength(20)
			.Matches(@"^\+?[\d\s\-()]+$")
			.When(x => !string.IsNullOrEmpty(x.MobileNo));

		RuleFor(x => x.SecondaryMobileNo)
			.MaximumLength(20)
			.Matches(@"^\+?[\d\s\-()]+$")
			.When(x => !string.IsNullOrEmpty(x.SecondaryMobileNo));

		RuleFor(x => x.Email)
			.MaximumLength(256)
			.EmailAddress()
			.When(x => !string.IsNullOrEmpty(x.Email));

		RuleFor(x => x.PanNo)
			.NotEmpty().WithMessage("PAN number is required.")
			.Must(BeValidPan).WithMessage("PAN must be in format AAAAA9999A.");

		RuleFor(x => x.AdharNo)
			.NotEmpty().WithMessage("Aadhaar number is required.")
			.Matches(@"^\d{12}$").WithMessage("Aadhaar number must be 12 digits.");

		RuleFor(x => x.Department).MaximumLength(256);
		RuleFor(x => x.Designation).MaximumLength(256);
		RuleFor(x => x.Qualification).MaximumLength(512);

		RuleFor(x => x.RoleId).NotEmpty();

		RuleFor(x => x.IdentificationMark1).MaximumLength(256);
		RuleFor(x => x.Address1).MaximumLength(512);
		RuleFor(x => x.Address2).MaximumLength(512);
		RuleFor(x => x.Address3).MaximumLength(512);
		RuleFor(x => x.City).MaximumLength(128);

		RuleFor(x => x.PinCode)
			.MaximumLength(20)
			.Matches(@"^[0-9]{6,10}$")
			.When(x => !string.IsNullOrEmpty(x.PinCode));
	}

	private static bool BeValidPan(string? panNo) =>
		!string.IsNullOrWhiteSpace(panNo) &&
		System.Text.RegularExpressions.Regex.IsMatch(panNo.Trim().ToUpperInvariant(), @"^[A-Z]{5}[0-9]{4}[A-Z]$");

	private static int CalculateAge(DateOnly dateOfBirth, DateOnly dateOfJoining)
	{
		var age = dateOfJoining.Year - dateOfBirth.Year;
		if (dateOfJoining < dateOfBirth.AddYears(age))
		{
			age--;
		}

		return age;
	}
}
