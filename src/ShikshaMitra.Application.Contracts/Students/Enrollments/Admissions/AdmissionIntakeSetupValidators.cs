using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class AdmissionInquiryIntakeRulesDtoValidator : AbstractValidator<AdmissionInquiryIntakeRulesDto>
{
	public AdmissionInquiryIntakeRulesDtoValidator()
	{
		RuleFor(item => item.DuplicateRule).IsInEnum();
		RuleFor(item => item.ApplicationStartRequirement).IsInEnum();
		RuleFor(item => item.FirstContactDueInHours).InclusiveBetween(1, 720).When(item => item.FirstContactDueInHours.HasValue);
		RuleFor(item => item.QualificationDueInDays).InclusiveBetween(1, 365).When(item => item.QualificationDueInDays.HasValue);
		RuleFor(item => item.FollowUpDueInDays).InclusiveBetween(1, 90);
		RuleFor(item => item.MaxOpenFollowUps).InclusiveBetween(1, 20);
		RuleFor(item => item.AllowedSources).NotNull().Must(items => items.Count <= 13 &&
			items.All(Enum.IsDefined) && items.Distinct().Count() == items.Count);
		RuleFor(item => item.RequireSourceDetailFor).NotNull().Must(items => items.Count <= 13 &&
			items.All(Enum.IsDefined) && items.Distinct().Count() == items.Count);
		RuleFor(item => item).Must(item => item.AllowedSources.Count == 0 ||
			item.RequireSourceDetailFor.All(item.AllowedSources.Contains))
			.WithMessage("Sources requiring details must be included in allowed sources.");
	}
}

public sealed class AdmissionCounsellorAssignmentRulesDtoValidator : AbstractValidator<AdmissionCounsellorAssignmentRulesDto>
{
	public AdmissionCounsellorAssignmentRulesDtoValidator()
	{
		RuleFor(item => item.CounsellorUserIds).NotNull().Must(items => items.Count is >= 1 and <= 50 &&
			items.All(id => id != Guid.Empty) && items.Distinct().Count() == items.Count);
		RuleFor(item => item.MaxActiveCasesPerCounsellor).InclusiveBetween(1, 10000);
		RuleFor(item => item.FallbackManagerUserId).NotEmpty();
	}
}

public class CreateAdmissionIntakeSetupDtoValidator : AbstractValidator<CreateAdmissionIntakeSetupDto>
{
	public CreateAdmissionIntakeSetupDtoValidator()
	{
		RuleFor(item => item.Name).NotEmpty().MaximumLength(128);
		RuleFor(item => item.InquiryRules).NotNull().SetValidator(new AdmissionInquiryIntakeRulesDtoValidator());
		RuleFor(item => item.CounsellorAssignmentRules).NotNull()
			.SetValidator(new AdmissionCounsellorAssignmentRulesDtoValidator());
	}
}

public sealed class UpdateAdmissionIntakeSetupDtoValidator : AbstractValidator<UpdateAdmissionIntakeSetupDto>
{
	public UpdateAdmissionIntakeSetupDtoValidator()
	{
		Include(new CreateAdmissionIntakeSetupDtoValidator());
		RuleFor(item => item.ConcurrencyStamp).NotEmpty().MaximumLength(40);
	}
}

public sealed class ArchiveAdmissionIntakeSetupDtoValidator : AbstractValidator<ArchiveAdmissionIntakeSetupDto>
{
	public ArchiveAdmissionIntakeSetupDtoValidator() =>
		RuleFor(item => item.ConcurrencyStamp).NotEmpty().MaximumLength(40);
}
