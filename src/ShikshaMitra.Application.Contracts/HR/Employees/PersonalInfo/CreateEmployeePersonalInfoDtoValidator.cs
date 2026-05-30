using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class CreateEmployeePersonalInfoDtoValidator : AbstractValidator<CreateEmployeePersonalInfoDto>
{
	public CreateEmployeePersonalInfoDtoValidator()
	{
		RuleFor(x => x.FirstName).NotEmpty().MaximumLength(256);
		RuleFor(x => x.LastName).NotEmpty().MaximumLength(256);
		RuleFor(x => x.MobileNo).NotEmpty().MaximumLength(15)
			.Matches(@"^[6-9]\d{9}$").WithMessage("Invalid mobile number. Must be 10 digits starting with 6-9.");
		RuleFor(x => x.DateOfJoining).NotEmpty();
		RuleFor(x => x.RoleId).NotEmpty();
		RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.")
			.EmailAddress().WithMessage("Invalid email address format.");
		RuleFor(x => x.PanNo).NotEmpty().WithMessage("PAN number is required.")
			.Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$").WithMessage("PAN must be in format AAAAA9999A.");
		RuleFor(x => x.AdharNo).NotEmpty().WithMessage("Aadhaar number is required.")
			.Matches(@"^\d{12}$").WithMessage("Aadhaar number must be 12 digits.");
		RuleFor(x => x.ShiftId).Must(s => s.HasValue && s.Value != Guid.Empty)
			.WithMessage("Shift is required.");
		RuleFor(x => x.SecondaryMobileNo)
			.Must((dto, secondary) => string.IsNullOrWhiteSpace(secondary) ||
				NormalizeDigits(dto.MobileNo) != NormalizeDigits(secondary))
			.WithMessage("Secondary mobile number must be different from the primary mobile number.")
			.When(x => !string.IsNullOrWhiteSpace(x.SecondaryMobileNo));

		RuleFor(x => x.DateOfBirth)
			.LessThan(DateOnly.FromDateTime(System.DateTime.UtcNow))
			.When(x => x.DateOfBirth.HasValue)
			.WithMessage("Date of birth cannot be in the future.");

		RuleFor(x => x)
			.Must(x => !x.DateOfBirth.HasValue || CalculateAge(x.DateOfBirth.Value, x.DateOfJoining) >= 18)
			.WithMessage("Employee must be at least 18 years old at the time of joining.");

		RuleFor(x => x.DateOfJoining)
			.LessThanOrEqualTo(DateOnly.FromDateTime(System.DateTime.UtcNow.AddYears(1)))
			.WithMessage("Date of joining cannot be more than 1 year in the future.");

		RuleFor(x => x.Pincode)
			.Matches(@"^\d{6}$").When(x => !string.IsNullOrEmpty(x.Pincode))
			.WithMessage("Pincode must be 6 digits.");
	}

	private static int CalculateAge(DateOnly dob, DateOnly referenceDate)
	{
		var age = referenceDate.Year - dob.Year;
		if (referenceDate < dob.AddYears(age)) age--;
		return age;
	}

	private static string NormalizeDigits(string? value) =>
		value == null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
}
