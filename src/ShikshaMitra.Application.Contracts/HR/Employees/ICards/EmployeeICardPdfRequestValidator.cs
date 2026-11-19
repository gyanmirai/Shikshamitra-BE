using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.ICards;

public class EmployeeICardPdfRequestValidator : AbstractValidator<EmployeeICardPdfRequest>
{
	public EmployeeICardPdfRequestValidator()
	{
		RuleFor(x => x.EmployeeIds)
			.NotEmpty()
			.WithMessage("At least one staff is required.");

		RuleFor(x => x.EmployeeIds)
			.Must(x => x.All(id => id != Guid.Empty))
			.WithMessage("EmployeeIds cannot contain empty values.");

		RuleFor(x => x.CardsPerPage)
			.InclusiveBetween(1, 10)
			.When(x => x.CardsPerPage.HasValue);
	}
}
