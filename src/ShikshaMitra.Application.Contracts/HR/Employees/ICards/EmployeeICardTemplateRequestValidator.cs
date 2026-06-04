using System;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.ICards;

public class EmployeeICardTemplateRequestValidator : AbstractValidator<EmployeeICardTemplateRequest>
{
	public EmployeeICardTemplateRequestValidator()
	{
		RuleFor(x => x.AcademicYearId)
			.NotEmpty()
			.WithMessage("AcademicYearId is required.");

		RuleFor(x => x.MediumStageId)
			.Must(x => x != Guid.Empty)
			.WithMessage("MediumStageId cannot be empty.");

		RuleFor(x => x.Name)
			.NotEmpty()
			.Length(2, 120);

		RuleFor(x => x.Width)
			.InclusiveBetween(100, 2000);

		RuleFor(x => x.Height)
			.InclusiveBetween(100, 2000);

		RuleFor(x => x.BackgroundColor)
			.MaximumLength(32)
			.When(x => x.BackgroundColor != null);

		RuleFor(x => x.BackgroundImageBlobName)
			.MaximumLength(512)
			.When(x => x.BackgroundImageBlobName != null);

		RuleFor(x => x.LogoBlobName)
			.MaximumLength(512)
			.When(x => x.LogoBlobName != null);

		RuleFor(x => x.SchoolName)
			.MaximumLength(180)
			.When(x => x.SchoolName != null);

		RuleFor(x => x.SchoolAddress)
			.MaximumLength(300)
			.When(x => x.SchoolAddress != null);

		RuleFor(x => x.ThemeColor)
			.MaximumLength(32)
			.When(x => x.ThemeColor != null);

		RuleFor(x => x.Orientation)
			.MaximumLength(24);

		RuleFor(x => x.PresetKey)
			.MaximumLength(64)
			.When(x => x.PresetKey != null);

		RuleFor(x => x.SchemaVersion)
			.Equal(1);

		RuleFor(x => x.FieldsJson)
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage("FieldsJson is required.");

		RuleFor(x => x.Orientation)
			.Must(x =>
				string.Equals(x, "Horizontal", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(x, "Vertical", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Orientation must be Horizontal or Vertical.");
	}
}
