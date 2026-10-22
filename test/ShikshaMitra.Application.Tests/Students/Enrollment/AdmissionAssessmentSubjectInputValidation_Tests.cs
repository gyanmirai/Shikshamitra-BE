using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSubjectInputValidation_Tests
{
	[Theory]
	[InlineData(AdmissionWorkflowStageType.Assessment, 9000, true)]
	[InlineData(AdmissionWorkflowStageType.Assessment, 64001, false)]
	[InlineData(AdmissionWorkflowStageType.Form, 8001, false)]
	[InlineData(AdmissionWorkflowStageType.Form, 8000, true)]
	public void Configuration_capacity_is_expanded_only_for_assessment(AdmissionWorkflowStageType type, int length, bool valid)
	{
		var input = new CreateUpdateAdmissionWorkflowStageDto { Code = "STAGE", Name = "Stage", StageType = type, ConfigurationJson = new string('x', length) };
		new CreateUpdateAdmissionWorkflowStageDtoValidator().Validate(input).IsValid.ShouldBe(valid);
	}

	[Fact]
	public void Subject_result_input_rejects_duplicate_unknown_shape_negative_and_null_rows()
	{
		var validator = new AdmissionAssessmentResultInputValidator();
		validator.Validate(new AdmissionAssessmentResultInput { SubjectScores = [new() { Code = "english", Marks = 0 }, new() { Code = "maths", Marks = null }] }).IsValid.ShouldBeTrue();
		validator.Validate(new AdmissionAssessmentResultInput { SubjectScores = [new() { Code = "english", Marks = 1 }, new() { Code = "english", Marks = 2 }] }).IsValid.ShouldBeFalse();
		validator.Validate(new AdmissionAssessmentResultInput { SubjectScores = [new() { Code = "english", Marks = -0.1m }] }).IsValid.ShouldBeFalse();
		validator.Validate(new AdmissionAssessmentResultInput { SubjectScores = [null!] }).IsValid.ShouldBeFalse();
		validator.Validate(new AdmissionAssessmentResultInput { SubjectScores = null! }).IsValid.ShouldBeFalse();
	}
}
