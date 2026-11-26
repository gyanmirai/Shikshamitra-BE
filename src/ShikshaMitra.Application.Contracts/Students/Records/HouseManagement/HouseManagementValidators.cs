using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.Students.Records.HouseManagement;

public class HouseManagementQueryValidator : AbstractValidator<HouseManagementQuery>
{
	public HouseManagementQueryValidator()
	{
		RuleFor(x => x.AcademicMediumId).NotEmpty();
		RuleFor(x => x.SkipCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaxResultCount).InclusiveBetween(1, 100);
		RuleFor(x => x.Filter).MaximumLength(150);
		RuleFor(x => x.Sorting).Must(x => string.IsNullOrEmpty(x) || new[] { "name asc", "name desc", "studentName asc", "studentName desc", "standardName asc", "standardName desc", "houseName asc", "houseName desc" }.Contains(x));
		RuleFor(x => x.StandardId).NotEqual(Guid.Empty).When(x => x.StandardId.HasValue);
		RuleFor(x => x.DivisionId).NotEqual(Guid.Empty).When(x => x.DivisionId.HasValue);
		RuleFor(x => x.HouseId).NotEqual(Guid.Empty).When(x => x.HouseId.HasValue);
		RuleFor(x => x.AssignedOnly).Equal(false).When(x => x.UnassignedOnly);
	}
}

public class SaveHouseRequestValidator : AbstractValidator<SaveHouseRequest>
{
	public SaveHouseRequestValidator()
	{
		RuleFor(x => x.AcademicMediumId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Color).Must(HouseManagementInputRules.IsColor).WithMessage("Use a six-digit hexadecimal color, for example #2468ab.");
		RuleFor(x => x.LogoUrl).Must(HouseManagementInputRules.IsLogoUrl).WithMessage("Use an HTTP(S) or root-relative image URL.");
		RuleFor(x => x.Description).MaximumLength(1000);
		RuleFor(x => x.ConcurrencyStamp).MaximumLength(40);
	}
}

public class HouseAssignmentRequestValidator : AbstractValidator<HouseAssignmentRequest>
{
	public HouseAssignmentRequestValidator()
	{
		RuleFor(x => x.AcademicMediumId).NotEmpty();
		RuleFor(x => x.DivisionId).NotEmpty().WithMessage("Select a division to change house membership.");
		RuleFor(x => x.HouseId).NotEqual(Guid.Empty).When(x => x.HouseId.HasValue);
		RuleFor(x => x.TargetScope).Must(value => value is null or HouseAssignmentRequest.DivisionScope);
		When(x => x.TargetScope == HouseAssignmentRequest.DivisionScope, () =>
		{
			RuleFor(x => x.HouseId).NotNull().WithMessage("Select a destination house for the division.");
			RuleFor(x => x.EnrollmentIds).NotNull().Empty();
		});
		When(x => x.TargetScope == null, () =>
		{
			RuleFor(x => x.IncludeAssigned).Equal(false);
			RuleFor(x => x.EnrollmentIds).NotNull().Must(ids => ids != null &&
				ids.Count is >= 1 and <= HouseAssignmentRequest.MaxEnrollmentIds &&
				ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count);
		});
		RuleFor(x => x.PreviewToken).MaximumLength(64);
	}
}

public class ReplaceStudentLeaderRequestValidator : AbstractValidator<ReplaceStudentLeaderRequest>
{
	public ReplaceStudentLeaderRequestValidator()
	{
		RuleFor(x => x.AcademicMediumId).NotEmpty();
		RuleFor(x => x.EnrollmentId).NotEqual(Guid.Empty).When(x => x.EnrollmentId.HasValue);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
	}
}
