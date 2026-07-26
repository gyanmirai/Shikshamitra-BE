using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp.Application.Dtos;
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
	public async Task Should_Persist_Distinct_Exam_Marks_And_Isolate_Item_Failures()
	{
		var firstLead = await CreateLeadAsync();
		var secondLead = await CreateLeadAsync();
		var slot = await CreateExamSlotAsync(studentLimit: 2);
		var firstExam = await ScheduleExamAsync(firstLead.Id, slot.Id);
		var secondExam = await ScheduleExamAsync(secondLead.Id, slot.Id);
		var missingExamId = Guid.NewGuid();
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			new AdmissionCrmBulkExamResultsDto
			{
				Items =
				[
					new AdmissionCrmBulkExamResultItemDto
					{
						ExamRecordId = firstExam.Id,
						TotalMarks = 100,
						PassingMarks = 40,
						ObtainedMarks = 71,
						MeritRank = 2
					},
					new AdmissionCrmBulkExamResultItemDto
					{
						ExamRecordId = secondExam.Id,
						TotalMarks = 100,
						PassingMarks = 40,
						ObtainedMarks = 86,
						MeritRank = 1
					},
					new AdmissionCrmBulkExamResultItemDto
					{
						ExamRecordId = missingExamId,
						TotalMarks = 100,
						PassingMarks = 40,
						ObtainedMarks = 55
					}
				]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = (await response.Content.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		result.RequestedCount.ShouldBe(3);
		result.SucceededCount.ShouldBe(2);
		result.FailedCount.ShouldBe(1);
		result.Items.Single(x => x.EntityId == missingExamId).Success.ShouldBeFalse();

		(await GetExamByLeadAsync(firstLead.Id)).ObtainedMarks.ShouldBe(71);
		(await GetExamByLeadAsync(secondLead.Id)).ObtainedMarks.ShouldBe(86);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Duplicate_Exam_Record_Ids_Before_Processing()
	{
		var lead = await CreateLeadAsync();
		var slot = await CreateExamSlotAsync(studentLimit: 1);
		var exam = await ScheduleExamAsync(lead.Id, slot.Id);
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			new AdmissionCrmBulkExamResultsDto
			{
				Items =
				[
					new AdmissionCrmBulkExamResultItemDto
					{
						ExamRecordId = exam.Id,
						TotalMarks = 100,
						PassingMarks = 40,
						ObtainedMarks = 71
					},
					new AdmissionCrmBulkExamResultItemDto
					{
						ExamRecordId = exam.Id,
						TotalMarks = 100,
						PassingMarks = 40,
						ObtainedMarks = 86
					}
				]
			}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:DuplicateBulkExamResultItems");
		(await GetExamByLeadAsync(lead.Id)).ResultRecordedAt.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Bulk_And_Change_Result_Permissions()
	{
		var lead = await CreateLeadAsync();
		var slot = await CreateExamSlotAsync(studentLimit: 1);
		var exam = await ScheduleExamAsync(lead.Id, slot.Id);
		var input = CreateBulkResultInput(exam.Id, obtainedMarks: 73);

		using var bulkOnlyClient = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations);
		var missingChangePermission = await bulkOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			input,
			JsonOptions);
		missingChangePermission.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await missingChangePermission.Content.ReadAsStringAsync());

		using var changeOnlyClient = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		var missingBulkPermission = await changeOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			input,
			JsonOptions);
		missingBulkPermission.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await missingBulkPermission.Content.ReadAsStringAsync());

		(await GetExamByLeadAsync(lead.Id)).ResultRecordedAt.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Override_Permission_And_Reason_For_Result_Corrections()
	{
		var lead = await CreateLeadAsync();
		var slot = await CreateExamSlotAsync(studentLimit: 1);
		var exam = await ScheduleExamAsync(lead.Id, slot.Id);
		using var resultEditor = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		var initialResponse = await resultEditor.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			CreateBulkResultInput(exam.Id, obtainedMarks: 64),
			JsonOptions);
		initialResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await initialResponse.Content.ReadAsStringAsync());

		var missingOverrideResponse = await resultEditor.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			CreateBulkResultInput(
				exam.Id,
				obtainedMarks: 78,
				correctionReason: "Paper-two marks were omitted."),
			JsonOptions);
		missingOverrideResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await missingOverrideResponse.Content.ReadAsStringAsync());
		var missingOverride = (await missingOverrideResponse.Content
			.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		missingOverride.FailedCount.ShouldBe(1);
		missingOverride.Items.Single().ErrorCode.ShouldBe("AdmissionCrm:BulkItemUnauthorized");

		using var resultOverrideEditor = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult);
		var missingReasonResponse = await resultOverrideEditor.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			CreateBulkResultInput(exam.Id, obtainedMarks: 78),
			JsonOptions);
		missingReasonResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await missingReasonResponse.Content.ReadAsStringAsync());
		var missingReason = (await missingReasonResponse.Content
			.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		missingReason.FailedCount.ShouldBe(1);
		missingReason.Items.Single().ErrorCode.ShouldBe("AdmissionCrm:ExamResultCorrectionReasonRequired");

		const string correctionReason = "Paper-two marks were omitted.";
		var correctedResponse = await resultOverrideEditor.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			CreateBulkResultInput(exam.Id, obtainedMarks: 78, correctionReason),
			JsonOptions);
		correctedResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await correctedResponse.Content.ReadAsStringAsync());
		var corrected = (await correctedResponse.Content
			.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		corrected.SucceededCount.ShouldBe(1);
		(await GetExamByLeadAsync(lead.Id)).ObtainedMarks.ShouldBe(78);

		var timeline = await GetTimelineAsync(lead.Id);
		var correctionEvent = timeline.Items.Single(x => x.Title == "Exam result corrected");
		correctionEvent.Details.ShouldNotBeNull();
		correctionEvent.Details.ShouldContain("Previous: obtained 64/100, passing 40");
		correctionEvent.Details.ShouldContain("New: obtained 78/100, passing 40");
		correctionEvent.Details.ShouldContain(correctionReason);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Treat_Identical_Result_Retry_As_Side_Effect_Safe()
	{
		var lead = await CreateLeadAsync();
		var slot = await CreateExamSlotAsync(studentLimit: 1);
		var exam = await ScheduleExamAsync(lead.Id, slot.Id);
		var input = CreateBulkResultInput(exam.Id, obtainedMarks: 82, meritRank: 1);

		var firstResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			input,
			JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
		var firstExamState = await GetExamByLeadAsync(lead.Id);
		var firstTimeline = await GetTimelineAsync(lead.Id);

		var retryResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/bulk/exam-results",
			input,
			JsonOptions);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await retryResponse.Content.ReadAsStringAsync());
		var retryResult = (await retryResponse.Content
			.ReadFromJsonAsync<AdmissionCrmBulkOperationResultDto>(JsonOptions))!;
		retryResult.SucceededCount.ShouldBe(1);
		var retriedExamState = await GetExamByLeadAsync(lead.Id);
		var retriedTimeline = await GetTimelineAsync(lead.Id);

		retriedExamState.ResultRecordedAt.ShouldBe(firstExamState.ResultRecordedAt);
		firstTimeline.Items.Count(x => x.EventType == AdmissionCrmTimelineEventType.ExamResultRecorded)
			.ShouldBe(1);
		retriedTimeline.Items.Count(x => x.EventType == AdmissionCrmTimelineEventType.ExamResultRecorded)
			.ShouldBe(1);
	}

	private static AdmissionCrmBulkExamResultsDto CreateBulkResultInput(
		Guid examRecordId,
		double obtainedMarks,
		string? correctionReason = null,
		int? meritRank = null)
	{
		return new AdmissionCrmBulkExamResultsDto
		{
			Items =
			[
				new AdmissionCrmBulkExamResultItemDto
				{
					ExamRecordId = examRecordId,
					TotalMarks = 100,
					PassingMarks = 40,
					ObtainedMarks = obtainedMarks,
					MeritRank = meritRank,
					CorrectionReason = correctionReason
				}
			]
		};
	}

	private HttpClient CreatePermissionClient(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", permissions));
		return client;
	}

	private async Task<AdmissionCrmExamSlotDto> CreateExamSlotAsync(int studentLimit)
	{
		var start = DateTimeOffset.UtcNow.AddDays(7);
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/catalog/exam-slots",
			new AdmissionCrmExamSlotUpsertDto
			{
				SlotName = $"Bulk results {Guid.NewGuid():N}",
				StartDate = start,
				EndDate = start.AddHours(2),
				StudentLimit = studentLimit,
				RoomNo = Random.Shared.Next(10000, 99999)
			},
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmExamSlotDto>(JsonOptions))!;
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

	private async Task<AdmissionCrmEntranceExamDto> GetExamByLeadAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/exam");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions))!;
	}

	private async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetTimelineAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/timeline?maxResultCount=100");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmTimelineEventDto>>(JsonOptions))!;
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
}
