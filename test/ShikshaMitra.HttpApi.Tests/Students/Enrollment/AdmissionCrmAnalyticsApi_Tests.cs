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
public class AdmissionCrmAnalyticsApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Crm_Analytics_Summary_For_Lead_Source_Task_And_Exam_Funnel()
	{
		var before = await GetCrmAnalyticsSummaryAsync();
		var beforeQuickCall = before.ChannelPerformance
			.FirstOrDefault(x => x.Channel == AdmissionInquiryChannel.QuickCallEntry);
		var beforeEligibleStage = before.StageFunnel
			.First(x => x.Stage == AdmissionInquiryStage.Eligible);
		var duplicateContact = "97777" + Random.Shared.Next(10000, 99999);
		var analyticsClassName = "Analytics Class " + Guid.NewGuid().ToString("N")[..6];
		var campaignName = "Analytics Campaign " + Guid.NewGuid().ToString("N")[..6];
		var firstLead = await CreateCrmLeadWithContactAsync(
			"Analytics inquiry for Grade 4",
			duplicateContact,
			AdmissionInquiryChannel.QuickCallEntry,
			analyticsClassName,
			campaignName);
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var assignResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = ownerUserId },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignResponse.Content.ReadAsStringAsync());

		var secondLead = await CreateCrmLeadWithContactAsync(
			"Analytics duplicate inquiry for Grade 4",
			duplicateContact,
			AdmissionInquiryChannel.QuickCallEntry,
			analyticsClassName,
			campaignName,
			duplicateContinueReason: "Parent submitted a second inquiry; retained to validate duplicate analytics.");
		var campaignListResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads" +
			$"?campaignName={Uri.EscapeDataString(campaignName)}&maxResultCount=20");
		campaignListResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await campaignListResponse.Content.ReadAsStringAsync());
		var campaignList = await campaignListResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmLeadDto>>(JsonOptions);
		campaignList.ShouldNotBeNull();
		campaignList.TotalCount.ShouldBe(2);
		campaignList.Items.All(x => x.CampaignName == campaignName).ShouldBeTrue();
		var thirdLead = await CreateCrmLeadForClassAsync(
			"Analytics override inquiry for Grade 4",
			analyticsClassName);
		var absentLead = await CreateCrmLeadForClassAsync(
			"Analytics absent inquiry for Grade 4",
			analyticsClassName);
		var unansweredLead = await CreateCrmLeadForClassAsync(
			"Analytics unanswered first response inquiry for Grade 4",
			analyticsClassName);
		await AgeCrmLeadCreationAsync(unansweredLead.Id, DateTime.UtcNow.AddHours(-30));
		await CreateCrmLeadForClassAsync(
			"Analytics walk-in inquiry for Grade 5",
			"Analytics Walk-in " + Guid.NewGuid().ToString("N")[..6]);

		var taskResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/tasks",
			new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = "Call assigned parent",
				DueAt = DateTime.Now.AddDays(-1),
				Notes = "Validate assigned owner workload."
			},
			JsonOptions);
		taskResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await taskResponse.Content.ReadAsStringAsync());
		var noteResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/notes",
			new CreateAdmissionCrmLeadNoteDto
			{
				IsCallNote = true,
				Note = "Parent was called and received entrance exam details."
			},
			JsonOptions);
		noteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await noteResponse.Content.ReadAsStringAsync());

		var slot = await CreateAdmissionCrmExamSlotAsync("Analytics", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(firstLead.Id, slot.Id);
		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 81,
				MeritRank = 1
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());

		var waitlistExam = await ScheduleCrmEntranceExamAsync(secondLead.Id, slot.Id);
		var waitlistResultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{waitlistExam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				MeritRank = 2
			},
			JsonOptions);
		waitlistResultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await waitlistResultResponse.Content.ReadAsStringAsync());

		var waitlistDecisionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{waitlistExam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Waitlisted,
				Reason = "Qualified but waiting for class capacity.",
				MeritRank = 2
			},
			JsonOptions);
		waitlistDecisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await waitlistDecisionResponse.Content.ReadAsStringAsync());

		var overrideExam = await ScheduleCrmEntranceExamAsync(
			thirdLead.Id,
			slot.Id,
			overrideCapacity: true,
			capacityOverrideReason: "Analytics test verifies capacity override reporting.");
		var overrideResultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{overrideExam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 31
			},
			JsonOptions);
		overrideResultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideResultResponse.Content.ReadAsStringAsync());

		var overrideDecisionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{overrideExam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Waitlisted,
				Reason = "Manual review placed the applicant on waitlist.",
				OverrideDecision = true,
				OverrideReason = "Principal approved waitlist after interview.",
				MeritRank = 3
			},
			JsonOptions);
		overrideDecisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideDecisionResponse.Content.ReadAsStringAsync());

		var absentExam = await ScheduleCrmEntranceExamAsync(
			absentLead.Id,
			slot.Id,
			overrideCapacity: true,
			capacityOverrideReason: "Analytics test verifies second override reporting.");
		var absentResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{absentExam.Id}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.Absent
			},
			JsonOptions);
		absentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await absentResponse.Content.ReadAsStringAsync());

		var after = await GetCrmAnalyticsSummaryAsync();
		var afterQuickCall = after.ChannelPerformance
			.First(x => x.Channel == AdmissionInquiryChannel.QuickCallEntry);
		var afterEligibleStage = after.StageFunnel
			.First(x => x.Stage == AdmissionInquiryStage.Eligible);
		var unassignedSource = after.SourcePerformance.First(x => x.SourceName == "Unassigned");
		var campaignPerformance = after.CampaignPerformance.FirstOrDefault(x =>
			x.CampaignName == campaignName);
		var ownerWorkload = after.StaffWorkload.FirstOrDefault(x => x.OwnerUserId == ownerUserId);
		var classPerformance = after.ExamAnalytics.ClassPerformance
			.FirstOrDefault(x => x.ClassName == analyticsClassName);

		after.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		after.TotalLeadCount.ShouldBeGreaterThanOrEqualTo(before.TotalLeadCount + 6);
		after.DuplicateWarningCount.ShouldBeGreaterThanOrEqualTo(before.DuplicateWarningCount + 2);
		after.DuplicateWarningRate.ShouldBeGreaterThan(0);
		after.ExamScheduledLeadCount.ShouldBeGreaterThanOrEqualTo(before.ExamScheduledLeadCount + 4);
		after.ExamScheduledRate.ShouldBeGreaterThan(0);
		after.EligibilityHandoffCount.ShouldBeGreaterThanOrEqualTo(before.EligibilityHandoffCount + 3);
		after.EligibilityHandoffRate.ShouldBeGreaterThan(0);
		after.StaleLeadCount.ShouldBeGreaterThanOrEqualTo(before.StaleLeadCount);
		after.ArchivedLeadCount.ShouldBeGreaterThanOrEqualTo(before.ArchivedLeadCount);
		after.StaleOrArchivedLeadCount.ShouldBe(
			after.StaleLeadCount + after.ArchivedLeadCount);
		after.SlaAnalytics.FirstResponseSlaHours.ShouldBeGreaterThan(0);
		after.SlaAnalytics.FirstResponseCount.ShouldBeGreaterThanOrEqualTo(before.SlaAnalytics.FirstResponseCount + 1);
		after.SlaAnalytics.FirstResponseSlaMetCount.ShouldBeGreaterThanOrEqualTo(before.SlaAnalytics.FirstResponseSlaMetCount + 1);
		after.SlaAnalytics.FirstResponseSlaMissedCount.ShouldBeGreaterThanOrEqualTo(before.SlaAnalytics.FirstResponseSlaMissedCount + 1);
		after.SlaAnalytics.FirstResponseRate.ShouldBeGreaterThan(0);
		after.SlaAnalytics.FirstResponseSlaMetRate.ShouldBeGreaterThan(0);
		after.SlaAnalytics.FirstResponseSlaMissedRate.ShouldBeGreaterThan(0);
		after.SlaAnalytics.AverageFirstResponseHours.ShouldBeGreaterThanOrEqualTo(0);
		afterEligibleStage.LeadCount.ShouldBeGreaterThanOrEqualTo(beforeEligibleStage.LeadCount + 1);
		afterQuickCall.LeadCount.ShouldBeGreaterThanOrEqualTo((beforeQuickCall?.LeadCount ?? 0) + 2);
		afterQuickCall.ExamScheduledCount.ShouldBeGreaterThanOrEqualTo((beforeQuickCall?.ExamScheduledCount ?? 0) + 2);
		afterQuickCall.EligibleCount.ShouldBeGreaterThanOrEqualTo((beforeQuickCall?.EligibleCount ?? 0) + 1);
		afterQuickCall.WaitlistedCount.ShouldBeGreaterThanOrEqualTo((beforeQuickCall?.WaitlistedCount ?? 0) + 1);
		unassignedSource.LeadCount.ShouldBeGreaterThan(0);
		campaignPerformance.ShouldNotBeNull();
		campaignPerformance.LeadCount.ShouldBe(2);
		campaignPerformance.ExamScheduledCount.ShouldBe(2);
		campaignPerformance.EligibleCount.ShouldBe(1);
		campaignPerformance.WaitlistedCount.ShouldBe(1);
		campaignPerformance.EligibilityRate.ShouldBe(50);
		campaignPerformance.AverageLeadScore.ShouldBeGreaterThan(0);
		ownerWorkload.ShouldNotBeNull();
		ownerWorkload.LeadCount.ShouldBeGreaterThanOrEqualTo(1);
		ownerWorkload.PendingTaskCount.ShouldBeGreaterThanOrEqualTo(1);
		ownerWorkload.OverdueTaskCount.ShouldBeGreaterThanOrEqualTo(1);
		ownerWorkload.ExamScheduledCount.ShouldBeGreaterThanOrEqualTo(1);
		after.StaffWorkload.Any(x => x.OwnerLabel == "Unassigned").ShouldBeTrue();
		after.TaskAnalytics.PendingCount.ShouldBeGreaterThanOrEqualTo(before.TaskAnalytics.PendingCount + 1);
		after.TaskAnalytics.OverdueCount.ShouldBeGreaterThanOrEqualTo(before.TaskAnalytics.OverdueCount + 1);
		after.ExamAnalytics.ScheduledCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.ScheduledCount + 4);
		after.ExamAnalytics.PresentCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.PresentCount + 3);
		after.ExamAnalytics.AbsentCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.AbsentCount + 1);
		after.ExamAnalytics.PassCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.PassCount + 2);
		after.ExamAnalytics.FailCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.FailCount + 1);
		after.ExamAnalytics.EligibleCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.EligibleCount + 1);
		after.ExamAnalytics.WaitlistedCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.WaitlistedCount + 2);
		after.ExamAnalytics.RejectedCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.RejectedCount);
		after.ExamAnalytics.MeritRankedCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.MeritRankedCount + 3);
		after.ExamAnalytics.CapacityOverrideCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.CapacityOverrideCount + 2);
		after.ExamAnalytics.EligibilityOverrideCount.ShouldBeGreaterThanOrEqualTo(before.ExamAnalytics.EligibilityOverrideCount + 1);
		after.ExamAnalytics.AttendanceRate.ShouldBeGreaterThan(0);
		after.ExamAnalytics.PassRate.ShouldBeGreaterThan(0);
		after.ExamAnalytics.EligibilityRate.ShouldBeGreaterThan(0);
		after.ExamAnalytics.WaitlistRate.ShouldBeGreaterThan(0);
		after.ExamAnalytics.RejectionRate.ShouldBeGreaterThanOrEqualTo(0);
		after.ExamAnalytics.AveragePercentage.ShouldBeGreaterThan(0);
		after.ExamAnalytics.HighestPercentage.ShouldNotBeNull();
		after.ExamAnalytics.LowestPercentage.ShouldNotBeNull();
		classPerformance.ShouldNotBeNull();
		classPerformance.ScheduledCount.ShouldBe(4);
		classPerformance.PresentCount.ShouldBe(3);
		classPerformance.AbsentCount.ShouldBe(1);
		classPerformance.PassCount.ShouldBe(2);
		classPerformance.FailCount.ShouldBe(1);
		classPerformance.EligibleCount.ShouldBe(1);
		classPerformance.WaitlistedCount.ShouldBe(2);
		classPerformance.RejectedCount.ShouldBe(0);
		classPerformance.MeritRankedCount.ShouldBe(3);
		classPerformance.AttendanceRate.ShouldBe(75);
		classPerformance.PassRate.ShouldBe(66.67m);
		classPerformance.EligibilityRate.ShouldBe(25);
		classPerformance.AveragePercentage.ShouldBe(61.33m);

		var trendFromDate = DateTime.UtcNow.AddDays(-1).Date;
		var trendToDate = DateTime.UtcNow.AddDays(1).Date;
		var filteredResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/analytics/summary" +
			$"?campaignName={Uri.EscapeDataString(campaignName)}" +
			$"&channel={(int) AdmissionInquiryChannel.QuickCallEntry}" +
			$"&fromDate={trendFromDate:yyyy-MM-dd}" +
			$"&toDate={trendToDate:yyyy-MM-dd}" +
			"&minimumLeadScore=0&maximumLeadScore=100");
		filteredResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await filteredResponse.Content.ReadAsStringAsync());
		var filtered = await filteredResponse.Content.ReadFromJsonAsync<AdmissionCrmAnalyticsSummaryDto>(JsonOptions);
		filtered.ShouldNotBeNull();
		filtered.TrendFromDate.Date.ShouldBe(trendFromDate);
		filtered.TrendToDate.Date.ShouldBe(trendToDate);
		filtered.TotalLeadCount.ShouldBe(2);
		filtered.SlaAnalytics.FirstResponseCount.ShouldBe(2);
		filtered.SlaAnalytics.FirstResponsePendingCount.ShouldBe(0);
		filtered.SlaAnalytics.FirstResponseSlaMetCount.ShouldBe(2);
		filtered.SlaAnalytics.FirstResponseSlaMissedCount.ShouldBe(0);
		filtered.SlaAnalytics.FirstResponseRate.ShouldBe(100);
		filtered.SlaAnalytics.FirstResponseSlaMetRate.ShouldBe(100);
		filtered.SlaAnalytics.FirstResponseSlaMissedRate.ShouldBe(0);
		filtered.SlaAnalytics.FollowUpSlaMissedCount.ShouldBe(0);
		filtered.SlaAnalytics.FollowUpSlaMissedRate.ShouldBe(0);
		filtered.DuplicateWarningCount.ShouldBe(2);
		filtered.DuplicateWarningRate.ShouldBe(100);
		filtered.ExamScheduledLeadCount.ShouldBe(2);
		filtered.ExamScheduledRate.ShouldBe(100);
		filtered.EligibilityHandoffCount.ShouldBe(2);
		filtered.EligibilityHandoffRate.ShouldBe(100);
		filtered.StaleLeadCount.ShouldBe(0);
		filtered.ArchivedLeadCount.ShouldBe(0);
		filtered.StaleOrArchivedLeadCount.ShouldBe(0);
		filtered.StaleRate.ShouldBe(0);
		filtered.ArchiveRate.ShouldBe(0);
		filtered.StaleOrArchivedRate.ShouldBe(0);
		filtered.CampaignPerformance.Single().CampaignName.ShouldBe(campaignName);
		filtered.CampaignPerformance.Single().LeadCount.ShouldBe(2);
		filtered.DailyTrends.Count.ShouldBe(3);
		filtered.DailyTrends.Sum(x => x.LeadCount).ShouldBeGreaterThanOrEqualTo(2);
		filtered.DailyTrends.Sum(x => x.ExamScheduledCount).ShouldBeGreaterThanOrEqualTo(2);
		filtered.DailyTrends.Sum(x => x.ExamCompletedCount).ShouldBeGreaterThanOrEqualTo(2);
		filtered.DailyTrends.Sum(x => x.EligibleCount).ShouldBeGreaterThanOrEqualTo(1);
		filtered.DailyTrends.Sum(x => x.WaitlistedCount).ShouldBeGreaterThanOrEqualTo(1);
	}

}
