using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using FluentValidation;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class CreateEmployeePersonalInfoDtoValidator : AbstractValidator<CreateEmployeePersonalInfoDto>
{
	public CreateEmployeePersonalInfoDtoValidator(IClock clock)
	{
		RuleFor(x => x.FirstName)
			.NotEmpty().WithMessage("First name is required.")
			.MaximumLength(256);
		RuleFor(x => x.LastName)
			.NotEmpty().WithMessage("Last name is required.")
			.MaximumLength(256);
		RuleFor(x => x.MobileNo)
			.NotEmpty().WithMessage("Mobile number is required.")
			.Must(IsPhoneNumber).WithMessage("Invalid phone number format.")
			.MaximumLength(15)
			.Matches(@"^[6-9]\d{9}$").WithMessage("Invalid mobile number. Must be 10 digits starting with 6-9.");
		RuleFor(x => x.DateOfJoining).NotEmpty().WithMessage("Date of joining is required.");
		RuleFor(x => x.RoleId).NotEmpty().WithMessage("Role is required.");
		RuleFor(x => x.MiddleName).MaximumLength(256);
		RuleFor(x => x.ShortName).MaximumLength(128);
		RuleFor(x => x.FamilyCaste).MaximumLength(128);
		RuleFor(x => x.Religion).MaximumLength(128);
		RuleFor(x => x.BloodGroup).MaximumLength(10);
		RuleFor(x => x.SecondaryMobileNo)
			.Must(IsPhoneNumber).WithMessage("Invalid phone number format.")
			.MaximumLength(15)
			.When(x => !string.IsNullOrWhiteSpace(x.SecondaryMobileNo));
		RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.")
			.EmailAddress().WithMessage("Invalid email address format.")
			.MaximumLength(256);
		RuleFor(x => x.PanNo).NotEmpty().WithMessage("PAN number is required.")
			.MaximumLength(10)
			.Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$").WithMessage("PAN must be in format AAAAA9999A.");
		RuleFor(x => x.AdharNo).NotEmpty().WithMessage("Aadhaar number is required.")
			.MaximumLength(12)
			.Matches(@"^\d{12}$").WithMessage("Aadhaar number must be 12 digits.");
		RuleFor(x => x.Department).MaximumLength(256);
		RuleFor(x => x.Designation).MaximumLength(256);
		RuleFor(x => x.Qualification).MaximumLength(512);
		RuleFor(x => x.IdentificationMark1).MaximumLength(256);
		RuleFor(x => x.Address1).MaximumLength(512);
		RuleFor(x => x.Address2).MaximumLength(512);
		RuleFor(x => x.Address3).MaximumLength(512);
		RuleFor(x => x.City).MaximumLength(128);
		RuleFor(x => x.ShiftId).Must(s => s.HasValue && s.Value != Guid.Empty)
			.WithMessage("Shift is required.");
		RuleFor(x => x.SecondaryMobileNo)
			.Must((dto, secondary) => string.IsNullOrWhiteSpace(secondary) ||
				NormalizeDigits(dto.MobileNo) != NormalizeDigits(secondary))
			.WithMessage("Secondary mobile number must be different from the primary mobile number.")
			.When(x => !string.IsNullOrWhiteSpace(x.SecondaryMobileNo));

		RuleFor(x => x.DateOfBirth)
			.LessThan(DateOnly.FromDateTime(clock.Now))
			.When(x => x.DateOfBirth.HasValue)
			.WithMessage("Date of birth cannot be in the future.");

		RuleFor(x => x)
			.Must(x => !x.DateOfBirth.HasValue || CalculateAge(x.DateOfBirth.Value, x.DateOfJoining) >= 18)
			.WithMessage("Employee must be at least 18 years old at the time of joining.");

		RuleFor(x => x.DateOfJoining)
			.Must((dto, dateOfJoining) => !dto.DateOfBirth.HasValue || dateOfJoining >= dto.DateOfBirth.Value)
			.WithMessage("Date of joining cannot be before date of birth.");

		RuleFor(x => x.DateOfJoining)
			.LessThanOrEqualTo(DateOnly.FromDateTime(clock.Now.AddYears(1)))
			.WithMessage("Date of joining cannot be more than 1 year in the future.");

		RuleFor(x => x.Pincode)
			.MaximumLength(10)
			.Matches(@"^\d{6}$").When(x => !string.IsNullOrEmpty(x.Pincode))
			.WithMessage("Pincode must be 6 digits.");

		RuleFor(x => x.PolicyOverride!.AcademicYearId)
			.NotEmpty()
			.When(x => x.PolicyOverride != null);
		RuleFor(x => x.PolicyOverride!.AttendancePolicyId)
			.NotEmpty()
			.When(x => x.PolicyOverride != null);
		RuleFor(x => x.PolicyOverride!.PayrollPolicyId)
			.NotEmpty()
			.When(x => x.PolicyOverride != null);
		RuleFor(x => x.PolicyOverride!.LeavePolicyId)
			.NotEmpty()
			.When(x => x.PolicyOverride != null);
		RuleFor(x => x.PolicyOverride!.EffectiveFrom)
			.NotEmpty()
			.When(x => x.PolicyOverride != null);
		RuleFor(x => x.PolicyOverride!.EffectiveTo)
			.Must((dto, effectiveTo) =>
				!effectiveTo.HasValue ||
				dto.PolicyOverride == null ||
				effectiveTo.Value.Date >= dto.PolicyOverride.EffectiveFrom.Date)
			.WithMessage("Policy override effective-to must be on or after effective-from.")
			.When(x => x.PolicyOverride != null);
	}

	private static int CalculateAge(DateOnly dob, DateOnly referenceDate)
	{
		var age = referenceDate.Year - dob.Year;
		if (referenceDate < dob.AddYears(age)) age--;
		return age;
	}

	private static string NormalizeDigits(string? value) =>
		value == null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

	private static bool IsPhoneNumber(string? value) =>
		string.IsNullOrWhiteSpace(value) || new PhoneAttribute().IsValid(value);
}
