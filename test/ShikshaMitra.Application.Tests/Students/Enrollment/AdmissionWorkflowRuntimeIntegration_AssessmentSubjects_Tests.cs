using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Subject_assessment_persists_partial_marks_then_authoritatively_submits_and_advances()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateSubjectAssessmentApplicationAsync();
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state.EffectiveProfile!.Subjects.Count.ShouldBe(2);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		var attempt = state.Attempts.Single();
		state.CanSaveResultDraft.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => service.SaveResultDraftAsync(attempt.Id,
			new() { ConcurrencyStamp = attempt.ConcurrencyStamp, SubjectScores = [SubjectScore("english", 20)] }));
		state = await service.RecordAttendanceAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Attendance = "present" });
		attempt = state.Attempts.Single();
		var staleStamp = attempt.ConcurrencyStamp;
		state.CanSaveResultDraft.ShouldBeTrue();
		state.CanSubmitResult.ShouldBeFalse();
		state = await service.SaveResultDraftAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, SubjectScores = [SubjectScore("english", 0)], Notes = "Partial saved marks" });
		var reloaded = await service.GetAsync(application.Id, stageId);
		reloaded.Attempts.Single().SubjectScores.Single(score => score.Code == "english").Marks.ShouldBe(0);
		reloaded.Attempts.Single().SubjectScores.Single(score => score.Code == "maths").Marks.ShouldBeNull();
		reloaded.Attempts.Single().Marks.ShouldBeNull();
		reloaded.CanComplete.ShouldBeFalse();
		reloaded.CanSubmitResult.ShouldBeFalse();
		var stale = await Should.ThrowAsync<BusinessException>(() => service.SaveResultDraftAsync(attempt.Id,
			new() { ConcurrencyStamp = staleStamp, SubjectScores = [] }));
		stale.Code.ShouldBe(AdmissionAssessmentErrorCodes.ConcurrencyConflict);
		await Should.ThrowAsync<BusinessException>(() => AdvanceAssessmentAsync(application.Id, stageId));
		attempt = reloaded.Attempts.Single();
		await Should.ThrowAsync<BusinessException>(() => service.RecordResultAsync(attempt.Id,
			new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Marks = 100, Outcome = "passed", SubjectScores = [SubjectScore("english", 50)] }));
		state = await service.RecordResultAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp,
			Marks = 0, Outcome = "failed", SubjectScores = [SubjectScore("english", 20.5m), SubjectScore("maths", 30.5m)] });
		state.Attempts.Single().Marks.ShouldBe(51);
		state.Outcome.ShouldBe("passed");
		state.CanComplete.ShouldBeTrue();
		var roster = await service.GetApplicantsAsync(new() { Search = application.ApplicationNumber });
		roster.Items.Single().Assessment.Attempts.Single().EffectiveProfile!.Name.ShouldBe("Combined entrance");
		roster.Items.Single().Assessment.Attempts.Single().SubjectScores.Count.ShouldBe(2);
		(await AdvanceAssessmentAsync(application.Id, stageId)).Stages.Single(stage => stage.Id == stageId).Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
		(await service.GetAsync(application.Id, stageId)).CanSaveResultDraft.ShouldBeFalse();
	}

	[Fact]
	public async Task Failed_subject_result_remains_at_assessment_and_retake_has_fresh_marks()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateSubjectAssessmentApplicationAsync();
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		var attempt = state.Attempts.Single();
		state = await service.RecordAttendanceAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Attendance = "present" });
		state = await service.RecordResultAsync(attempt.Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp,
			SubjectScores = [SubjectScore("english", 10), SubjectScore("maths", 50)] });
		state.Outcome.ShouldBe("failed");
		state.CanComplete.ShouldBeFalse();
		state.CanRetake.ShouldBeTrue();
		state = await service.RetakeAsync(attempt.Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp });
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		state.Attempts.Last().SubjectScores.ShouldBeEmpty();
		state.Attempts.First().SubjectScores.Count.ShouldBe(2);
		state.Attempts.Last().EffectiveProfile!.Subjects.Count.ShouldBe(2);
		var second = state.Attempts.Last();
		state = await service.RecordAttendanceAsync(second.Id, new() { ConcurrencyStamp = second.ConcurrencyStamp, Attendance = "absent" });
		state.Attempts.Last().Marks.ShouldBeNull();
		state.Attempts.Last().SubjectScores.ShouldBeEmpty();
		state.CanComplete.ShouldBeFalse();
		state.CanRetake.ShouldBeFalse();
	}

	[Fact]
	public async Task Subject_draft_enforces_tenant_and_record_scope_before_mutation()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateSubjectAssessmentApplicationAsync();
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		var attempt = state.Attempts.Single();
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.SaveResultDraftAsync(attempt.Id,
				new() { ConcurrencyStamp = attempt.ConcurrencyStamp, SubjectScores = [] }));
		var outsider = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(outsider, $"subject-outsider-{outsider:N}");
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, outsider.ToString()), new Claim(AbpClaimTypes.UserName, $"subject-outsider-{outsider:N}")], "Test"))))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.SaveResultDraftAsync(attempt.Id,
				new() { ConcurrencyStamp = attempt.ConcurrencyStamp, SubjectScores = [SubjectScore("english", 40)] }));
		(await service.GetAsync(application.Id, stageId)).Attempts.Single().SubjectScores.ShouldBeEmpty();
	}

	private static AdmissionAssessmentSubjectScore SubjectScore(string code, decimal? marks) => new() { Code = code, Marks = marks };

	[Fact]
	public async Task Subject_draft_marks_prevent_cancellation_and_attempt_limit_bypass_without_required_attendance()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateSubjectAssessmentApplicationAsync(requireAttendance: false);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		var attempt = state.Attempts.Single();
		state.CanCancel.ShouldBeTrue();
		state = await service.SaveResultDraftAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, SubjectScores = [SubjectScore("english", 0)] });
		state.CanCancel.ShouldBeFalse();
		state = await service.SaveResultDraftAsync(attempt.Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp, SubjectScores = [] });
		state = await service.GetAsync(application.Id, stageId);
		state.CanCancel.ShouldBeFalse();
		attempt = state.Attempts.Single();
		await Should.ThrowAsync<BusinessException>(() => service.CancelAttemptAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp }));
		await Should.ThrowAsync<BusinessException>(() => service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp }));
		var reloaded = await service.GetAsync(application.Id, stageId);
		reloaded.Attempts.Count.ShouldBe(1);
		reloaded.Attempts.Single().Status.ShouldBe("pending");
		reloaded.Attempts.Single().SubjectScores.ShouldAllBe(score => score.Marks == null);
	}

	[Fact]
	public async Task Subject_draft_with_no_entered_marks_keeps_unstarted_cancellation_available()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, _) = await CreateSubjectAssessmentApplicationAsync(requireAttendance: false);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var state = await service.GetAsync(application.Id, stageId);
		state = await service.BookAsync(application.Id, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		var attempt = state.Attempts.Single();
		state = await service.SaveResultDraftAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp,
			SubjectScores = [SubjectScore("english", null), SubjectScore("maths", null)] });
		state = await service.GetAsync(application.Id, stageId);
		state.CanCancel.ShouldBeTrue();
		state = await service.CancelAttemptAsync(attempt.Id, new() { ConcurrencyStamp = state.Attempts.Single().ConcurrencyStamp });
		state.Attempts.Single().Status.ShouldBe("cancelled");
		state.CanBook.ShouldBeTrue();
	}

	private async Task<(AdmissionApplicationDto Application, Guid StageId, AdmissionOffering Offering)> CreateSubjectAssessmentApplicationAsync(bool requireAttendance = true)
	{
		var (_, _, offering) = await CreateManagedAssessmentApplicationAsync();
		var assessment = Guid.NewGuid();
		var decision = Guid.NewGuid();
		var draft = await _workflowAppService.CreateAsync(new()
		{
			Name = "Subject entrance journey", Code = $"SUBJECT_{Guid.NewGuid():N}"[..24],
			Stages = [new() { Id = assessment, Code = "SUBJECT_ASSESSMENT", Name = "Combined entrance", StageType = AdmissionWorkflowStageType.Assessment, IsEntryStage = true, IsRequired = true,
				ConfigurationJson = JsonSerializer.Serialize(new { assessment = new AdmissionAssessmentPolicy { Version = 2, Enabled = true, ScoringEnabled = true, AttendanceRequired = requireAttendance, ScoringMode = "subjects", MaxAttempts = 2,
					ClassProfiles = [new() { Id = "combined", Name = "Combined entrance", BoardId = offering.BoardId, MediumId = offering.MediumId, StandardIds = [offering.StandardId], PassRule = "both", PassingMarks = 40,
						Subjects = [new() { Code = "english", Name = "English", TotalMarks = 50, PassingMarks = 20 }, new() { Code = "maths", Name = "Maths", TotalMarks = 50, PassingMarks = 20 }] }] } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) },
				new() { Id = decision, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 1, IsRequired = true, IsTerminalStage = true }],
			Transitions = [new() { FromStageId = assessment, ToStageId = decision, IsDefault = true }]
		});
		var workflow = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(workflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var applicant = await CreateApplicantAsync("Subject applicant");
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		return (await _applicationAppService.GetAsync(application.Id), runtime.CurrentStageInstanceId!.Value, offering);
	}
}
