using System.Linq;
using FluentValidation;
using ShikshaMitra.Domain.Finance.Policies;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class StandardFeeStructureRequestValidator : AbstractValidator<StandardFeeStructureRequest>
{
	public StandardFeeStructureRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleFor(x => x.FeeHeads).NotNull();
		RuleForEach(x => x.FeeHeads).NotNull().SetValidator(new FeeHeadAssignmentRequestValidator());
		RuleFor(x => x.FeeHeads).Must(x => x == null || x.Count <= 500).WithMessage("A structure supports at most 500 Fee Heads.");
		RuleFor(x => x.ConcessionFeeHeadScope).IsInEnum();
		RuleFor(x => x.ConcessionApplicableFeeHeadIds)
			.NotNull().Must(ids => ids != null && ids.Count == ids.Distinct().Count())
			.WithMessage("Concession fee-head IDs must be distinct.");
		RuleFor(x => x.ConcessionApplicableFeeHeadIds)
			.NotEmpty()
			.When(x => x.ConcessionPolicyId.HasValue
				&& x.ConcessionFeeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads);
	}
}

public class FeeHeadAssignmentRequestValidator : AbstractValidator<FeeHeadAssignmentRequest>
{
	public FeeHeadAssignmentRequestValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x).Must(x => x.IsVariable
            ? !x.Amount.HasValue
            : x.Amount.HasValue && x.Amount >= 0 && x.Amount <= 9999999999.99m && decimal.Round(x.Amount.Value, 2) == x.Amount)
            .WithMessage("Fixed fees require a valid amount; variable fees must not contain a structure amount.");
		RuleFor(x => x.InstallmentOverrides).NotNull();
		RuleForEach(x => x.InstallmentOverrides).NotNull().ChildRules(row =>
		{
			row.RuleFor(x => x.SourcePaymentTermInstallmentId).NotEmpty();
			row.RuleFor(x => x.Name).NotEmpty();
			row.RuleFor(x => x.Sequence).GreaterThan(0);
		});
		RuleFor(x => x).Must(x => x.PaymentTermId.HasValue == x.PaymentTermVersionId.HasValue)
			.WithMessage("Select both a Payment Term and its published version.");
		RuleFor(x => x.PayableMonth).IsInEnum();
		RuleFor(x => x.ScheduleMode).IsInEnum();
	}
}
