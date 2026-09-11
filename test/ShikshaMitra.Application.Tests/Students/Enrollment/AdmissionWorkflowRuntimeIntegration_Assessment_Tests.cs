using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_assessment_blocks_missing_evidence_and_failed_default_then_allows_a_passing_retake()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateManagedAssessmentApplicationAsync();
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var initial = await service.GetAsync(application.Id, stageId);
		initial.AssessmentEnabled.ShouldBeTrue();
		initial.CanComplete.ShouldBeFalse();
		var missing = await Should.ThrowAsync<BusinessException>(() => AdvanceAssessmentAsync(application.Id, stageId));
		missing.Code.ShouldBe(AdmissionAssessmentErrorCodes.EvidenceRequired);
		var booked = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = initial.ConcurrencyStamp });
		var attended = await service.RecordAttendanceAsync(booked.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = booked.Attempts.Single().ConcurrencyStamp, Attendance = "present"
		});
		var failed = await service.RecordResultAsync(attended.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = attended.Attempts.Single().ConcurrencyStamp, Marks = 20
		});
		failed.Outcome.ShouldBe("failed");
		failed.CanComplete.ShouldBeFalse();
		var wrongRoute = await Should.ThrowAsync<BusinessException>(() => AdvanceAssessmentAsync(application.Id, stageId));
		wrongRoute.Code.ShouldBe(AdmissionAssessmentErrorCodes.RouteRequired);
		var retake = await service.RetakeAsync(failed.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = failed.Attempts.Single().ConcurrencyStamp, Notes = "A second assessment is allowed."
		});
		var second = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = retake.ConcurrencyStamp });
		second.Attempts.Count.ShouldBe(2);
		var attempt = second.Attempts.Last();
		var secondAttendance = await service.RecordAttendanceAsync(attempt.Id, new()
		{
			ConcurrencyStamp = attempt.ConcurrencyStamp, Attendance = "present"
		});
		var passed = await service.RecordResultAsync(attempt.Id, new()
		{
			ConcurrencyStamp = secondAttendance.Attempts.Last().ConcurrencyStamp, Marks = 80
		});
		passed.CanComplete.ShouldBeTrue();
		passed.Outcome.ShouldBe("passed");
		var runtime = await AdvanceAssessmentAsync(application.Id, stageId);
		runtime.Stages.Single(stage => stage.Id == stageId).Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
		runtime.Stages.Single(stage => stage.Code == "ADMISSION").Status.ShouldBe(AdmissionRuntimeStageStatus.Active);
		var historical = await service.GetAsync(application.Id, stageId);
		historical.Attempts.Count.ShouldBe(2);
		historical.CanRecordResult.ShouldBeFalse();
	}

	[Fact]
	public async Task Managed_assessment_review_requires_a_different_reviewer_and_preserves_the_result()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateManagedAssessmentApplicationAsync(review: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state = await service.RecordAttendanceAsync(state.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Attendance = "present"
		});
		state = await service.RecordResultAsync(state.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Marks = 85
		});
		state.Attempts.Single().Status.ShouldBe("pendingReview");
		state.CanComplete.ShouldBeFalse();
		state.CanReview.ShouldBeFalse();
		var reviewInput = new AdmissionAssessmentReviewInput { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Approved = true };
		await Should.ThrowAsync<BusinessException>(() => service.ReviewAsync(state.Attempts.Single().Id, reviewInput));
		var reviewerId = Guid.NewGuid();
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: reviewerId, description: "Assessment reviewer"), autoSave: true);
		using var reviewer = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
		[
			new Claim(AbpClaimTypes.UserId, reviewerId.ToString()),
			new Claim(AbpClaimTypes.UserName, "assessment-reviewer"),
			new Claim(AbpClaimTypes.Role, "admin")
		], "Test")));
		var reviewed = await service.ReviewAsync(state.Attempts.Single().Id, reviewInput);
		reviewed.CanComplete.ShouldBeTrue();
		reviewed.Attempts.Single().ReviewedBy.ShouldBe(reviewerId);
		reviewed.Attempts.Single().Marks.ShouldBe(85);
	}

	[Fact]
	public async Task Managed_assessment_rejects_foreign_application_stage_and_tenant_reads()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync();
		var other = await CreateApplicantAsync("Other applicant");
		var otherApplication = await _applicationAppService.StartAsync(other.Id, offering.Id);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(otherApplication.Id, stageId));
		using var otherTenant = _currentTenant.Change(Guid.NewGuid());
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(application.Id, stageId));
	}

	[Fact]
	public async Task Failed_assessment_follows_explicit_outcome_and_application_condition_with_matching_readiness()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateManagedAssessmentApplicationAsync(failureRoute: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state = await service.RecordAttendanceAsync(state.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Attendance = "present"
		});
		state = await service.RecordResultAsync(state.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Marks = 20
		});
		state.Outcome.ShouldBe("failed");
		state.CanComplete.ShouldBeTrue();
		state.Blockers.ShouldBeEmpty();
		var runtime = await AdvanceAssessmentAsync(application.Id, stageId);
		runtime.Stages.Single(stage => stage.Id == stageId).Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
		runtime.Stages.Single(stage => stage.Id == runtime.CurrentStageInstanceId).Code.ShouldBe("NOT_SELECTED");
		var history = runtime.History.Last();
		history.ConditionEvaluationSnapshotJson.ShouldNotBeNull().ShouldContain("assessment.outcome");
		history.ConditionEvaluationSnapshotJson.ShouldContain("application.status");
	}

	private async Task<(AdmissionApplicationDto Application, Guid StageId, AdmissionOffering Offering)> CreateManagedAssessmentApplicationAsync(bool review = false, bool scheduling = false, bool failureRoute = false)
	{
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: TestUserId, description: "Assessment integration test"), autoSave: true);
		var legacy = await CreateAndPublishWorkflowAsync();
		var draft = await _workflowAppService.CreateNewVersionAsync(legacy.Id);
		var input = ToUpdateInput(draft);
		var entry = input.Stages.Single(stage => stage.IsEntryStage);
		var terminal = input.Stages.Single(stage => stage.IsTerminalStage);
		terminal.DisplayOrder = 2;
		var assessmentId = Guid.NewGuid();
		input.Stages.Add(new CreateUpdateAdmissionWorkflowStageDto
		{
			Id = assessmentId, Code = "MANAGED_ASSESSMENT", Name = "Entrance assessment",
			StageType = AdmissionWorkflowStageType.Assessment, DisplayOrder = 1, IsRequired = true,
			ConfigurationJson = JsonSerializer.Serialize(new { assessment = new
			{
				version = 1, enabled = true, schedulingEnabled = scheduling, attendanceRequired = true,
				scoringEnabled = true, totalMarks = 100, passingMarks = 40, reviewRequired = review, maxAttempts = 2
			} })
		});
		input.Transitions =
		[
			new() { FromStageId = entry.Id!.Value, ToStageId = assessmentId, IsDefault = true },
			new() { FromStageId = assessmentId, ToStageId = terminal.Id!.Value, IsDefault = true }
		];
		if (failureRoute)
		{
			var failureStageId = Guid.NewGuid();
			input.Stages.Add(new()
			{
				Id = failureStageId, Code = "NOT_SELECTED", Name = "Not selected",
				StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 3, IsTerminalStage = true
			});
			input.Transitions.Add(new()
			{
				FromStageId = assessmentId, ToStageId = failureStageId, IsDefault = false, Priority = 1,
				ConditionRuleJson = """
					{"operator":"ALL","conditions":[
					{"field":"assessment.outcome","operator":"Equals","value":"failed"},
					{"field":"application.status","operator":"Equals","value":"Draft"}]}
					"""
			});
		}
		draft = await _workflowAppService.UpdateAsync(draft.Id, input);
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var offering = await CreateAvailableOfferingAsync(published.Id);
		var applicant = await CreateApplicantAsync("Assessment applicant");
		var application = await _applicationAppService.StartAsync(applicant.Id, offering.Id);
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime = await AdvanceAssessmentAsync(application.Id, runtime.CurrentStageInstanceId!.Value);
		return (await _applicationAppService.GetAsync(application.Id), runtime.CurrentStageInstanceId!.Value, offering);
	}

	private async Task<AdmissionApplicationWorkflowDto> AdvanceAssessmentAsync(Guid applicationId, Guid stageId) =>
		await _applicationAppService.AdvanceWorkflowAsync(applicationId, new AdvanceAdmissionWorkflowDto
		{
			ExpectedCurrentStageInstanceId = stageId, IdempotencyKey = Guid.NewGuid().ToString("N"),
			ConcurrencyStamp = (await _applicationAppService.GetAsync(applicationId)).ConcurrencyStamp
		});
}
