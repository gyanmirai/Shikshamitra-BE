using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class UpdateEmployeePersonalInfoDtoValidator : AbstractValidator<UpdateEmployeePersonalInfoDto>
{
	public UpdateEmployeePersonalInfoDtoValidator()
	{
		RuleFor(x => x.FirstName).MaximumLength(256);
		RuleFor(x => x.LastName).MaximumLength(256);

		RuleFor(x => x.MobileNo)
			.MaximumLength(15)
			.Matches(@"^[6-9]\d{9}$")
			.When(x => !string.IsNullOrEmpty(x.MobileNo));

		RuleFor(x => x.RoleId).NotEmpty();

		RuleFor(x => x.ShiftId).Must(id => id.HasValue && id.Value != Guid.Empty)
			.WithMessage("Shift is required.");
		RuleFor(x => x.MiddleName).MaximumLength(256);
		RuleFor(x => x.ShortName).MaximumLength(128);
		RuleFor(x => x.FamilyCaste).MaximumLength(128);
		RuleFor(x => x.Religion).MaximumLength(128);
		RuleFor(x => x.BloodGroup).MaximumLength(10);

		RuleFor(x => x.SecondaryMobileNo)
			.MaximumLength(15)
			.Matches(@"^\+?[\d\s\-()]+$")
			.When(x => !string.IsNullOrEmpty(x.SecondaryMobileNo));

		RuleFor(x => x.SecondaryMobileNo)
			.Must((dto, secondary) => string.IsNullOrWhiteSpace(secondary) || string.IsNullOrWhiteSpace(dto.MobileNo) ||
				NormalizeDigits(dto.MobileNo) != NormalizeDigits(secondary))
			.WithMessage("Secondary mobile number must be different from the primary mobile number.")
			.When(x => !string.IsNullOrWhiteSpace(x.SecondaryMobileNo) && !string.IsNullOrWhiteSpace(x.MobileNo));

		RuleFor(x => x.Email)
			.NotEmpty().WithMessage("Email is required.")
			.MaximumLength(256)
			.EmailAddress();

		RuleFor(x => x.PanNo).NotEmpty().WithMessage("PAN number is required.")
			.Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$").WithMessage("PAN must be in format AAAAA9999A.");
		RuleFor(x => x.AdharNo).NotEmpty().WithMessage("Aadhaar number is required.")
			.Matches(@"^\d{12}$").WithMessage("Aadhaar number must be 12 digits.");

		RuleFor(x => x.Department).MaximumLength(256);
		RuleFor(x => x.Designation).MaximumLength(256);
		RuleFor(x => x.Qualification).MaximumLength(512);
		RuleFor(x => x.IdentificationMark1).MaximumLength(256);
		RuleFor(x => x.Address1).MaximumLength(512);
		RuleFor(x => x.Address2).MaximumLength(512);
		RuleFor(x => x.Address3).MaximumLength(512);
		RuleFor(x => x.City).MaximumLength(128);

		RuleFor(x => x.Pincode)
			.MaximumLength(10)
			.Matches(@"^\d{6}$")
			.When(x => !string.IsNullOrEmpty(x.Pincode));
	}

	private static string NormalizeDigits(string? value) =>
		value == null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
}
