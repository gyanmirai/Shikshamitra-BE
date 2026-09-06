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
		RuleForEach(x => x.FeeHeads).SetValidator(new FeeHeadAssignmentRequestValidator());
		RuleFor(x => x.ConcessionFeeHeadScope).IsInEnum();
		RuleFor(x => x.ConcessionApplicableFeeHeadIds)
			.Must(ids => ids.Count == ids.Distinct().Count())
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
		RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.PayableMonth).IsInEnum();
		RuleFor(x => x.ScheduleMode).IsInEnum();
	}
}
