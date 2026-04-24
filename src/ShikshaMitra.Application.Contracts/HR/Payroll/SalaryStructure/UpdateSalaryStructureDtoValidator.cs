using FluentValidation;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class UpdateSalaryStructureDtoValidator : AbstractValidator<UpdateSalaryStructureDto>
{
	public UpdateSalaryStructureDtoValidator()
	{
		RuleFor(x => x.EarningComponents).NotEmpty();
		RuleFor(x => x.DeductionComponents).NotEmpty();

		RuleForEach(x => x.EarningComponents).SetValidator(new SalaryComponentDtoValidator());
		RuleForEach(x => x.DeductionComponents).SetValidator(new SalaryComponentDtoValidator());
	}
}
