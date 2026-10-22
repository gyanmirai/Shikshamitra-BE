using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Shared_subject_assessment_keeps_two_class_profiles_scores_and_pinned_attempts_separate()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (workflow, firstOffering, secondOffering) = await CreateSharedSubjectOfferingsAsync();
		var first = await StartFixtureApplicationAsync((await CreateApplicantAsync("Lower class candidate")).Id, firstOffering.Id);
		var second = await StartFixtureApplicationAsync((await CreateApplicantAsync("Upper class candidate")).Id, secondOffering.Id);
		var definitionId = workflow.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Assessment).Id;
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var room = await WithUnitOfWorkAsync(() => CreateAssessmentPlanningRoomAsync());
		var sessionInput = new AdmissionAssessmentSessionInput
		{
			Name = "Shared subject entrance", RoomId = room.Id, Capacity = 2,
			StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(4),
			Scopes = [new() { AdmissionOfferingId = firstOffering.Id, WorkflowStageDefinitionId = definitionId }, new() { AdmissionOfferingId = secondOffering.Id, WorkflowStageDefinitionId = definitionId }]
		};
		var preview = await service.PreviewSessionAsync(new() { Session = sessionInput });
		preview.EligibleCount.ShouldBe(2);
		var confirmation = await service.ConfirmSessionAsync(new()
		{
			Session = sessionInput, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
			Bookings = preview.ProposedBookings.Select(item => new AdmissionAssessmentSelectedBookingInput { ApplicationId = item.ApplicationId, StageInstanceId = item.StageInstanceId, ConcurrencyStamp = item.ConcurrencyStamp }).ToList()
		});
		confirmation.BookedCount.ShouldBe(2);
		confirmation.Session.ScopeDetails.Select(scope => scope.StandardId).Distinct().Count().ShouldBe(2);
		var firstStageId = (await _applicationAppService.GetWorkflowAsync(first.Id)).CurrentStageInstanceId!.Value;
		var secondStageId = (await _applicationAppService.GetWorkflowAsync(second.Id)).CurrentStageInstanceId!.Value;
		var firstState = await service.GetAsync(first.Id, firstStageId);
		var secondState = await service.GetAsync(second.Id, secondStageId);
		firstState.Attempts.Single().SessionId.ShouldBe(confirmation.Session.Id);
		secondState.Attempts.Single().SessionId.ShouldBe(confirmation.Session.Id);
		firstState.Policy!.TotalMarks.ShouldBe(100);
		secondState.Policy!.TotalMarks.ShouldBe(80);
		firstState.EffectiveProfile!.PassRule.ShouldBe("both");
		secondState.EffectiveProfile!.PassRule.ShouldBe("overall");
		firstState.CanRecordAttendance.ShouldBeFalse();
		secondState.CanRecordResult.ShouldBeFalse();

		// Existing fixture time-passage pattern: only the session clock moves, never result/configuration data.
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionAssessmentSession, Guid>>();
			var session = await repository.GetAsync(confirmation.Session.Id);
			session.Update(session.Name, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), session.Location, session.Capacity);
			await repository.UpdateAsync(session, true);
		});
		confirmation.Session.RoomReservationStatus.ShouldBe((int)RoomReservationStatus.Approved);
		firstState = await service.RecordAttendanceAsync(firstState.Attempts.Single().Id,
			new() { ConcurrencyStamp = firstState.Attempts.Single().ConcurrencyStamp, Attendance = "present" });
		secondState = await service.RecordAttendanceAsync(secondState.Attempts.Single().Id,
			new() { ConcurrencyStamp = secondState.Attempts.Single().ConcurrencyStamp, Attendance = "present" });

		var attempts = GetRequiredService<IRepository<AdmissionAssessmentAttempt, Guid>>();
		var frozenFirst = await WithUnitOfWorkAsync(async () => (await attempts.GetAsync(firstState.Attempts.Single().Id)).PolicySnapshotJson);
		var frozenSecond = await WithUnitOfWorkAsync(async () => (await attempts.GetAsync(secondState.Attempts.Single().Id)).PolicySnapshotJson);
		var nextVersion = await _workflowAppService.CreateNewVersionAsync(workflow.Id);
		var update = ToUpdateInput(nextVersion);
		var nextStage = update.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Assessment);
		var nextPolicy = AdmissionAssessmentPolicy.Read(nextStage.ConfigurationJson)!;
		nextStage.ConfigurationJson = JsonSerializer.Serialize(new { assessment = nextPolicy with
		{
			ClassProfiles = nextPolicy.ClassProfiles.Select(profile => profile with { Name = profile.Name + " revised", PassingMarks = 1, PassRule = "overall" }).ToList()
		} }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		nextVersion = await _workflowAppService.UpdateAsync(nextVersion.Id, update);
		nextVersion = await _workflowAppService.PublishAsync(nextVersion.Id, nextVersion.ConcurrencyStamp);
		await UpdateSharedSubjectOfferingAsync(firstOffering.Id, nextVersion.Id);
		await UpdateSharedSubjectOfferingAsync(secondOffering.Id, nextVersion.Id);
		(await _applicationAppService.GetWorkflowAsync(first.Id)).WorkflowDefinitionId.ShouldBe(workflow.Id);
		(await _applicationAppService.GetWorkflowAsync(second.Id)).WorkflowDefinitionId.ShouldBe(workflow.Id);

		await Should.ThrowAsync<BusinessException>(() => service.RecordResultAsync(secondState.Attempts.Single().Id,
			new() { ConcurrencyStamp = secondState.Attempts.Single().ConcurrencyStamp, SubjectScores = [SubjectScore("english", 50)] }));
		await Should.ThrowAsync<BusinessException>(() => service.RecordResultAsync(secondState.Attempts.Single().Id,
			new() { ConcurrencyStamp = secondState.Attempts.Single().ConcurrencyStamp, SubjectScores = [SubjectScore("science", 81)] }));
		firstState = await service.RecordResultAsync(firstState.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = firstState.Attempts.Single().ConcurrencyStamp,
			SubjectScores = [SubjectScore("english", 10), SubjectScore("maths", 50)]
		});
		secondState = await service.RecordResultAsync(secondState.Attempts.Single().Id, new()
		{
			ConcurrencyStamp = secondState.Attempts.Single().ConcurrencyStamp, SubjectScores = [SubjectScore("science", 60)]
		});
		firstState.Outcome.ShouldBe("failed");
		firstState.CanComplete.ShouldBeFalse();
		secondState.Outcome.ShouldBe("passed");
		secondState.CanComplete.ShouldBeTrue();
		var roster = await service.GetApplicantsAsync(new() { SessionId = confirmation.Session.Id });
		roster.Items.Count.ShouldBe(2);
		roster.Items.Single(item => item.ApplicationId == first.Id).Assessment.EffectiveProfile!.Name.ShouldBe("Lower class entrance");
		roster.Items.Single(item => item.ApplicationId == second.Id).Assessment.EffectiveProfile!.Name.ShouldBe("Upper class entrance");
		roster.Items.Single(item => item.ApplicationId == first.Id).Assessment.Attempts.Single().SubjectScores.Select(score => score.Code).ShouldBe(["english", "maths"]);
		roster.Items.Single(item => item.ApplicationId == second.Id).Assessment.Attempts.Single().SubjectScores.Single().Code.ShouldBe("science");
		(await WithUnitOfWorkAsync(async () => (await attempts.GetAsync(firstState.Attempts.Single().Id)).PolicySnapshotJson)).ShouldBe(frozenFirst);
		(await WithUnitOfWorkAsync(async () => (await attempts.GetAsync(secondState.Attempts.Single().Id)).PolicySnapshotJson)).ShouldBe(frozenSecond);
		(await service.GetSessionAsync(confirmation.Session.Id)).BookedCount.ShouldBe(2);
	}

	[Fact]
	public async Task Subject_assessment_offering_rejects_a_class_without_a_configured_profile()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (workflow, first, _) = await CreateSharedSubjectOfferingsAsync();
		var unconfiguredClass = await WithUnitOfWorkAsync(async () =>
		{
			var standard = await _standardManager.CreateAsync(first.StageId, $"Unconfigured {Guid.NewGuid():N}"[..30], 12, null);
			return await _standardRepository.InsertAsync(standard, true);
		});
		var input = SharedSubjectOfferingInput(first, workflow.Id, unconfiguredClass.Id);
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var validation = await service.ValidateAsync(new()
		{
			AdmissionCycleId = first.AdmissionCycleId, BoardId = input.BoardId, MediumId = input.MediumId, StageId = input.StageId, StandardId = input.StandardId,
			Code = input.Code, Name = input.Name, UseCycleDates = true, WorkflowDefinitionId = workflow.Id, InquiryFormDefinitionId = input.InquiryFormDefinitionId
		});
		validation.Errors.ShouldContain(error => error.Code == AdmissionOfferingValidationCodes.AssessmentProfileMissing);
		await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(first.AdmissionCycleId, input));
		(await WithUnitOfWorkAsync(() => _offeringRepository.CountAsync(offering => offering.Code == input.Code))).ShouldBe(0);
	}

	private async Task<(AdmissionWorkflowDto Workflow, AdmissionOfferingDto First, AdmissionOfferingDto Second)> CreateSharedSubjectOfferingsAsync()
	{
		var (_, _, source) = await CreateManagedAssessmentApplicationAsync();
		var additionalClass = await WithUnitOfWorkAsync(async () =>
		{
			var standard = await _standardManager.CreateAsync(source.StageId, $"Upper entrance {Guid.NewGuid():N}"[..30], 11, null);
			return await _standardRepository.InsertAsync(standard, true);
		});
		var assessment = Guid.NewGuid(); var form = Guid.NewGuid(); var decision = Guid.NewGuid();
		var policy = new AdmissionAssessmentPolicy { Version = 2, Enabled = true, SchedulingEnabled = true, AttendanceRequired = true, ScoringEnabled = true, ScoringMode = "subjects", MaxAttempts = 2,
			ClassProfiles = [new() { Id = "lower", Name = "Lower class entrance", BoardId = source.BoardId, MediumId = source.MediumId, StandardIds = [source.StandardId], PassingMarks = 40, PassRule = "both",
				Subjects = [new() { Code = "english", Name = "English", TotalMarks = 50, PassingMarks = 20 }, new() { Code = "maths", Name = "Maths", TotalMarks = 50, PassingMarks = 20 }] },
				new() { Id = "upper", Name = "Upper class entrance", BoardId = source.BoardId, MediumId = source.MediumId, StandardIds = [additionalClass.Id], PassingMarks = 60, PassRule = "overall",
					Subjects = [new() { Code = "science", Name = "General science", TotalMarks = 80 }] }] };
		var draft = await _workflowAppService.CreateAsync(new()
		{
			Name = "Shared class subject journey", Code = $"SHARED_SUB_{Guid.NewGuid():N}"[..24], DefaultInquiryFormDefinitionId = source.InquiryFormDefinitionId,
			Stages = [new() { Id = assessment, Code = "ASSESSMENT", Name = "Entrance assessment", StageType = AdmissionWorkflowStageType.Assessment, IsEntryStage = true, IsRequired = true,
				ConfigurationJson = JsonSerializer.Serialize(new { assessment = policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) },
				new() { Id = form, Code = "FORM", Name = "Admission form", StageType = AdmissionWorkflowStageType.Form, DisplayOrder = 1, IsRequired = true, ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = source.ApplicationFormDefinitionId }) },
				new() { Id = decision, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 2, IsRequired = true, IsTerminalStage = true }],
			Transitions = [new() { FromStageId = assessment, ToStageId = form, IsDefault = true }, new() { FromStageId = form, ToStageId = decision, IsDefault = true }]
		});
		var workflow = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var first = await UpdateSharedSubjectOfferingAsync(source.Id, workflow.Id);
		var second = await GetRequiredService<IAdmissionOfferingAppService>().CreateAsync(first.AdmissionCycleId, SharedSubjectOfferingInput(first, workflow.Id, additionalClass.Id));
		return (workflow, first, second);
	}

	private async Task<AdmissionOfferingDto> UpdateSharedSubjectOfferingAsync(Guid id, Guid workflowId)
	{
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var stored = await service.GetAsync(id);
		return await service.UpdateAsync(id, new() { Name = stored.Name, Code = stored.Code, Track = stored.Track, EntryMode = stored.EntryMode,
			CampusId = stored.CampusId, BoardId = stored.BoardId, MediumId = stored.MediumId, StageId = stored.StageId, StandardId = stored.StandardId,
			UseCycleDates = true, WorkflowDefinitionId = workflowId, InquiryFormDefinitionId = stored.InquiryFormDefinitionId,
			IsActive = stored.IsActive, ConcurrencyStamp = stored.ConcurrencyStamp });
	}

	private static CreateAdmissionOfferingDto SharedSubjectOfferingInput(AdmissionOfferingDto source, Guid workflowId, Guid standardId) => new()
	{
		Name = "Shared subject class offering", Code = $"SHARED_{Guid.NewGuid():N}"[..24], CampusId = source.CampusId, BoardId = source.BoardId, MediumId = source.MediumId,
		StageId = source.StageId, StandardId = standardId, UseCycleDates = true, WorkflowDefinitionId = workflowId, InquiryFormDefinitionId = source.InquiryFormDefinitionId
	};
}
