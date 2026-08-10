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
public class AdmissionCrmOperationsApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Assign_Recalculate_Archive_And_Reopen_Crm_Lead()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);

		var assignResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = ownerUserId },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignResponse.Content.ReadAsStringAsync());
		var assigned = await assignResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		assigned.ShouldNotBeNull();
		assigned.OwnerUserId.ShouldBe(ownerUserId);

		var clearOwnerResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = null },
			JsonOptions);
		clearOwnerResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await clearOwnerResponse.Content.ReadAsStringAsync());
		var unassigned = await clearOwnerResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		unassigned.ShouldNotBeNull();
		unassigned.OwnerUserId.ShouldBeNull();

		var reassignResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = ownerUserId },
			JsonOptions);
		reassignResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reassignResponse.Content.ReadAsStringAsync());

		var scoreResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/score/recalculate",
			content: null);
		scoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scoreResponse.Content.ReadAsStringAsync());
		var scored = await scoreResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		scored.ShouldNotBeNull();
		scored.LeadScore.ShouldBeGreaterThan(0);

		var archiveResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/archive",
			new ArchiveAdmissionCrmLeadDto { Reason = "Family is not ready this cycle." },
			JsonOptions);
		archiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await archiveResponse.Content.ReadAsStringAsync());
		var archived = await archiveResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		archived.ShouldNotBeNull();
		archived.Stage.ShouldBe(AdmissionInquiryStage.Archived);
		archived.ArchiveReason.ShouldBe("Family is not ready this cycle.");

		var reopenResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/reopen",
			new ReopenAdmissionCrmLeadDto { Reason = "Parent called back for follow-up." },
			JsonOptions);
		reopenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reopenResponse.Content.ReadAsStringAsync());
		var reopened = await reopenResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		reopened.ShouldNotBeNull();
		reopened.Stage.ShouldBe(AdmissionInquiryStage.FollowUp);
		reopened.ReopenReason.ShouldBe("Parent called back for follow-up.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_List_And_Complete_Follow_Up_Task()
	{
		var lead = await CreateCrmLeadAsync(
			"Admission inquiry for Grade 5",
			Factory.SeededStandardId);
		var owner = (await GetEligibleCrmOwnersAsync()).First();
		var assignOwnerResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = owner.Id },
			JsonOptions);
		assignOwnerResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await assignOwnerResponse.Content.ReadAsStringAsync());

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks",
			new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = "Call parent for document readiness",
				DueAt = DateTime.Now.AddDays(1),
				AssignedUserId = Guid.Parse(TestAuthHandler.TestUserId),
				Notes = "Ask for previous school report card."
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var task = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmFollowUpTaskDto>(JsonOptions);
		task.ShouldNotBeNull();
		task.LeadId.ShouldBe(lead.Id);
		task.Status.ShouldBe(AdmissionCrmTaskStatus.Pending);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		tasks.Items.Select(x => x.Id).ShouldContain(task.Id);

		var myWorkResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/my-work?skipCount=0&maxResultCount=25");
		myWorkResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await myWorkResponse.Content.ReadAsStringAsync());
		var myWork = await myWorkResponse.Content.ReadFromJsonAsync<AdmissionCrmMyWorkDto>(JsonOptions);
		myWork.ShouldNotBeNull();
		myWork.Items.ShouldContain(item => item.LeadId == lead.Id && item.Title == task.Title);
		var workItem = myWork.Items.Single(item => item.LeadId == lead.Id && item.Title == task.Title);
		workItem.TargetClassName.ShouldNotBeNullOrWhiteSpace();
		workItem.OwnerDisplayName.ShouldNotBeNullOrWhiteSpace();

		var completeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/tasks/{task.Id}/complete",
			new CompleteAdmissionCrmFollowUpTaskDto { CompletionNotes = "Parent confirmed availability." },
			JsonOptions);
		completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await completeResponse.Content.ReadAsStringAsync());
		var completed = await completeResponse.Content.ReadFromJsonAsync<AdmissionCrmFollowUpTaskDto>(JsonOptions);
		completed.ShouldNotBeNull();
		completed.Status.ShouldBe(AdmissionCrmTaskStatus.Completed);
		completed.CompletionNotes.ShouldBe("Parent confirmed availability.");
		completed.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Use_Stage_Sla_Target_For_Sla_Reminder_Task()
	{
		var stagesResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/pipeline-stages");
		stagesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stagesResponse.Content.ReadAsStringAsync());
		var stages = await stagesResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmPipelineStageDto>>(JsonOptions);
		stages.ShouldNotBeNull();
		var newInquiryStage = stages.Items.Single(x => x.SystemStage == AdmissionInquiryStage.NewInquiry);
		var originalStage = new UpdateAdmissionCrmPipelineStageDto
		{
			StageName = newInquiryStage.StageName,
			DisplayOrder = newInquiryStage.DisplayOrder,
			IsEnabled = newInquiryStage.IsEnabled,
			ColorHex = newInquiryStage.ColorHex,
			SlaTargetHours = newInquiryStage.SlaTargetHours,
			OwnerResponsibility = newInquiryStage.OwnerResponsibility,
			AllowedNextStages = [.. newInquiryStage.AllowedNextStages]
		};

		try
		{
			var updateStageResponse = await PutPipelineStageWithImpactAsync(
				Factory.SeededAcademicYearId,
				newInquiryStage.Id,
				new UpdateAdmissionCrmPipelineStageDto
				{
					StageName = newInquiryStage.StageName,
					DisplayOrder = newInquiryStage.DisplayOrder,
					IsEnabled = true,
					ColorHex = newInquiryStage.ColorHex,
					SlaTargetHours = 8,
					OwnerResponsibility = newInquiryStage.OwnerResponsibility,
					AllowedNextStages = [.. newInquiryStage.AllowedNextStages]
				});
			updateStageResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateStageResponse.Content.ReadAsStringAsync());

			var lead = await CreateCrmLeadAsync("Stage SLA inquiry for Grade 5");
			await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-2));

			var runResponse = await Client.PostAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
				content: null);
			runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
			var result = await runResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.StageOverrideCount.ShouldBeGreaterThanOrEqualTo(1);

			var listResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
			listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
			var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
			tasks.ShouldNotBeNull();
			var slaTask = tasks.Items.Single(x => x.Title == "SLA follow-up required");
			slaTask.Notes.ShouldNotBeNull();
			slaTask.Notes!.ShouldContain("after 8 hours without lead activity in NewInquiry");

			var analytics = await GetCrmAnalyticsSummaryAsync();
			analytics.SlaAnalytics.FirstResponseSlaHours.ShouldBe(8);
		}
		finally
		{
			await PutPipelineStageWithImpactAsync(
				Factory.SeededAcademicYearId,
				newInquiryStage.Id,
				originalStage);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Sla_Reminder_Task_And_Skip_Duplicate_Pending_Reminder()
	{
		var lead = await CreateCrmLeadAsync("SLA inquiry for Grade 5");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-5));

		var runResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
		var result = await runResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.CreatedTaskCount.ShouldBeGreaterThanOrEqualTo(1);
		result.SlaThresholdHours.ShouldBe(24);
		result.SlaCutoff.ShouldBeLessThan(DateTimeOffset.UtcNow);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		var slaTask = tasks.Items.Single(x => x.Title == "SLA follow-up required");
		slaTask.Status.ShouldBe(AdmissionCrmTaskStatus.Pending);
		slaTask.Notes.ShouldNotBeNull();
		slaTask.Notes!.ShouldContain("Generated by admissions CRM SLA automation");

		var secondRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		secondRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondRunResponse.Content.ReadAsStringAsync());
		var secondResult = await secondRunResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		secondResult.ShouldNotBeNull();
		secondResult.CreatedTaskCount.ShouldBe(0);
		secondResult.SkippedExistingTaskCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Stale_Owner_Notification_Task_When_Lead_Becomes_Stale()
	{
		var lead = await CreateCrmLeadAsync("Stale inquiry for Grade 5");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-31));

		var runResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/stale-archive/run",
			content: null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
		var result = await runResponse.Content.ReadFromJsonAsync<AdmissionCrmStaleArchiveRunResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.MarkedStaleCount.ShouldBeGreaterThanOrEqualTo(1);
		result.CreatedOwnerNotificationTaskCount.ShouldBeGreaterThanOrEqualTo(1);

		var getLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		getLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getLeadResponse.Content.ReadAsStringAsync());
		var staleLead = await getLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staleLead.ShouldNotBeNull();
		staleLead.Stage.ShouldBe(AdmissionInquiryStage.Stale);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		var staleTasks = tasks.Items.Where(x => x.Title == "Stale lead follow-up required").ToList();
		staleTasks.Count.ShouldBe(1);
		staleTasks[0].Status.ShouldBe(AdmissionCrmTaskStatus.Pending);
		staleTasks[0].Notes.ShouldNotBeNull();
		staleTasks[0].Notes!.ShouldContain("Generated by admissions CRM stale automation");

		var secondRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/stale-archive/run",
			content: null);
		secondRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondRunResponse.Content.ReadAsStringAsync());

		var secondListResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		secondListResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondListResponse.Content.ReadAsStringAsync());
		var secondTasks = await secondListResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		secondTasks.ShouldNotBeNull();
		secondTasks.Items.Count(x => x.Title == "Stale lead follow-up required").ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Run_Stale_And_Sla_Rules_From_Background_Automation_Path()
	{
		var lead = await CreateCrmLeadAsync("Background automation inquiry for Grade 5");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-31));

		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var enrollmentAutomation = scope.ServiceProvider.GetRequiredService<AdmissionCrmEnrollmentAutomationCoordinator>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var staleResult = await enrollmentAutomation.RunStaleArchiveRulesAsync(
				Factory.SeededAcademicYearId);
			var slaResult = await enrollmentAutomation.RunSlaReminderRulesAsync(
				Factory.SeededAcademicYearId);
			await uow.CompleteAsync();

			staleResult.MarkedStaleCount.ShouldBeGreaterThanOrEqualTo(1);
			staleResult.CreatedOwnerNotificationTaskCount.ShouldBeGreaterThanOrEqualTo(1);
			slaResult.CreatedTaskCount.ShouldBeGreaterThanOrEqualTo(1);
		}

		var getLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		getLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getLeadResponse.Content.ReadAsStringAsync());
		var staleLead = await getLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staleLead.ShouldNotBeNull();
		staleLead.Stage.ShouldBe(AdmissionInquiryStage.Stale);

		var analytics = await GetCrmAnalyticsSummaryAsync();
		analytics.AutomationAnalytics.RunCount.ShouldBeGreaterThanOrEqualTo(2);
		analytics.AutomationAnalytics.RecentRuns.Any(x =>
			x.RunType == AdmissionCrmAutomationRunType.StaleArchive &&
			x.MarkedStaleCount >= 1 &&
			x.CreatedTaskCount >= 1).ShouldBeTrue();
		analytics.AutomationAnalytics.RecentRuns.Any(x =>
			x.RunType == AdmissionCrmAutomationRunType.SlaReminder &&
			x.CreatedTaskCount >= 1).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Pause_And_Resume_Working_Time_Sla_With_Audit_History()
	{
		var lead = await CreateCrmLeadAsync("SLA hold inquiry for Grade 6");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-31));

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/sla-holds",
			new CreateAdmissionCrmSlaHoldDto
			{
				HoldReason = AdmissionCrmSlaHoldReason.ExternalAuthority,
				Reason = "Awaiting a governed certificate from the issuing authority."
			}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var hold = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaHoldDto>(JsonOptions);
		hold.ShouldNotBeNull();
		hold.IsActive.ShouldBeTrue();

		var pausedRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		pausedRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pausedRunResponse.Content.ReadAsStringAsync());
		var pausedRun = await pausedRunResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		pausedRun.ShouldNotBeNull();
		pausedRun.PausedLeadCount.ShouldBeGreaterThanOrEqualTo(1);

		var pausedTasksResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=20");
		var pausedTasks = await pausedTasksResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		pausedTasks.ShouldNotBeNull();
		pausedTasks.Items.ShouldNotContain(x => x.Title.Contains("SLA", StringComparison.OrdinalIgnoreCase));

		var resumeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/sla-holds/{hold.Id}/resume",
			new ResumeAdmissionCrmSlaHoldDto { Reason = "Authority evidence received and verified." }, JsonOptions);
		resumeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resumeResponse.Content.ReadAsStringAsync());

		var resumedRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		resumedRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resumedRunResponse.Content.ReadAsStringAsync());
		var resumedRun = await resumedRunResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		resumedRun.ShouldNotBeNull();
		resumedRun.CreatedTaskCount.ShouldBeGreaterThanOrEqualTo(1);

		var historyResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/sla-holds");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = await historyResponse.Content.ReadFromJsonAsync<List<AdmissionCrmSlaHoldDto>>(JsonOptions);
		history.ShouldNotBeNull();
		history.ShouldContain(x => x.Id == hold.Id && !x.IsActive && x.ResumeReason != null);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Record_Crm_Timeline_For_Lead_Task_And_Exam_Actions()
	{
		var lead = await CreateCrmLeadAsync("Timeline inquiry for Grade 4");
		var createTaskResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks",
			new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = "Timeline parent follow-up",
				DueAt = DateTime.Now.AddDays(1)
			},
			JsonOptions);
		createTaskResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createTaskResponse.Content.ReadAsStringAsync());
		var task = await createTaskResponse.Content.ReadFromJsonAsync<AdmissionCrmFollowUpTaskDto>(JsonOptions);
		task.ShouldNotBeNull();

		var completeTaskResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/tasks/{task.Id}/complete",
			new CompleteAdmissionCrmFollowUpTaskDto { CompletionNotes = "Timeline follow-up completed." },
			JsonOptions);
		completeTaskResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await completeTaskResponse.Content.ReadAsStringAsync());

		var slot = await CreateAdmissionCrmExamSlotAsync("Timeline", studentLimit: 2);
		await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		var eventTypes = timeline.Items.Select(x => x.EventType).ToList();
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.LeadCreated);
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.TaskCreated);
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.TaskCompleted);
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.ExamScheduled);

		var leadCreatedEvent = timeline.Items.Single(x => x.EventType == AdmissionCrmTimelineEventType.LeadCreated);
		leadCreatedEvent.Details.ShouldNotBeNull();
		leadCreatedEvent.Details.ShouldContain("Channel: Walk-in");
		leadCreatedEvent.Details.ShouldNotContain(nameof(AdmissionInquiryChannel.WalkInReception));

		var scheduleEvent = timeline.Items.Single(x => x.EventType == AdmissionCrmTimelineEventType.ExamScheduled);
		scheduleEvent.Title.ShouldBe("Entrance exam scheduled");
		scheduleEvent.RelatedEntityId.ShouldNotBeNull();
		scheduleEvent.ActorUserId.ShouldNotBeNull();
	}

}
