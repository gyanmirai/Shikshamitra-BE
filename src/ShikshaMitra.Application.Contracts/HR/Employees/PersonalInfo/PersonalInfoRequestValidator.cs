using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class PersonalInfoRequestValidator : AbstractValidator<PersonalInfoRequest>
{
	public PersonalInfoRequestValidator()
	{
		RuleFor(x => x.FirstName)
			.NotEmpty()
			.MaximumLength(256);

		RuleFor(x => x.MiddleName).MaximumLength(256);

		RuleFor(x => x.LastName)
			.NotEmpty()
			.MaximumLength(256);

		RuleFor(x => x.ShortName).MaximumLength(128);
		RuleFor(x => x.Religion).MaximumLength(128);
		RuleFor(x => x.FamilyCaste).MaximumLength(128);

		RuleFor(x => x.Email)
			.EmailAddress()
			.MaximumLength(256)
			.When(x => !string.IsNullOrWhiteSpace(x.Email));

		RuleFor(x => x.MobileNo)
			.NotEmpty()
			.MaximumLength(15)
			.Matches(@"^[6-9]\d{9}$")
			.WithMessage("Invalid mobile number. Must be 10 digits starting with 6-9.");

		RuleFor(x => x.SecondaryMobileNo)
			.MaximumLength(15)
			.Must((request, secondaryMobileNo) =>
			{
				if (string.IsNullOrWhiteSpace(secondaryMobileNo) || string.IsNullOrWhiteSpace(request.MobileNo))
				{
					return true;
				}

				var primaryDigits = NormalizeDigits(request.MobileNo);
				var secondaryDigits = NormalizeDigits(secondaryMobileNo);
				return primaryDigits.Length == 0 || secondaryDigits.Length == 0 || primaryDigits != secondaryDigits;
			})
			.WithMessage("Secondary mobile number must be different from the primary mobile number.");

		RuleFor(x => x.PanNo)
			.NotEmpty()
			.MaximumLength(10)
			.Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
			.WithMessage("PAN must be in format AAAAA9999A.");

		RuleFor(x => x.AdharNo)
			.NotEmpty()
			.MaximumLength(12)
			.Matches(@"^\d{12}$")
			.WithMessage("Aadhaar number must be 12 digits.");

		RuleFor(x => x.Doj).NotEmpty();
		RuleFor(x => x.RoleId).NotEmpty();
		RuleFor(x => x.ShiftId).NotEmpty();

		RuleFor(x => x.Address1).MaximumLength(512);
		RuleFor(x => x.Address2).MaximumLength(512);
		RuleFor(x => x.Address3).MaximumLength(512);
		RuleFor(x => x.Designation).MaximumLength(256);
		RuleFor(x => x.Qualification).MaximumLength(512);
		RuleFor(x => x.IdentificationMark1).MaximumLength(256);
		RuleFor(x => x.City).MaximumLength(128);

		RuleFor(x => x.PinCode)
			.MaximumLength(10)
			.Matches(@"^$|^\d{6}$")
			.WithMessage("Pincode must be 6 digits.")
			.When(x => x.PinCode != null);

		RuleFor(x => x.Country)
			.SetValidator(new CountryInfoValidator()!)
			.When(x => x.Country != null);

		RuleFor(x => x.State)
			.SetValidator(new StateInfoValidator()!)
			.When(x => x.State != null);
	}

	private static string NormalizeDigits(string value) =>
		new(value.Where(char.IsDigit).ToArray());
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
