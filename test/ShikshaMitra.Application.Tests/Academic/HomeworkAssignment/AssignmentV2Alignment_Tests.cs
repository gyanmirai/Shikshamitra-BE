using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using FluentValidation;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentV2Alignment_Tests
{
	[Fact]
	public void Submission_Type_Three_Should_Be_A_Valid_Create_Contract_Value()
	{
		var input = CreateValidAssignment();
		input.SubmissionType = (int) SubmissionType.NoSubmissionRequired;
		var validationResults = new List<ValidationResult>();

		var isValid = Validator.TryValidateObject(
			input,
			new ValidationContext(input),
			validationResults,
			validateAllProperties: true);

		isValid.ShouldBeTrue(string.Join(Environment.NewLine, validationResults.Select(x => x.ErrorMessage)));
		((SubmissionType) input.SubmissionType.Value).ShouldBe(SubmissionType.NoSubmissionRequired);
	}

	[Fact]
	public void Submission_Type_Above_Three_Should_Fail_Contract_Validation()
	{
		var input = CreateValidAssignment();
		input.SubmissionType = 4;
		var validationResults = new List<ValidationResult>();

		var isValid = Validator.TryValidateObject(
			input,
			new ValidationContext(input),
			validationResults,
			validateAllProperties: true);

		isValid.ShouldBeFalse();
		validationResults.ShouldContain(result => result.MemberNames.Contains(nameof(input.SubmissionType)));
	}

	[Theory]
	[MemberData(nameof(AssignmentTypeValidators))]
	public void Assignment_Type_Validator_Should_Enforce_Evaluation_Cross_Field_Rules(
		IValidator validator,
		object input,
		bool expectedValid)
	{
		var result = validator.Validate(new ValidationContext<object>(input));

		result.IsValid.ShouldBe(expectedValid, string.Join(Environment.NewLine, result.Errors));
	}

	[Fact]
	public void Assignment_Attachment_And_Chapter_Ids_Should_Round_Trip_Through_Dto_Mapping()
	{
		var chapterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var secondChapterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var attachment = "assignments/unit-1.pdf?version=2";
		var canonicalAttachment = AssignmentAttachmentSerializer.Canonicalize(attachment);
		var assignment = CreateAssignment(canonicalAttachment);
		assignment.ChapterIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { chapterId, secondChapterId });

		var dto = BuildAssignmentDto(assignment);

		dto.Attachment.ShouldBe(attachment);
		dto.ChapterIds.ShouldBe(new[] { chapterId, secondChapterId });
		AssignmentAttachmentSerializer.ReadLegacyList(canonicalAttachment)!.Single().Name.ShouldBe("unit-1.pdf");
	}

	[Fact]
	public void Legacy_Attachment_Mapping_Should_Preserve_All_Items_And_Metadata()
	{
		const string stored = """
			[{"name":"first.pdf","url":"blob/first.pdf","size":12},{"Name":"second.docx","Url":"blob/second.docx","Size":34}]
			""";

		var attachments = AssignmentAttachmentSerializer.ReadLegacyList(stored)!;

		attachments.Count.ShouldBe(2);
		attachments[0].Name.ShouldBe("first.pdf");
		attachments[0].Size.ShouldBe(12);
		attachments[1].Name.ShouldBe("second.docx");
		attachments[1].Size.ShouldBe(34);
		AssignmentAttachmentSerializer.ReadSingle(stored).ShouldBe("blob/first.pdf");
	}

	[Fact]
	public void Assignment_Dto_Mapping_Should_Tolerate_Legacy_Attachment_And_Invalid_Chapter_Data()
	{
		var assignment = CreateAssignment("legacy-file.pdf");
		assignment.ChapterIdsJson = "not-json";

		var dto = BuildAssignmentDto(assignment);

		dto.Attachment.ShouldBe("legacy-file.pdf");
		dto.ChapterIds.ShouldBeEmpty();
	}

	[Fact]
	public void Aggregate_Scope_Should_Require_Access_To_Every_Division()
	{
		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();
		var standardId = Guid.NewGuid();
		var divisionA = Guid.NewGuid();
		var divisionB = Guid.NewGuid();
		var scope = new UserScopeContext
		{
			AcademicYearIds = [yearId],
			BoardIds = [boardId],
			MediumIds = [mediumId],
			MediumStageIds = [stageId],
			StandardIds = [standardId],
			DivisionIds = [divisionA]
		};

		AssignmentScopePolicy.CanAccessAggregate(
			scope, yearId, boardId, mediumId, stageId, standardId, [divisionA]).ShouldBeTrue();
		AssignmentScopePolicy.CanAccessAggregate(
			scope, yearId, boardId, mediumId, stageId, standardId, [divisionA, divisionB]).ShouldBeFalse();

		scope.WildcardDivisionStandardIds.Add(standardId);
		AssignmentScopePolicy.CanAccessAggregate(
			scope, yearId, boardId, mediumId, stageId, standardId, [divisionA, divisionB]).ShouldBeTrue();
	}

	[Fact]
	public void Calendar_Division_Should_Only_Apply_Inside_Its_Standard()
	{
		var calendarId = Guid.NewGuid();
		var standardA = Guid.NewGuid();
		var standardB = Guid.NewGuid();
		var divisionA = Guid.NewGuid();
		var divisionB = Guid.NewGuid();
		var day = new AcademicCalendarDay(
			Guid.NewGuid(),
			Guid.NewGuid(),
			calendarId,
			new DateTime(2026, 8, 1),
			DayType.Holiday,
			"Division A holiday",
			appliesToAll: false,
			standardId: standardA,
			divisionId: divisionA);

		AssignmentCalendarPolicy.AppliesTo(day, standardA, divisionA).ShouldBeTrue();
		AssignmentCalendarPolicy.AppliesTo(day, standardA, divisionB).ShouldBeFalse();
		AssignmentCalendarPolicy.AppliesTo(day, standardB, divisionA).ShouldBeFalse();
	}

	public static IEnumerable<object[]> AssignmentTypeValidators()
	{
		yield return
		[
			new CreateAssignmentTypeDtoValidator(),
			new CreateAssignmentTypeDto
			{
				AssignmentTypeName = "Project",
				EvaluationRequired = true,
				DefaultEvaluationMethod = null,
				DefaultMarks = null,
			},
			false
		];
		yield return
		[
			new CreateAssignmentTypeDtoValidator(),
			new CreateAssignmentTypeDto
			{
				AssignmentTypeName = "Project",
				EvaluationRequired = true,
				DefaultEvaluationMethod = EvaluationMethod.Marks,
				DefaultMarks = 25,
			},
			true
		];
		yield return
		[
			new UpdateAssignmentTypeDtoValidator(),
			new UpdateAssignmentTypeDto
			{
				AssignmentTypeName = "Project",
				EvaluationRequired = true,
				DefaultEvaluationMethod = EvaluationMethod.FeedbackOnly,
				DefaultMarks = 25,
			},
			false
		];
		yield return
		[
			new UpdateAssignmentTypeDtoValidator(),
			new UpdateAssignmentTypeDto
			{
				AssignmentTypeName = "Project",
				EvaluationRequired = false,
				DefaultEvaluationMethod = null,
				DefaultMarks = 25,
			},
			false
		];
	}

	private static CreateAssignmentV2Dto CreateValidAssignment() => new()
	{
		StandardId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
		SubjectId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
		Title = "Unit assignment",
		AssignmentTypeId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
		Description = "Complete the assigned unit.",
		DivisionSchedules =
		[
			new CreateAssignmentV2DivisionScheduleDto
			{
				DivisionId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
				DueAt = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
			}
		],
	};

	private static AssignmentEntity CreateAssignment(string? attachmentsJson) => new(
		Guid.Parse("77777777-7777-7777-7777-777777777777"),
		Guid.Parse("33333333-3333-3333-3333-333333333333"),
		Guid.Parse("44444444-4444-4444-4444-444444444444"),
		"Unit assignment",
		"Complete the assigned unit.",
		Guid.Parse("55555555-5555-5555-5555-555555555555"),
		SubmissionType.NoSubmissionRequired,
		0,
		attachmentsJson: attachmentsJson,
		evaluationRequired: false,
		evaluationMethod: EvaluationMethod.NoEvaluation);

	private static AssignmentV2Dto BuildAssignmentDto(AssignmentEntity assignment)
	{
		var method = typeof(AssignmentV2AppService).GetMethod(
			"BuildAssignmentDto",
			BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		return (AssignmentV2Dto) method.Invoke(
			null,
			[
				assignment,
				Guid.Parse("88888888-8888-8888-8888-888888888888"),
				"Standard 8",
				"Science",
				null,
				new Dictionary<Guid, string>(),
				"Test Creator",
				"Test Updater",
			])!;
	}
}
