using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmBulkOperationsApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmBulkOperationsApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Isolate_Item_Failures_And_Persist_Successful_Bulk_Work()
	{
		var lead = await CreateLeadAsync();
		var missingLeadId = Guid.NewGuid();
		var ownerId = (await GetEligibleCrmOwnersAsync())[0].Id;
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/lead-owners",
			new AdmissionCrmBulkAssignOwnersDto
			{
				Items =
				[
					new AdmissionCrmBulkLeadOwnerItemDto { LeadId = lead.Id, OwnerUserId = ownerId },
					new AdmissionCrmBulkLeadOwnerItemDto { LeadId = missingLeadId, OwnerUserId = ownerId }
				]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = (await response.Content.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		result.RequestedCount.ShouldBe(2);
		result.SucceededCount.ShouldBe(1);
		result.FailedCount.ShouldBe(1);
		result.Items.Single(x => x.EntityId == missingLeadId).Success.ShouldBeFalse();

		var leadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var updatedLead = (await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
		updatedLead.OwnerUserId.ShouldBe(ownerId);

		var taskResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/tasks",
			new AdmissionCrmBulkCreateTasksDto
			{
				LeadIds = [lead.Id], Title = "Verify original transfer certificate",
				DueAt = DateTime.UtcNow.AddDays(2), AssignedUserId = ownerId,
				Notes = "Created from the governed bulk workspace."
			}, JsonOptions);
		taskResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await taskResponse.Content.ReadAsStringAsync());
		var taskResult = (await taskResponse.Content.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		taskResult.SucceededCount.ShouldBe(1);

		var oversized = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();
		var oversizedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/archive",
			new AdmissionCrmBulkArchiveDto { LeadIds = oversized, Reason = "Capacity guard test." }, JsonOptions);
		oversizedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await oversizedResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:InvalidBulkBatch");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Validate_And_Isolate_Bulk_Exam_Attendance_Items()
	{
		var invalidStatusResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exams/attendance",
			new AdmissionCrmBulkExamAttendanceDto
			{
				ExamRecordIds = [Guid.NewGuid()],
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.NotMarked
			}, JsonOptions);
		invalidStatusResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await invalidStatusResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:InvalidBulkExamAttendanceStatus");

		var missingExamId = Guid.NewGuid();
		var missingExamResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exams/attendance",
			new AdmissionCrmBulkExamAttendanceDto
			{
				ExamRecordIds = [missingExamId],
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.Present
			}, JsonOptions);
		missingExamResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await missingExamResponse.Content.ReadAsStringAsync());
		var result = (await missingExamResponse.Content
			.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		result.RequestedCount.ShouldBe(1);
		result.SucceededCount.ShouldBe(0);
		result.FailedCount.ShouldBe(1);
		result.Items.Single().EntityId.ShouldBe(missingExamId);
		result.Items.Single().Success.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Apply_Bulk_Exam_Results_And_Isolate_Item_Failures()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Bulk marks", studentLimit: 3);
		var first = await ScheduleExamAsync((await CreateLeadAsync()).Id, slot.Id);
		var second = await ScheduleExamAsync((await CreateLeadAsync()).Id, slot.Id);
		var missingExamId = Guid.NewGuid();

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exams/results",
			new AdmissionCrmBulkExamResultDto
			{
				ExamRecordIds = [first.Id, second.Id, missingExamId],
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72
			}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = (await response.Content.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		result.RequestedCount.ShouldBe(3);
		result.SucceededCount.ShouldBe(2);
		result.FailedCount.ShouldBe(1);
		result.Items.Single(x => x.EntityId == missingExamId).Success.ShouldBeFalse();

		var firstExam = await GetExamByLeadAsync(first.LeadId);
		var secondExam = await GetExamByLeadAsync(second.LeadId);
		firstExam.ObtainedMarks.ShouldBe(72);
		secondExam.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pass);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Bulk_Exam_Result_Marks()
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exams/results",
			new AdmissionCrmBulkExamResultDto
			{
				ExamRecordIds = [Guid.NewGuid()],
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 120
			}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:InvalidExamObtainedMarks");
	}

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync()
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Grade 4 admission", DesiredClassName = "Grade 4",
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Bulk" + Guid.NewGuid().ToString("N")[..6]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}

	private async Task<AdmissionCrmEntranceExamDto> GetExamByLeadAsync(Guid leadId)
	{
		var response = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{leadId}/exam");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions))!;
	}

	private async Task<AdmissionCrmEntranceExamDto> ScheduleExamAsync(Guid leadId, Guid slotId)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slotId },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions))!;
	}

	private async Task<AdmissionCrmExamSlotDto> CreateAdmissionCrmExamSlotAsync(
		string suffix,
		int studentLimit)
	{
		var start = DateTimeOffset.UtcNow.AddDays(7);
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/catalog/exam-slots",
			new AdmissionCrmExamSlotUpsertDto
			{
				SlotName = $"CRM {suffix} {Guid.NewGuid():N}",
				StartDate = start,
				EndDate = start.AddHours(2),
				StudentLimit = studentLimit,
				RoomNo = Random.Shared.Next(10000, 99999)
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmExamSlotDto>(JsonOptions))!;
	}
}
