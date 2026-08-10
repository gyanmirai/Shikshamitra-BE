using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmExamApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Schedule_Crm_Entrance_Exam_From_Exam_Ready_Lead()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var slot = await CreateAdmissionCrmExamSlotAsync("Exam ready", studentLimit: 2);

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var exam = await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		exam.LeadId.ShouldBe(lead.Id);
		exam.EntranceExamSlotId.ShouldBe(slot.Id);
		exam.ExamRoomNo.ShouldBe(slot.RoomNo);
		exam.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.NotMarked);
		exam.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pending);
		exam.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Pending);

		var searchResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/exams?filterText={Uri.EscapeDataString(exam.ApplicantName)}&maxResultCount=50");
		searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await searchResponse.Content.ReadAsStringAsync());
		var searchResult = await searchResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmEntranceExamDto>>(JsonOptions);
		searchResult.ShouldNotBeNull();
		searchResult.Items.ShouldContain(x => x.Id == exam.Id);

		var leadResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var scheduledLead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		scheduledLead.ShouldNotBeNull();
		scheduledLead.Stage.ShouldBe(AdmissionInquiryStage.ExamScheduled);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preserve_Previous_Slot_Timeline_When_Entrance_Exam_Is_Rescheduled()
	{
		var lead = await CreateCrmLeadAsync("Entrance exam reschedule inquiry");
		var originalSlot = await CreateAdmissionCrmExamSlotAsync("Original slot", studentLimit: 2);
		var replacementSlot = await CreateAdmissionCrmExamSlotAsync("Replacement slot", studentLimit: 2);

		var originalExam = await ScheduleCrmEntranceExamAsync(lead.Id, originalSlot.Id);
		var rescheduledExam = await ScheduleCrmEntranceExamAsync(lead.Id, replacementSlot.Id);

		rescheduledExam.Id.ShouldBe(originalExam.Id);
		rescheduledExam.EntranceExamSlotId.ShouldBe(replacementSlot.Id);
		rescheduledExam.ExamRoomNo.ShouldBe(replacementSlot.RoomNo);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		var scheduleEvents = timeline.Items
			.Where(x => x.EventType == AdmissionCrmTimelineEventType.ExamScheduled)
			.ToList();
		originalSlot.SlotName.ShouldNotBeNull();
		replacementSlot.SlotName.ShouldNotBeNull();
		scheduleEvents.Count.ShouldBe(2);
		scheduleEvents.ShouldContain(x =>
			x.Title == "Entrance exam scheduled" &&
			x.Details != null &&
			x.Details.Contains(originalSlot.SlotName, StringComparison.Ordinal));
		scheduleEvents.ShouldContain(x =>
			x.Title == "Entrance exam rescheduled" &&
			x.Details != null &&
			x.Details.Contains(replacementSlot.SlotName, StringComparison.Ordinal));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Crm_Registration_Form_Before_Exam_When_Configured()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			registrationFormRequiredBeforeExam: true);

		try
		{
			var lead = await CreateCrmLeadAsync("Registration form gated inquiry for Grade 4");
			var slot = await CreateAdmissionCrmExamSlotAsync("Registration gate", studentLimit: 1);
			var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
			var assignResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
				new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = ownerUserId },
				JsonOptions);
			assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignResponse.Content.ReadAsStringAsync());
			var assignedLead = await assignResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			assignedLead.ShouldNotBeNull();
			assignedLead.OwnerUserId.ShouldBe(ownerUserId);
			var followUpResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
				new ChangeAdmissionCrmLeadStageDto
				{
					Stage = AdmissionInquiryStage.FollowUp,
					Reason = "Counsellor completed the first registration follow-up."
				},
				JsonOptions);
			followUpResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await followUpResponse.Content.ReadAsStringAsync());
			var followUpLead = await followUpResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			followUpLead.ShouldNotBeNull();
			followUpLead.Stage.ShouldBe(AdmissionInquiryStage.FollowUp);

			var blockedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
				new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
				JsonOptions);

			blockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			var blockedBody = await blockedResponse.Content.ReadAsStringAsync();
			blockedBody.ShouldContain("AdmissionCrm:RegistrationFormRequiredBeforeExam");

			var formNumber = "CRM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
			var issueResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registration-form",
				new IssueAdmissionCrmRegistrationFormDto
				{
					FormNumber = formNumber,
					Amount = 250
				},
				JsonOptions);

			issueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await issueResponse.Content.ReadAsStringAsync());
			var issuedLead = await issueResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			issuedLead.ShouldNotBeNull();
			issuedLead.RegistrationFormNumber.ShouldBe(formNumber);
			issuedLead.RegistrationFormAmount.ShouldBe(250D);
			issuedLead.RegistrationFormIssuedAt.ShouldNotBeNull();

			var timeline = await GetCrmTimelineAsync(lead.Id);
			var issueEvent = timeline.Items.Single(x =>
				x.EventType == AdmissionCrmTimelineEventType.RegistrationFormIssued);
			issueEvent.Title.ShouldBe("Registration form issued");
			issueEvent.Details!.ShouldContain(formNumber);

			var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);
			exam.LeadId.ShouldBe(lead.Id);
			exam.EntranceExamSlotId.ShouldBe(slot.Id);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				registrationFormRequiredBeforeExam: originalCycle.RegistrationFormRequiredBeforeExam);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Run_Exam_Lifecycle_Journeys_When_Crm_Exam_Changes()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var scheduledTaskTitle = "Exam scheduled journey " + Guid.NewGuid().ToString("N")[..8];
		var stageTaskTitle = "Exam stage journey " + Guid.NewGuid().ToString("N")[..8];
		var completedTaskTitle = "Exam completed journey " + Guid.NewGuid().ToString("N")[..8];
		var eligibilityTaskTitle = "Eligibility journey " + Guid.NewGuid().ToString("N")[..8];
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules:
			[
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Exam scheduled event journey",
					IsEnabled = true,
					Priority = 10,
					Trigger = AdmissionCrmJourneyTrigger.ExamScheduled,
					CreateTask = true,
					TaskTitle = scheduledTaskTitle,
					TaskDueOffsetHours = 1
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Exam scheduled stage journey",
					IsEnabled = true,
					Priority = 20,
					Trigger = AdmissionCrmJourneyTrigger.StageReached,
					Stage = AdmissionInquiryStage.ExamScheduled,
					CreateTask = true,
					TaskTitle = stageTaskTitle,
					TaskDueOffsetHours = 1
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Exam completed event journey",
					IsEnabled = true,
					Priority = 30,
					Trigger = AdmissionCrmJourneyTrigger.ExamCompleted,
					CreateTask = true,
					TaskTitle = completedTaskTitle,
					TaskDueOffsetHours = 1
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Eligibility decision event journey",
					IsEnabled = true,
					Priority = 40,
					Trigger = AdmissionCrmJourneyTrigger.EligibilityDecided,
					CreateTask = true,
					TaskTitle = eligibilityTaskTitle,
					TaskDueOffsetHours = 1
				}
			]);

		try
		{
			var lead = await CreateCrmLeadAsync("Admission journey exam inquiry for Grade 4");
			var slot = await CreateAdmissionCrmExamSlotAsync("Journey lifecycle", studentLimit: 1);
			var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

			var scheduledTasks = await GetCrmTasksAsync(lead.Id);
			scheduledTasks.Items.Select(x => x.Title).ShouldContain(scheduledTaskTitle);
			scheduledTasks.Items.Select(x => x.Title).ShouldContain(stageTaskTitle);

			var replayResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/journeys/run",
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.ExamScheduled,
					LeadId = lead.Id,
					Stage = AdmissionInquiryStage.ExamScheduled
				},
				JsonOptions);
			replayResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await replayResponse.Content.ReadAsStringAsync());
			var replay = await replayResponse.Content
				.ReadFromJsonAsync<AdmissionCrmJourneyRunResultDto>(JsonOptions);
			replay.ShouldNotBeNull();
			replay.CreatedTaskCount.ShouldBe(0);
			replay.SkippedExistingTaskCount.ShouldBe(1);

			var resultResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
				new UploadAdmissionCrmEntranceExamResultDto
				{
					TotalMarks = 100,
					PassingMarks = 40,
					ObtainedMarks = 78,
					MeritRank = 1
				},
				JsonOptions);
			resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
			var result = await resultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pass);

			var resultTasks = await GetCrmTasksAsync(lead.Id);
			resultTasks.Items.Select(x => x.Title).ShouldContain(completedTaskTitle);

			var decisionResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
				new UpdateAdmissionCrmEligibilityDecisionDto
				{
					Decision = AdmissionCrmEligibilityDecision.Waitlisted,
					Reason = "Eligible but waitlisted by journey test.",
					MeritRank = 1
				},
				JsonOptions);
			decisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await decisionResponse.Content.ReadAsStringAsync());

			var finalTasks = await GetCrmTasksAsync(lead.Id);
			finalTasks.Items.Select(x => x.Title).ShouldContain(eligibilityTaskTitle);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules,
				originalCycle.LeadRoutingSettings,
				originalCycle.JourneyRules);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Use_Cycle_Configured_Exam_Readiness_Fields()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields | AdmissionCrmExamReadinessField.ParentGuardianName);

		try
		{
			var lead = await CreateCrmLeadForClassAsync(
				"Strict readiness inquiry",
				"Strict readiness " + Guid.NewGuid().ToString("N")[..8],
				omitParentGuardianName: true);
			var slot = await CreateAdmissionCrmExamSlotAsync("Strict readiness", studentLimit: 1);

			var blockedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
				new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
				JsonOptions);

			blockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			var blockedBody = await blockedResponse.Content.ReadAsStringAsync();
			blockedBody.ShouldContain("AdmissionCrm:ExamReadinessIncomplete");
			blockedBody.ShouldContain("Parent or guardian name");
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(originalCycle, originalCycle.ExamReadinessFields);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Recalculate_Class_Wise_Merit_Ranks_When_Result_Uploaded()
	{
		var desiredClassName = "Merit Class " + Guid.NewGuid().ToString("N")[..8];
		var firstLead = await CreateCrmLeadForClassAsync("Merit ranking inquiry one", desiredClassName);
		var secondLead = await CreateCrmLeadForClassAsync("Merit ranking inquiry two", desiredClassName);
		var slot = await CreateAdmissionCrmExamSlotAsync("Merit ranking", studentLimit: 2);
		var firstExam = await ScheduleCrmEntranceExamAsync(firstLead.Id, slot.Id);
		var secondExam = await ScheduleCrmEntranceExamAsync(secondLead.Id, slot.Id);

		var firstResultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{firstExam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 82
			},
			JsonOptions);
		firstResultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResultResponse.Content.ReadAsStringAsync());
		var firstResult = await firstResultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		firstResult.ShouldNotBeNull();
		firstResult.MeritRank.ShouldBe(1);

		var secondResultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{secondExam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 91
			},
			JsonOptions);
		secondResultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondResultResponse.Content.ReadAsStringAsync());
		var secondResult = await secondResultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		secondResult.ShouldNotBeNull();
		secondResult.MeritRank.ShouldBe(1);

		var updatedFirstExam = await GetCrmEntranceExamByLeadAsync(firstLead.Id);
		updatedFirstExam.MeritRank.ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Prevent_Crm_Exam_Overbooking_Unless_Override_Recorded()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Capacity", studentLimit: 1);
		var firstLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var secondLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");

		var firstResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());

		var blockedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);
		blockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var blockedBody = await blockedResponse.Content.ReadAsStringAsync();
		blockedBody.ShouldContain("AdmissionCrm:ExamSlotCapacityFull");

		var overrideResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto
			{
				EntranceExamSlotId = slot.Id,
				OverrideCapacity = true,
				CapacityOverrideReason = "Principal approved extra chair for sibling applicant."
			},
			JsonOptions);
		overrideResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideResponse.Content.ReadAsStringAsync());
		var exam = await overrideResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		exam.CapacityOverrideUsed.ShouldBeTrue();
		exam.CapacityOverrideReason.ShouldBe("Principal approved extra chair for sibling applicant.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Inactive_Crm_Exam_Slot_Server_Side()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Inactive capacity", studentLimit: 1);
		var lead = await CreateCrmLeadAsync("Inactive exam slot inquiry");

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var slotRepository = scope.ServiceProvider
					.GetRequiredService<IRepository<AdmissionCrmExamSlot, Guid>>();
				var entity = await slotRepository.GetAsync(slot.Id);
				entity.SetActive(false);
				await slotRepository.UpdateAsync(entity, autoSave: true);
				await uow.CompleteAsync();
			}
		}

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:ExamSlotInactive");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Allow_Only_One_Concurrent_Reservation_For_The_Last_Exam_Seat()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Concurrent last seat", studentLimit: 1);
		var firstLead = await CreateCrmLeadAsync("Concurrent seat inquiry one");
		var secondLead = await CreateCrmLeadAsync("Concurrent seat inquiry two");
		var firstRequest = Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);
		var secondRequest = Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);

		var responses = await Task.WhenAll(firstRequest, secondRequest);

		responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
		responses.Count(x => x.StatusCode != HttpStatusCode.OK).ShouldBe(1);
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var examRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmEntranceExamRecord, Guid>>();
			(await examRepository.CountAsync(x => x.EntranceExamSlotId == slot.Id)).ShouldBe(1);
			await uow.CompleteAsync();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preserve_Distinct_Exam_NoShow_And_Excused_Attendance()
	{
		var noShowSlot = await CreateAdmissionCrmExamSlotAsync("No-show attendance", studentLimit: 1);
		var excusedSlot = await CreateAdmissionCrmExamSlotAsync("Excused attendance", studentLimit: 1);
		var noShowLead = await CreateCrmLeadAsync("No-show attendance inquiry");
		var excusedLead = await CreateCrmLeadAsync("Excused attendance inquiry");
		var noShowExam = await ScheduleCrmEntranceExamAsync(noShowLead.Id, noShowSlot.Id);
		var excusedExam = await ScheduleCrmEntranceExamAsync(excusedLead.Id, excusedSlot.Id);

		var noShowResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{noShowExam.Id}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.NoShow
			},
			JsonOptions);
		noShowResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await noShowResponse.Content.ReadAsStringAsync());
		var markedNoShow = await noShowResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		markedNoShow.ShouldNotBeNull();
		markedNoShow.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.NoShow);
		markedNoShow.ExamStatus.ShouldBe(EntranceExamStatusEnum.Absent);

		var excusedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{excusedExam.Id}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.Excused
			},
			JsonOptions);
		excusedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await excusedResponse.Content.ReadAsStringAsync());
		var markedExcused = await excusedResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		markedExcused.ShouldNotBeNull();
		markedExcused.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.Excused);
		markedExcused.ExamStatus.ShouldBe(EntranceExamStatusEnum.Absent);

		var invalidResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{excusedExam.Id}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = (AdmissionCrmExamAttendanceStatus) 999
			},
			JsonOptions);
		invalidResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await invalidResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:InvalidAttendanceStatus");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Override_Permissions_For_Crm_Exam_Capacity_And_Eligibility()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Permission capacity", studentLimit: 1);
		var firstLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var secondLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		await ScheduleCrmEntranceExamAsync(firstLead.Id, slot.Id);

		using var scheduleOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam);
		var capacityOverrideResponse = await scheduleOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto
			{
				EntranceExamSlotId = slot.Id,
				OverrideCapacity = true,
				CapacityOverrideReason = "Permission gate capacity override attempt."
			},
			JsonOptions);

		capacityOverrideResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await capacityOverrideResponse.Content.ReadAsStringAsync());

		var resultLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var resultSlot = await CreateAdmissionCrmExamSlotAsync("Permission eligibility", studentLimit: 1);
		var exam = await ScheduleCrmEntranceExamAsync(resultLead.Id, resultSlot.Id);
		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 35
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());

		using var resultOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		var eligibilityOverrideResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Permission gate eligibility override attempt.",
				OverrideDecision = true,
				OverrideReason = "Principal override should require explicit permission."
			},
			JsonOptions);

		eligibilityOverrideResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await eligibilityOverrideResponse.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Govern_Individual_Crm_Exam_Result_Corrections()
	{
		var lead = await CreateCrmLeadAsync("Individual result correction governance");
		var slot = await CreateAdmissionCrmExamSlotAsync(
			"Individual result correction governance",
			studentLimit: 1);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);
		var initialResult = new UploadAdmissionCrmEntranceExamResultDto
		{
			TotalMarks = 100,
			PassingMarks = 40,
			ObtainedMarks = 68
		};
		var initialResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			initialResult,
			JsonOptions);
		initialResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await initialResponse.Content.ReadAsStringAsync());

		using var resultOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		var identicalRetryResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			initialResult,
			JsonOptions);
		identicalRetryResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await identicalRetryResponse.Content.ReadAsStringAsync());

		var missingReasonResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72
			},
			JsonOptions);
		missingReasonResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingReasonResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ExamResultCorrectionReasonRequired");

		var missingOverridePermissionResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				CorrectionReason = "Marks rechecked against the signed answer sheet."
			},
			JsonOptions);
		missingOverridePermissionResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await missingOverridePermissionResponse.Content.ReadAsStringAsync());

		using var overrideClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult);
		var correctionResponse = await overrideClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				CorrectionReason = "Marks rechecked against the signed answer sheet."
			},
			JsonOptions);
		correctionResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await correctionResponse.Content.ReadAsStringAsync());
		var corrected = await correctionResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		corrected.ShouldNotBeNull();
		corrected.ObtainedMarks.ShouldBe(72);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Record_Crm_Exam_Attendance_Result_And_Waitlist_Decision()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var slot = await CreateAdmissionCrmExamSlotAsync("Result", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var attendanceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.Present
			},
			JsonOptions);
		attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await attendanceResponse.Content.ReadAsStringAsync());
		var attended = await attendanceResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		attended.ShouldNotBeNull();
		attended.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.Present);

		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				MeritRank = 3
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
		var result = await resultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pass);
		result.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		result.Percentage.ShouldBe(72);
		result.MeritRank.ShouldBe(3);

		var eligibleHandoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility-handoff");
		eligibleHandoffResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await eligibleHandoffResponse.Content.ReadAsStringAsync());
		var eligibleHandoff = await eligibleHandoffResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEligibilityHandoffDto>(JsonOptions);
		eligibleHandoff.ShouldNotBeNull();
		eligibleHandoff.LeadId.ShouldBe(lead.Id);
		eligibleHandoff.ExamRecordId.ShouldBe(exam.Id);
		eligibleHandoff.CanStartAdmission.ShouldBeTrue();
		eligibleHandoff.RequiresManualReview.ShouldBeFalse();
		eligibleHandoff.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		eligibleHandoff.ObtainedMarks.ShouldBe(72);
		eligibleHandoff.MeritRank.ShouldBe(3);
		eligibleHandoff.PrimaryContactNo.ShouldBe(lead.PrimaryContactNo);

		var decisionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Waitlisted,
				Reason = "Eligible but waitlisted for class capacity.",
				MeritRank = 3
			},
			JsonOptions);
		decisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await decisionResponse.Content.ReadAsStringAsync());
		var decision = await decisionResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		decision.ShouldNotBeNull();
		decision.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Waitlisted);
		decision.EligibilityDecisionReason.ShouldBe("Eligible but waitlisted for class capacity.");

		var waitlistHandoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility-handoff");
		waitlistHandoffResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await waitlistHandoffResponse.Content.ReadAsStringAsync());
		var waitlistHandoff = await waitlistHandoffResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEligibilityHandoffDto>(JsonOptions);
		waitlistHandoff.ShouldNotBeNull();
		waitlistHandoff.CanStartAdmission.ShouldBeFalse();
		waitlistHandoff.RequiresManualReview.ShouldBeTrue();
		waitlistHandoff.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Waitlisted);

		var leadResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var waitlistedLead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		waitlistedLead.ShouldNotBeNull();
		waitlistedLead.Stage.ShouldBe(AdmissionInquiryStage.Waitlisted);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Show_Missing_Admission_Information_In_Crm_Eligibility_Handoff()
	{
		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Admission inquiry with incomplete admission handoff details",
				FirstName = "Incomplete",
				PrimaryContactNo = "989898" + Random.Shared.Next(1000, 9999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				DesiredClassName = "Grade 4"
			},
			JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		lead.FirstName.ShouldBe("Incomplete");
		lead.ParentGuardianName.ShouldBeNull();

		var slot = await CreateAdmissionCrmExamSlotAsync("Handoff Missing", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 84,
				MeritRank = 2
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());

		var handoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility-handoff");
		handoffResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await handoffResponse.Content.ReadAsStringAsync());
		var handoff = await handoffResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEligibilityHandoffDto>(JsonOptions);
		handoff.ShouldNotBeNull();
		handoff.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		handoff.CanStartAdmission.ShouldBeFalse();
		handoff.RequiresManualReview.ShouldBeTrue();
		handoff.MissingAdmissionFields.ShouldNotContain("Applicant name");
		handoff.MissingAdmissionFields.ShouldContain("Parent or guardian name");
		handoff.MissingAdmissionFields.ShouldNotContain("Contact");
		handoff.MissingAdmissionFields.ShouldNotContain("Admission intent");
		handoff.MissingAdmissionFields.ShouldNotContain("Desired class");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Require_Override_Reason_For_Failed_Crm_Exam_Eligibility_Upgrade()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var slot = await CreateAdmissionCrmExamSlotAsync("Override", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 35,
				MeritRank = 8
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
		var failed = await resultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		failed.ShouldNotBeNull();
		failed.ExamStatus.ShouldBe(EntranceExamStatusEnum.Fail);
		failed.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Rejected);

		var missingOverrideResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Management approved interview-based admission.",
				MeritRank = 8
			},
			JsonOptions);
		missingOverrideResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var missingOverrideBody = await missingOverrideResponse.Content.ReadAsStringAsync();
		missingOverrideBody.ShouldContain("AdmissionCrm:EligibilityOverrideRequired");

		var missingReasonResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Management approved interview-based admission.",
				OverrideDecision = true,
				MeritRank = 8
			},
			JsonOptions);
		missingReasonResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var missingReasonBody = await missingReasonResponse.Content.ReadAsStringAsync();
		missingReasonBody.ShouldContain("AdmissionCrm:EligibilityOverrideReasonRequired");

		var overrideResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Management approved interview-based admission.",
				OverrideDecision = true,
				OverrideReason = "Principal approved after interview and sibling priority.",
				MeritRank = 8
			},
			JsonOptions);
		overrideResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideResponse.Content.ReadAsStringAsync());
		var overridden = await overrideResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		overridden.ShouldNotBeNull();
		overridden.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		overridden.EligibilityOverrideUsed.ShouldBeTrue();
		overridden.EligibilityOverrideReason.ShouldBe("Principal approved after interview and sibling priority.");
	}

}
