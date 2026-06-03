using System;
using FluentValidation;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditStudentLookupRequestDtoValidator : AbstractValidator<FinanceAuditStudentLookupRequestDto>
{
	public FinanceAuditStudentLookupRequestDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.MaxResultCount)
			.LessThanOrEqualTo(30)
			.When(x => x.MaxResultCount > 0);
		RuleFor(x => x.Search)
			.MinimumLength(2)
			.When(x => !string.IsNullOrWhiteSpace(x.Search) && !Guid.TryParse(x.Search.Trim(), out _));
	}
}
