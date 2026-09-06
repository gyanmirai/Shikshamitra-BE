using FluentValidation;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using System;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Concessions;

public class CreateConcessionRequestValidator : AbstractValidator<CreateConcessionRequestDto>
{
	public CreateConcessionRequestValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x.Remarks).MaximumLength(2000).When(x => x.Remarks != null);
		RuleFor(x => x.FeeHeadConcessions).NotEmpty();
		RuleFor(x => x.FeeHeadConcessions)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate fee heads are not allowed.");
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
	}
}

public class UpdateConcessionRequestValidator : AbstractValidator<UpdateConcessionRequestDto>
{
	public UpdateConcessionRequestValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x.Remarks).MaximumLength(2000).When(x => x.Remarks != null);
		RuleFor(x => x.FeeHeadConcessions).NotEmpty();
		RuleFor(x => x.FeeHeadConcessions)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate fee heads are not allowed.");
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
	}
}

public class FeeHeadConcessionValidator : AbstractValidator<FeeHeadConcessionDto>
{
	public FeeHeadConcessionValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.BasedOn).Must(x => Enum.IsDefined(typeof(DiscountBasis), x));
		RuleFor(x => x.Amount).GreaterThan(0);
	}
}

public class UploadConcessionDocumentsValidator : AbstractValidator<UploadConcessionDocumentsDto>
{
	public UploadConcessionDocumentsValidator()
	{
		RuleFor(x => x.Documents).NotEmpty();
		RuleFor(x => x.Documents)
			.Must(x => x.Select(y => y.DocumentId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate documents are not allowed.");
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
	}
}

public class UploadConcessionDocumentEntryValidator : AbstractValidator<UploadConcessionDocumentEntryDto>
{
	public UploadConcessionDocumentEntryValidator()
	{
		RuleFor(x => x.DocumentId).NotEmpty();
		RuleFor(x => x.Url).NotEmpty().MaximumLength(512);
		RuleFor(x => x.Status).Equal(1);
	}
}

public class RejectConcessionRequestValidator : AbstractValidator<RejectConcessionRequestDto>
{
	public RejectConcessionRequestValidator()
	{
		RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(2000);
	}
}

public class ApproveConcessionRequestValidator : AbstractValidator<ApproveConcessionRequestDto>
{
	public ApproveConcessionRequestValidator()
	{
		RuleFor(x => x.Message).MaximumLength(1000).When(x => x.Message != null);
		RuleForEach(x => x.FeeHeadApprovals!).SetValidator(new FeeHeadApprovalValidator())
			.When(x => x.FeeHeadApprovals != null);
		RuleFor(x => x.FeeHeadApprovals!)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.When(x => x.FeeHeadApprovals != null)
			.WithMessage("Duplicate fee-head approval groups are not allowed.");
	}
}

public class FeeHeadApprovalValidator : AbstractValidator<FeeHeadApprovalDto>
{
	public FeeHeadApprovalValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.ApprovedConcessionAmount).GreaterThanOrEqualTo(0);
		RuleForEach(x => x.DueFeeAllocations).SetValidator(new DueFeeAllocationValidator());
		RuleFor(x => x.DueFeeAllocations)
			.Must(x => x.Select(y => y.DueFeeId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate due-fee allocations are not allowed.");
	}
}

public class ReverseConcessionRequestValidator : AbstractValidator<ReverseConcessionRequestDto>
{
	public ReverseConcessionRequestValidator()
	{
		RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(2000);
		RuleFor(x => x.Lines).NotEmpty()
			.Must(x => x.Select(y => y.ConcessionDueFeeAllocationId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate reversal allocation lines are not allowed.");
		RuleForEach(x => x.Lines).ChildRules(line =>
		{
			line.RuleFor(x => x.ConcessionDueFeeAllocationId).NotEmpty();
			line.RuleFor(x => x.Amount).GreaterThan(0m);
		});
	}
}

public class DueFeeAllocationValidator : AbstractValidator<DueFeeAllocationDto>
{
	public DueFeeAllocationValidator()
	{
		RuleFor(x => x.DueFeeId).NotEmpty();
		RuleFor(x => x.ConcessionAmount).GreaterThan(0);
	}
}
