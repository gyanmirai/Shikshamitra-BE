using FluentValidation;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions;

public class CreateConcessionRequestValidator : AbstractValidator<CreateConcessionRequestDto>
{
	public CreateConcessionRequestValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x.Remarks).MaximumLength(2000).When(x => x.Remarks != null);
		RuleFor(x => x.FeeHeadConcessions).NotEmpty();
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
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
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
	}
}

public class FeeHeadConcessionValidator : AbstractValidator<FeeHeadConcessionDto>
{
	public FeeHeadConcessionValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.BasedOn).InclusiveBetween(0, 1);
		RuleFor(x => x.Amount).GreaterThan(0);
	}
}

public class UploadConcessionDocumentsValidator : AbstractValidator<UploadConcessionDocumentsDto>
{
	public UploadConcessionDocumentsValidator()
	{
		RuleFor(x => x.Documents).NotEmpty();
		RuleForEach(x => x.Documents).ChildRules(doc =>
		{
			doc.RuleFor(d => d.DocumentId).NotEmpty();
			doc.RuleFor(d => d.Url).NotEmpty().MaximumLength(2048);
		});
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
	}
}

public class FeeHeadApprovalValidator : AbstractValidator<FeeHeadApprovalDto>
{
	public FeeHeadApprovalValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.ApprovedConcessionAmount).GreaterThanOrEqualTo(0);
		RuleForEach(x => x.DueFeeAllocations).SetValidator(new DueFeeAllocationValidator());
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
