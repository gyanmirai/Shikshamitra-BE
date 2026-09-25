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
		RuleFor(x => x.StudentFeeStructureId).NotEmpty().When(x => x.StudentFeeStructureId.HasValue);
		RuleFor(x => x.ConcessionPolicyId).NotEmpty();
		RuleFor(x => x.ConcessionPolicyVersionId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x.Remarks).MaximumLength(2000).When(x => x.Remarks != null);
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(x => x.PreviewFingerprint).NotEmpty().Length(64);
		RuleFor(x => x).Must(x => x.FeeHeadConcessions.Count > 0 || x.DueFeeConcessions.Count > 0)
			.WithMessage("At least one concession line is required.");
		RuleFor(x => x.FeeHeadConcessions)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate fee heads are not allowed.");
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
		RuleForEach(x => x.DueFeeConcessions).SetValidator(new DueFeeConcessionValidator());
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
	}
}

public class UpdateConcessionRequestValidator : AbstractValidator<UpdateConcessionRequestDto>
{
	public UpdateConcessionRequestValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.StudentFeeStructureId).NotEmpty().When(x => x.StudentFeeStructureId.HasValue);
		RuleFor(x => x.ConcessionPolicyId).NotEmpty();
		RuleFor(x => x.ConcessionPolicyVersionId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x.Remarks).MaximumLength(2000).When(x => x.Remarks != null);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(x => x.FeeHeadConcessions)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate fee heads are not allowed.");
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
		RuleForEach(x => x.DueFeeConcessions).SetValidator(new DueFeeConcessionValidator());
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
	}
}

public class FeeHeadConcessionValidator : AbstractValidator<FeeHeadConcessionDto>
{
	public FeeHeadConcessionValidator()
	{
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.BasedOn).Must(x => Enum.IsDefined(typeof(DiscountBasis), x));
		// Policy-fixed modes do not have a caller-entered amount; the domain evaluator
		// enforces a positive value only when the assigned policy permits input.
		RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
	}
}

public class DueFeeConcessionValidator : AbstractValidator<DueFeeConcessionDto>
{
	public DueFeeConcessionValidator()
	{
		RuleFor(x => x.DueFeeId).NotEmpty();
		RuleFor(x => x.FeeHeadId).NotEmpty();
		RuleFor(x => x.Amount).GreaterThan(0m);
	}
}

public class ConcessionRequestPreviewInputValidator : AbstractValidator<ConcessionRequestPreviewInputDto>
{
	public ConcessionRequestPreviewInputValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.StudentFeeStructureId).NotEmpty().When(x => x.StudentFeeStructureId.HasValue);
		RuleFor(x => x.ConcessionPolicyId).NotEmpty();
		RuleFor(x => x.ConcessionPolicyVersionId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x).Must(x => x.FeeHeadConcessions.Count > 0 || x.DueFeeConcessions.Count > 0)
			.WithMessage("At least one concession line is required.");
		RuleFor(x => x.FeeHeadConcessions)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate fee heads are not allowed.");
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
		RuleForEach(x => x.DueFeeConcessions).SetValidator(new DueFeeConcessionValidator());
	}
}

public class UploadConcessionDocumentsValidator : AbstractValidator<UploadConcessionDocumentsDto>
{
	public UploadConcessionDocumentsValidator()
	{
		RuleFor(x => x.Documents)
			.Must(x => x.Select(y => y.DocumentId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate documents are not allowed.");
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
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
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
	}
}

public class ApproveConcessionRequestValidator : AbstractValidator<ApproveConcessionRequestDto>
{
	public ApproveConcessionRequestValidator()
	{
		RuleFor(x => x.Message).MaximumLength(1000).When(x => x.Message != null);
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.PreviewFingerprint).NotEmpty().Length(64);
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
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
			line.RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		});
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
	}
}

public class CreateConcessionDraftValidator : AbstractValidator<CreateConcessionDraftDto>
{
	public CreateConcessionDraftValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.StudentFeeStructureId).NotEmpty().When(x => x.StudentFeeStructureId.HasValue);
		RuleFor(x => x.ConcessionPolicyId).NotEmpty();
		RuleFor(x => x.ConcessionPolicyVersionId).NotEmpty();
		RuleFor(x => x.ConcessionTypeId).NotEmpty();
		RuleFor(x => x.Remarks).MaximumLength(2000).When(x => x.Remarks != null);
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(x => x.FeeHeadConcessions)
			.Must(x => x.Select(y => y.FeeHeadId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate fee heads are not allowed.");
		RuleForEach(x => x.FeeHeadConcessions).SetValidator(new FeeHeadConcessionValidator());
		RuleForEach(x => x.Documents).SetValidator(new UploadConcessionDocumentEntryValidator());
	}
}

public class SubmitConcessionRequestValidator : AbstractValidator<SubmitConcessionRequestDto>
{
	public SubmitConcessionRequestValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.PreviewFingerprint).NotEmpty().Length(64);
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
	}
}

public class CancelConcessionRequestValidator : AbstractValidator<CancelConcessionRequestDto>
{
	public CancelConcessionRequestValidator()
	{
		RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(2000);
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
	}
}

public class PreviewConcessionReversalValidator : AbstractValidator<PreviewConcessionReversalDto>
{
	public PreviewConcessionReversalValidator()
	{
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.Lines).NotEmpty()
			.Must(x => x.Select(y => y.ConcessionDueFeeAllocationId).Distinct().Count() == x.Count)
			.WithMessage("Duplicate reversal allocation lines are not allowed.");
		RuleForEach(x => x.Lines).ChildRules(line =>
		{
			line.RuleFor(x => x.ConcessionDueFeeAllocationId).NotEmpty();
			line.RuleFor(x => x.Amount).GreaterThan(0m);
			line.RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		});
	}
}

public class PreviewConcessionApprovalValidator : AbstractValidator<PreviewConcessionApprovalDto>
{
	public PreviewConcessionApprovalValidator()
	{
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.PreviewFingerprint).NotEmpty().Length(64);
	}
}

public class DeleteConcessionRequestValidator : AbstractValidator<DeleteConcessionRequestDto>
{
	public DeleteConcessionRequestValidator()
	{
		RuleFor(x => x.RequestConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
	}
}

public class DueFeeAllocationValidator : AbstractValidator<DueFeeAllocationDto>
{
	public DueFeeAllocationValidator()
	{
		RuleFor(x => x.DueFeeId).NotEmpty();
		// Amount is a client-side preview only. The server uses the selected due IDs,
		// validates their stamps, and recalculates the final minor-unit distribution.
		RuleFor(x => x.ConcessionAmount).GreaterThanOrEqualTo(0);
	}
}
