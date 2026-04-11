using FluentValidation;

namespace ShikshaMitra.HR.Employees;

public class UpdateEmployeeBasicInfoDtoValidator : AbstractValidator<UpdateEmployeeBasicInfoDto>
{
	public UpdateEmployeeBasicInfoDtoValidator()
	{
		RuleFor(x => x.FirstName).MaximumLength(256);
		RuleFor(x => x.LastName).MaximumLength(256);
		RuleFor(x => x.MiddleName).MaximumLength(256);
		RuleFor(x => x.ShortName).MaximumLength(128);
		RuleFor(x => x.FamilyCast).MaximumLength(128);
		RuleFor(x => x.Religion).MaximumLength(128);
		RuleFor(x => x.BloodGroup).MaximumLength(20);

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
}
