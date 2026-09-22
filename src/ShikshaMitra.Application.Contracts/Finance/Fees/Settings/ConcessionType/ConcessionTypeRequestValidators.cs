using FluentValidation;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using System;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType;

public class ConcessionTypeDocumentDtoValidator : AbstractValidator<ConcessionTypeDocumentDto>
{
	public ConcessionTypeDocumentDtoValidator()
	{
		RuleFor(x => x.DocumentName)
			.NotEmpty()
			.MaximumLength(ConcessionTypeConsts.MaxDocumentNameLength);
		RuleFor(x => x.MaxFileSizeBytes)
			.GreaterThan(0)
			.LessThanOrEqualTo(ConcessionTypeConsts.AbsoluteMaxFileSizeBytes);
		RuleFor(x => x.AcceptedFormats)
			.NotEmpty()
			.Must(formats => formats.Distinct().Count() == formats.Count)
			.WithMessage("Accepted formats must be unique.");
		RuleForEach(x => x.AcceptedFormats).IsInEnum();
	}
}

public class ConcessionTypeRequestValidator : AbstractValidator<ConcessionTypeRequest>
{
	public ConcessionTypeRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(ConcessionTypeConsts.MaxNameLength);
		RuleFor(x => x.Description)
			.MaximumLength(ConcessionTypeConsts.MaxDescriptionLength);
		RuleFor(x => x.Documents)
			.Cascade(CascadeMode.Stop)
			.NotNull()
			.Must(documents => documents
				.Select(document => ConcessionTypeConsts.NormalizeNameKey(document.DocumentName))
				.Distinct(StringComparer.Ordinal)
				.Count() == documents.Count)
			.WithMessage("Duplicate document names are not allowed.");
		RuleForEach(x => x.Documents).SetValidator(new ConcessionTypeDocumentDtoValidator());
	}
}

public class ConcessionTypeStatusUpdateRequestValidator : AbstractValidator<ConcessionTypeStatusUpdateRequest>
{
	public ConcessionTypeStatusUpdateRequestValidator()
	{
		RuleFor(x => x.DeactivationReason)
			.NotEmpty()
			.When(x => !x.IsActive)
			.MaximumLength(ConcessionTypeConsts.MaxDeactivationReasonLength);
	}
}
