using System;
using FluentValidation;

namespace ShikshaMitra.Library.Charges;

public class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequest>
{
	public CreateChargeRequestValidator()
	{
		RuleFor(x => x.MemberId).NotEmpty();
		RuleFor(x => x.ChargeCategoryId).NotEmpty();
		RuleFor(x => x.BookIssueId).NotEqual(Guid.Empty).When(x => x.BookIssueId.HasValue);
		RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, true);
		When(x => !x.AcademicYearId.HasValue && !x.SchoolChargeDate.HasValue && !x.SchoolDueDate.HasValue, () =>
		{
			RuleFor(x => x.ChargeDate).NotEmpty();
			RuleFor(x => x.DueDate).NotEmpty().GreaterThanOrEqualTo(x => x.ChargeDate.Date);
		});
		When(x => x.AcademicYearId.HasValue || x.SchoolChargeDate.HasValue || x.SchoolDueDate.HasValue, () =>
		{
			RuleFor(x => x.AcademicYearId).NotEmpty();
			RuleFor(x => x.SchoolChargeDate).NotEmpty();
			RuleFor(x => x.SchoolDueDate).NotEmpty().Must((request, value) => value >= request.SchoolChargeDate);
			RuleFor(x => x.Remarks).NotEmpty();
		});
		RuleFor(x => x.Remarks).MaximumLength(500);
	}
}

public class UpdateChargeRequestValidator : AbstractValidator<UpdateChargeRequest>
{
	public UpdateChargeRequestValidator()
	{
		RuleFor(x => x.Amount!.Value).GreaterThan(0).PrecisionScale(18, 2, true).When(x => x.Amount.HasValue);
		RuleFor(x => x.DueDate!.Value).NotEmpty().When(x => x.DueDate.HasValue);
		RuleFor(x => x.SchoolDueDate!.Value).NotEmpty().When(x => x.SchoolDueDate.HasValue);
		RuleFor(x => x.DueDate).Null().When(x => x.SchoolDueDate.HasValue);
		RuleFor(x => x.PaymentStatus).Must(value => !value.HasValue || value == (int)ChargePaymentStatus.Pending)
			.WithErrorCode(LibraryFinanceErrorCodes.SettlementRequiresFinance)
			.WithMessage("Record library charge payments and waivers through Finance.");
		RuleFor(x => x.Remarks).MaximumLength(500);
		RuleFor(x => x.PaidDate).Null()
			.WithErrorCode(LibraryFinanceErrorCodes.SettlementRequiresFinance)
			.WithMessage("Payment dates are recorded by Finance.");
	}
}
