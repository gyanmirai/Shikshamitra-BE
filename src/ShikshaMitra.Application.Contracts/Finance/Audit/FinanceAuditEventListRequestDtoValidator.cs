using FluentValidation;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditEventListRequestDtoValidator : AbstractValidator<FinanceAuditEventListRequestDto>
{
	public FinanceAuditEventListRequestDtoValidator()
	{
		RuleFor(x => x.MaxResultCount)
			.LessThanOrEqualTo(100)
			.When(x => x.MaxResultCount > 0);
		RuleFor(x => x.ToDate)
			.GreaterThanOrEqualTo(x => x.FromDate)
			.When(x => x.FromDate.HasValue && x.ToDate.HasValue);
		RuleFor(x => x.Search)
			.MinimumLength(2)
			.When(x => !string.IsNullOrWhiteSpace(x.Search));
	}
}
