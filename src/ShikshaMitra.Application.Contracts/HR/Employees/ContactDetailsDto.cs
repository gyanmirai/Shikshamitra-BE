using FluentValidation;

namespace ShikshaMitra.HR.Employees;

public class ContactDetailsDto
{
	public string? Name { get; set; }

	public string? Occupation { get; set; }

	public string? ContactNo { get; set; }

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public string? City { get; set; }

	public string? StateName { get; set; }

	public string? StateIsoCode { get; set; }

	public string? CountryName { get; set; }

	public string? CountryIsoCode { get; set; }

	public string? PinCode { get; set; }
}

public class ContactDetailsDtoValidator : AbstractValidator<ContactDetailsDto>
{
	public ContactDetailsDtoValidator()
	{
		RuleFor(x => x.Name)
			.MaximumLength(256)
			.Matches(@"^[a-zA-Z\s\.\-']+$")
			.WithMessage("Name contains invalid characters")
			.When(x => !string.IsNullOrWhiteSpace(x.Name));

		RuleFor(x => x.Occupation)
			.MaximumLength(256)
			.Matches(@"^[a-zA-Z0-9\s\.\-,]+$")
			.WithMessage("Occupation contains invalid characters")
			.When(x => !string.IsNullOrWhiteSpace(x.Occupation));

		RuleFor(x => x.ContactNo)
			.MaximumLength(20)
			.Matches(@"^\+?[0-9\s\-()]+$")
			.WithMessage("Invalid phone number format")
			.When(x => !string.IsNullOrWhiteSpace(x.ContactNo));

		RuleFor(x => x.Address1).MaximumLength(512);
		RuleFor(x => x.Address2).MaximumLength(512);
		RuleFor(x => x.Address3).MaximumLength(512);

		RuleFor(x => x.City)
			.MaximumLength(128)
			.Matches(@"^[a-zA-Z\s\.\-']+$")
			.WithMessage("City contains invalid characters")
			.When(x => !string.IsNullOrWhiteSpace(x.City));

		RuleFor(x => x.StateName).MaximumLength(128);
		RuleFor(x => x.CountryName).MaximumLength(128);

		RuleFor(x => x.StateIsoCode)
			.MaximumLength(3)
			.Matches(@"^[A-Z]{2,3}$")
			.WithMessage("State ISO code must be 2-3 uppercase letters")
			.When(x => !string.IsNullOrWhiteSpace(x.StateIsoCode));

		RuleFor(x => x.CountryIsoCode)
			.MaximumLength(3)
			.Matches(@"^[A-Z]{2,3}$")
			.WithMessage("Country ISO code must be 2-3 uppercase letters")
			.When(x => !string.IsNullOrWhiteSpace(x.CountryIsoCode));

		RuleFor(x => x.PinCode)
			.MaximumLength(10)
			.Matches(@"^[0-9]{6,10}$")
			.WithMessage("PIN code must be 6-10 digits")
			.When(x => !string.IsNullOrWhiteSpace(x.PinCode));
	}
}
