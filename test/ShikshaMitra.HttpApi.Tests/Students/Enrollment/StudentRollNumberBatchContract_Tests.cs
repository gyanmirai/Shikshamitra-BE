using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class StudentRollNumberBatchContract_Tests
{
	[Theory]
	[InlineData(StudentRollNumberErrorCodes.StalePreview, 409)]
	[InlineData(StudentRollNumberErrorCodes.InvalidState, 409)]
	[InlineData(StudentRollNumberErrorCodes.AdminRequired, 403)]
	[InlineData(StudentRollNumberErrorCodes.ApproverRequired, 403)]
	[InlineData(StudentRollNumberErrorCodes.InvalidBatch, 400)]
	public void Public_errors_keep_conflicts_authority_and_validation_distinct(string code, int status) => ApiProblemDetailsMapper.ResolveBusinessStatusCode(code).ShouldBe(status);
	[Fact]
	public void Explicit_controller_is_authenticated_and_accepts_only_case_targets_not_a_roll_mapping()
	{
		var controller = typeof(StudentRollNumberBatchesController);
		controller.GetCustomAttribute<AuthorizeAttribute>().ShouldNotBeNull();
		controller.GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/students/roll-number-batches");
		typeof(StudentRollNumberBatchAppService).GetCustomAttribute<AuthorizeAttribute>().ShouldNotBeNull();
		typeof(PreviewStudentRollBatchInput).GetProperties().Select(x => x.Name).Order().ShouldBe(new[] { "AcademicYearId", "DivisionId", "Operation", "StandardId", "CaseIds" }.Order());
	}
	[Theory]
	[InlineData("PreviewAsync", "preview")]
	[InlineData("SubmitAsync", "{id:guid}/submit")]
	[InlineData("DecideAsync", "{id:guid}/decision")]
	[InlineData("ApplyAsync", "{id:guid}/apply")]
	public void Frozen_preview_submission_review_and_application_have_separate_commands(string method, string route) =>
		typeof(StudentRollNumberBatchesController).GetMethod(method)!.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe(route);
	[Fact]
	public void Inputs_require_explicit_scope_stamps_and_stable_submission_keys()
	{
		new PreviewStudentRollBatchInputValidator().Validate(new PreviewStudentRollBatchInput()).IsValid.ShouldBeFalse();
		new PreviewStudentRollBatchInputValidator().Validate(new PreviewStudentRollBatchInput { AcademicYearId = Guid.NewGuid(), StandardId = Guid.NewGuid(), DivisionId = Guid.NewGuid(), Operation = "client-renumber" }).IsValid.ShouldBeFalse();
		var scope = new PreviewStudentRollBatchInput { AcademicYearId = Guid.NewGuid(), StandardId = Guid.NewGuid(), DivisionId = Guid.NewGuid(), CaseIds = [] };
		var validator = new PreviewStudentRollBatchInputValidator();
		validator.Validate(scope).IsValid.ShouldBeFalse();
		scope.CaseIds = [Guid.NewGuid()]; validator.Validate(scope).IsValid.ShouldBeTrue();
		scope.Operation = "renumber"; validator.Validate(scope).IsValid.ShouldBeFalse();
		scope.Operation = "assign-missing"; scope.CaseIds = [Guid.Empty]; validator.Validate(scope).IsValid.ShouldBeFalse();
		var id = Guid.NewGuid(); scope.CaseIds = [id, id]; validator.Validate(scope).IsValid.ShouldBeFalse();
		scope.CaseIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList(); validator.Validate(scope).IsValid.ShouldBeFalse();
		new SubmitStudentRollBatchInputValidator().Validate(new SubmitStudentRollBatchInput { IdempotencyKey = "key" }).IsValid.ShouldBeFalse();
		new SubmitStudentRollBatchInputValidator().Validate(new SubmitStudentRollBatchInput { ConcurrencyStamp = "stamp" }).IsValid.ShouldBeFalse();
		new SubmitStudentRollBatchInputValidator().Validate(new SubmitStudentRollBatchInput { ConcurrencyStamp = "stamp", IdempotencyKey = "key", Reason = new string('x', 1001) }).IsValid.ShouldBeFalse();
	}
}
