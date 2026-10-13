using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Assessment_roster_tracks_unbooked_attendance_result_route_readiness_and_completed_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync();
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var input = new AdmissionAssessmentRosterInput { AdmissionOfferingId = offering.Id,
			WorkflowStageDefinitionId = runtime.Stages.Single(stage => stage.Id == stageId).DefinitionStageId, MaxResultCount = 10 };
		var scope = (await service.GetScopesAsync()).Single(scope => scope.AdmissionOfferingId == offering.Id &&
			scope.WorkflowStageDefinitionId == input.WorkflowStageDefinitionId);
		scope.WorkflowName.ShouldNotBeNullOrWhiteSpace();
		scope.OfferingCode.ShouldBe(offering.Code);
		var roster = await service.GetApplicantsAsync(input);
		roster.TotalCount.ShouldBe(1);
		roster.Counts.ToBook.ShouldBe(1);
		roster.Items.Single().Assessment.CanBook.ShouldBeTrue();
		var state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = roster.Items.Single().Assessment.ConcurrencyStamp });
		(await service.GetApplicantsAsync(input)).Items.Single().Queue.ShouldBe("attendance");
		state = await service.RecordAttendanceAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Attendance = "present" });
		(await service.GetApplicantsAsync(input)).Items.Single().Queue.ShouldBe("result");
		state = await service.RecordResultAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Marks = 20 });
		roster = await service.GetApplicantsAsync(input);
		roster.Items.Single().Queue.ShouldBe("attention");
		roster.Items.Single().Assessment.CanComplete.ShouldBeFalse();
		state = await service.RetakeAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Notes = "Second attempt" });
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state = await service.RecordAttendanceAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Attendance = "present" });
		state = await service.RecordResultAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Marks = 80 });
		roster = await service.GetApplicantsAsync(input);
		roster.Items.Single().Queue.ShouldBe("ready");
		roster.Items.Single().Assessment.CanComplete.ShouldBeTrue();
		await AdvanceAssessmentAsync(application.Id, stageId);
		roster = await service.GetApplicantsAsync(input);
		roster.Counts.Completed.ShouldBe(1);
		roster.Items.Single().Assessment.Attempts.Count.ShouldBe(2);
		roster.Items.Single().Assessment.CanRecordResult.ShouldBeFalse();
	}

	[Fact]
	public async Task Assessment_roster_session_search_and_queue_counts_stay_scoped_and_reject_foreign_tenant()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var definitionId = runtime.Stages.Single(stage => stage.Id == stageId).DefinitionStageId;
		var session = await service.CreateSessionAsync(new() { AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = definitionId,
			Name = "Roster session", StartsAt = DateTime.UtcNow.AddDays(1), EndsAt = DateTime.UtcNow.AddDays(1).AddHours(1), Location = "Room A", Capacity = 10 });
		var input = new AdmissionAssessmentRosterInput { AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = definitionId,
			SessionId = session.Id, MaxResultCount = 1 };
		(await service.GetApplicantsAsync(input)).TotalCount.ShouldBe(0);
		var state = await service.GetAsync(application.Id, stageId);
		await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp, SessionId = session.Id });
		var otherApplicant = await CreateApplicantAsync("Unbooked roster applicant");
		var otherApplication = await StartFixtureApplicationAsync(otherApplicant.Id, offering.Id);
		var roster = await service.GetApplicantsAsync(input);
		roster.Counts.All.ShouldBe(1);
		roster.Items.Single().ApplicationId.ShouldBe(application.Id);
		input.Queue = "result";
		roster = await service.GetApplicantsAsync(input);
		roster.TotalCount.ShouldBe(0);
		roster.Counts.Attendance.ShouldBe(1);
		input.Queue = null;
		input.SessionId = null;
		roster = await service.GetApplicantsAsync(input);
		roster.Counts.All.ShouldBe(2);
		roster.Items.Count.ShouldBe(1);
		input.Search = "Unbooked roster";
		roster = await service.GetApplicantsAsync(input);
		roster.Counts.All.ShouldBe(1);
		roster.Items.Single().ApplicationId.ShouldBe(otherApplication.Id);
		input.SessionId = Guid.NewGuid();
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetApplicantsAsync(input));
		input.SessionId = null;
		using var otherTenant = _currentTenant.Change(Guid.NewGuid());
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetApplicantsAsync(input));
	}

	[Fact]
	public async Task Assessment_roster_readiness_matches_an_explicit_failure_route()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync(failureRoute: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state = await service.RecordAttendanceAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Attendance = "present" });
		await service.RecordResultAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Marks = 20 });
		var roster = await service.GetApplicantsAsync(new() { AdmissionOfferingId = offering.Id,
			WorkflowStageDefinitionId = runtime.Stages.Single(stage => stage.Id == stageId).DefinitionStageId });
		roster.Items.Single().Queue.ShouldBe("ready");
		roster.Items.Single().Assessment.Blockers.ShouldBeEmpty();
	}

	[Fact]
	public async Task Assessment_roster_keeps_direct_results_ready_without_reopening_retired_review()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync(review: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state = await service.RecordAttendanceAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Attendance = "present" });
		await service.RecordResultAsync(state.Attempts.Single().Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, Marks = 80 });
		var roster = await service.GetApplicantsAsync(new() { AdmissionOfferingId = offering.Id,
			WorkflowStageDefinitionId = runtime.Stages.Single(stage => stage.Id == stageId).DefinitionStageId, Queue = "ready" });
		roster.Counts.Review.ShouldBe(0);
		roster.Counts.Ready.ShouldBe(1);
		roster.Items.Single().Assessment.CanReview.ShouldBeFalse();
		roster.Items.Single().Assessment.CanComplete.ShouldBeTrue();
		roster.Items.Single().Assessment.Attempts.Single().Marks.ShouldBe(80);
	}

	[Fact]
	public async Task Assessment_roster_rejects_a_current_tenant_user_without_academic_assignments()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var userId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(userId, $"unassigned-assessment-{userId:N}");
		using var unassigned = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
		[
			new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, "unassigned-assessment")
		], "Test")));
		await Should.ThrowAsync<EntityNotFoundException>(() => GetRequiredService<IAdmissionAssessmentAppService>().GetApplicantsAsync(new()
		{
			AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = runtime.Stages.Single(stage => stage.Id == stageId).DefinitionStageId
		}));
	}

	[Fact]
	public async Task Assessment_roster_moves_an_active_retake_to_the_new_session_and_retains_completed_session_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var definitionId = runtime.Stages.Single(stage => stage.Id == stageId).DefinitionStageId;
		async Task<AdmissionAssessmentSessionDto> CreateSession(string name) => await service.CreateSessionAsync(new()
		{
			AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = definitionId, Name = name,
			StartsAt = DateTime.UtcNow.AddDays(1), EndsAt = DateTime.UtcNow.AddDays(1).AddHours(1), Location = "Room A", Capacity = 10
		});
		async Task LetSessionStart(Guid id) => await WithUnitOfWorkAsync(async () =>
		{
			// Fixture time passage: exercise the real attendance/result commands after a session starts.
			var repository = GetRequiredService<IRepository<AdmissionAssessmentSession, Guid>>();
			var session = await repository.GetAsync(id);
			session.Update(session.Name, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), session.Location, session.Capacity);
			await repository.UpdateAsync(session, true);
		});
		var first = await CreateSession("First exam");
		var second = await CreateSession("Retake exam");
		var input = new AdmissionAssessmentRosterInput { AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = definitionId, SessionId = first.Id };
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp, SessionId = first.Id });
		await LetSessionStart(first.Id);
		state = await service.RecordAttendanceAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Attendance = "present" });
		state = await service.RecordResultAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Marks = 20 });
		state = await service.RetakeAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Notes = "Retake at another session" });
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp, SessionId = second.Id });
		(await service.GetApplicantsAsync(input)).Counts.All.ShouldBe(0);
		input.SessionId = second.Id;
		(await service.GetApplicantsAsync(input)).Items.Single().Assessment.Attempts.Last().SessionId.ShouldBe(second.Id);
		await LetSessionStart(second.Id);
		state = await service.RecordAttendanceAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Attendance = "present" });
		await service.RecordResultAsync(state.Attempts.Last().Id, new() { ConcurrencyStamp = state.Attempts.Last().ConcurrencyStamp, Marks = 80 });
		await AdvanceAssessmentAsync(application.Id, stageId);
		input.SessionId = first.Id;
		var history = await service.GetApplicantsAsync(input);
		history.Counts.Completed.ShouldBe(1);
		history.Items.Single().Assessment.CanRecordResult.ShouldBeFalse();
	}
}
