using System;
using FluentValidation;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class StudentOnboardingValidator_Tests
{
	[Fact]
	public void Finalization_requires_concurrency_and_idempotency_tokens()
	{
		var result = new FinalizeAdmissionApplicationDtoValidator().Validate(
			new FinalizeAdmissionApplicationDto());

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(FinalizeAdmissionApplicationDto.ConcurrencyStamp));
		result.Errors.ShouldContain(error => error.PropertyName == nameof(FinalizeAdmissionApplicationDto.IdempotencyKey));
	}

	[Fact]
	public void Admission_decisions_require_concurrency_and_limit_the_audit_reason()
	{
		var missingStamp = new AdmissionApplicationDecisionDtoValidator().Validate(
			new AdmissionApplicationDecisionDto());
		missingStamp.Errors.ShouldContain(error =>
			error.PropertyName == nameof(AdmissionApplicationDecisionDto.ConcurrencyStamp));

		var excessiveReason = new AdmissionApplicationDecisionDtoValidator().Validate(
			new AdmissionApplicationDecisionDto
			{
				ConcurrencyStamp = "current",
				Reason = new string('x', 513)
			});
		excessiveReason.Errors.ShouldContain(error =>
			error.PropertyName == nameof(AdmissionApplicationDecisionDto.Reason));
	}

	[Fact]
	public void Admission_form_and_workflow_commands_require_concurrency()
	{
		var formResult = new UpdateAdmissionApplicationFormValuesDtoValidator().Validate(
			new UpdateAdmissionApplicationFormValuesDto());
		formResult.Errors.ShouldContain(error =>
			error.PropertyName == nameof(UpdateAdmissionApplicationFormValuesDto.ConcurrencyStamp));

		var workflowResult = new AdvanceAdmissionWorkflowDtoValidator().Validate(
			new AdvanceAdmissionWorkflowDto
			{
				ExpectedCurrentStageInstanceId = Guid.NewGuid(),
				IdempotencyKey = "workflow-command"
			});
		workflowResult.Errors.ShouldContain(error =>
			error.PropertyName == nameof(AdvanceAdmissionWorkflowDto.ConcurrencyStamp));
	}

	[Fact]
	public void Activation_requires_concurrency_and_idempotency_tokens()
	{
		var result = new ActivateStudentOnboardingDtoValidator().Validate(
			new ActivateStudentOnboardingDto());

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(ActivateStudentOnboardingDto.ConcurrencyStamp));
		result.Errors.ShouldContain(error => error.PropertyName == nameof(ActivateStudentOnboardingDto.IdempotencyKey));
	}

	[Fact]
	public void Identifier_series_rejects_unsupported_types_and_unsafe_database_lengths()
	{
		var result = new CreateStudentIdentifierSeriesDtoValidator().Validate(new CreateStudentIdentifierSeriesDto
		{
			AcademicYearId = Guid.NewGuid(),
			IdentifierType = StudentIdentifierType.StudentCode,
			Code = "ROLL",
			Name = "Roll number series",
			Prefix = new string('P', 41),
			Suffix = new string('S', 41),
			Padding = 5,
			StartingSequence = 10,
			MaximumSequence = 9,
			EffectiveFrom = DateTime.UtcNow
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateStudentIdentifierSeriesDto.IdentifierType));
		result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateStudentIdentifierSeriesDto.Prefix));
		result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateStudentIdentifierSeriesDto.Suffix));
		result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateStudentIdentifierSeriesDto.MaximumSequence));
	}
}
