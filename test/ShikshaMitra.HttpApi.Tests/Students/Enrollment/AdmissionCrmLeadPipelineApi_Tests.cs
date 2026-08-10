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
public class AdmissionCrmLeadPipelineApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Minimal_Crm_Lead_Without_Dob_Or_Address()
	{
		var input = new CreateAdmissionCrmLeadDto
		{
			AdmissionIntent = "Admission inquiry for Grade 1",
			PrimaryContactNo = "9876543210",
			Channel = AdmissionInquiryChannel.QuickCallEntry,
			FirstName = "Aarav"
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		lead.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		lead.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);
		lead.PrimaryContactNo.ShouldBe("9876543210");
		lead.AdmissionIntent.ShouldBe(input.AdmissionIntent);
		lead.LikelyDuplicateCount.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Preview_Duplicate_By_Contact_Number()
	{
		var input = new CreateAdmissionCrmLeadDto
		{
			AdmissionIntent = "Admission inquiry for Grade 2",
			PrimaryContactNo = "98765 43211",
			Channel = AdmissionInquiryChannel.WalkInReception,
			FirstName = "Isha"
		};

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			input,
			JsonOptions);
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		created.ShouldNotBeNull();

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads/duplicate-preview",
			new AdmissionCrmLeadDuplicatePreviewInput
			{
				PrimaryContactNo = "9876543211",
				AdmissionIntent = "Second inquiry for same family"
			},
			JsonOptions);

		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var duplicates = await previewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmLeadDuplicateDto[]>(JsonOptions);
		duplicates.ShouldNotBeNull();
		duplicates.Select(x => x.LeadId).ShouldContain(created.Id);
		var duplicate = duplicates.Single(x => x.LeadId == created.Id);
		duplicate.MatchReason.ShouldBe("Contact number");
		duplicate.ConfidenceScore.ShouldBe(100);
		duplicate.MatchedFields.ShouldContain("Contact number");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Preview_Duplicate_By_Applicant_Parent_And_Class()
	{
		var parentName = "Profile Parent " + Guid.NewGuid().ToString("N")[..8];
		var desiredClassName = "Profile Grade " + Guid.NewGuid().ToString("N")[..6];
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Profile duplicate original inquiry",
				PrimaryContactNo = "91000" + Random.Shared.Next(10000, 99999),
				Email = "profile.original." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = AdmissionInquiryChannel.WalkInReception,
				FirstName = "Kavya",
				LastName = "Sharma",
				ParentGuardianName = parentName,
				DesiredClassName = desiredClassName
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		created.ShouldNotBeNull();

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads/duplicate-preview",
			new AdmissionCrmLeadDuplicatePreviewInput
			{
				FirstName = "Kavya",
				LastName = "Sharma",
				ParentGuardianName = parentName,
				PrimaryContactNo = "92000" + Random.Shared.Next(10000, 99999),
				Email = "profile.preview." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				AdmissionIntent = "Second profile duplicate inquiry",
				DesiredClassName = desiredClassName
			},
			JsonOptions);

		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var duplicates = await previewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmLeadDuplicateDto[]>(JsonOptions);
		duplicates.ShouldNotBeNull();
		duplicates.Select(x => x.LeadId).ShouldContain(created.Id);
		var duplicate = duplicates.Single(x => x.LeadId == created.Id);
		duplicate.MatchReason.ShouldBe("Applicant name, parent name, and desired class");
		duplicate.ConfidenceScore.ShouldBe(80);
		duplicate.MatchedFields.ShouldContain("Applicant name");
		duplicate.MatchedFields.ShouldContain("Parent or guardian");
		duplicate.MatchedFields.ShouldContain("Desired class");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Preview_Duplicate_By_Applicant_Dob_And_Class()
	{
		var dateOfBirth = new DateTimeOffset(2018, 5, 12, 0, 0, 0, TimeSpan.Zero);
		var desiredClassName = "DOB Grade " + Guid.NewGuid().ToString("N")[..6];
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "DOB duplicate original inquiry",
				PrimaryContactNo = "95000" + Random.Shared.Next(10000, 99999),
				Email = "dob.original." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = AdmissionInquiryChannel.WalkInReception,
				FirstName = "Kabir",
				DateOfBirth = dateOfBirth,
				DesiredClassName = desiredClassName
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		created.ShouldNotBeNull();
		created.DateOfBirth.ShouldBe(dateOfBirth);

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads/duplicate-preview",
			new AdmissionCrmLeadDuplicatePreviewInput
			{
				FirstName = "Kabir",
				DateOfBirth = dateOfBirth.AddHours(5),
				PrimaryContactNo = "96000" + Random.Shared.Next(10000, 99999),
				Email = "dob.preview." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				AdmissionIntent = "Second DOB duplicate inquiry",
				DesiredClassName = desiredClassName
			},
			JsonOptions);

		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var duplicates = await previewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmLeadDuplicateDto[]>(JsonOptions);
		duplicates.ShouldNotBeNull();
		duplicates.Select(x => x.LeadId).ShouldContain(created.Id);
		var duplicate = duplicates.Single(x => x.LeadId == created.Id);
		duplicate.MatchReason.ShouldBe("Applicant name, date of birth, and desired class");
		duplicate.ConfidenceScore.ShouldBe(85);
		duplicate.MatchedFields.ShouldContain("Applicant name");
		duplicate.MatchedFields.ShouldContain("Date of birth");
		duplicate.MatchedFields.ShouldContain("Desired class");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Record_Duplicate_Continue_Reason()
	{
		var contactNo = "98" + Random.Shared.Next(10000000, 99999999);
		await CreateCrmLeadWithContactAsync(
			"Original duplicate governance inquiry",
			contactNo,
			AdmissionInquiryChannel.WalkInReception);

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Second inquiry for same applicant",
				PrimaryContactNo = contactNo,
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Ira",
				DuplicateContinueReason = "Parent called again with a different class preference."
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		lead.LikelyDuplicateCount.ShouldBeGreaterThan(0);
		lead.DuplicateContinueReason.ShouldBe("Parent called again with a different class preference.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Merge_Duplicate_Crm_Lead_With_Reason()
	{
		var contactNo = "98" + Random.Shared.Next(10000000, 99999999);
		var target = await CreateCrmLeadWithContactAsync(
			"Primary duplicate merge inquiry",
			contactNo,
			AdmissionInquiryChannel.WalkInReception);
		var duplicate = await CreateCrmLeadWithContactAsync(
			"Duplicate merge inquiry",
			contactNo,
			AdmissionInquiryChannel.QuickCallEntry,
			duplicateContinueReason: "Parent confirmed this is the same applicant before merge review.");

		var mergeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{target.Id}/duplicates/merge",
			new MergeAdmissionCrmDuplicateLeadDto
			{
				DuplicateLeadId = duplicate.Id,
				Reason = "Same parent contact confirmed this is the same applicant."
			},
			JsonOptions);

		mergeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await mergeResponse.Content.ReadAsStringAsync());
		var mergedTarget = await mergeResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		mergedTarget.ShouldNotBeNull();
		mergedTarget.Id.ShouldBe(target.Id);
		mergedTarget.LikelyDuplicateCount.ShouldBe(0);

		var duplicateResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{duplicate.Id}");
		duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await duplicateResponse.Content.ReadAsStringAsync());
		var mergedDuplicate = await duplicateResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		mergedDuplicate.ShouldNotBeNull();
		mergedDuplicate.Stage.ShouldBe(AdmissionInquiryStage.Archived);
		mergedDuplicate.MergedIntoLeadId.ShouldBe(target.Id);
		mergedDuplicate.MergeReason.ShouldBe("Same parent contact confirmed this is the same applicant.");
		mergedDuplicate.ArchiveReason.ShouldBe("Same parent contact confirmed this is the same applicant.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Sensitive_Crm_Operations_Without_Required_Permissions()
	{
		using var client = CreateClientWithCrmPermissions();
		var academicYearId = Factory.SeededAcademicYearId;
		var leadId = Guid.NewGuid();
		var examRecordId = Guid.NewGuid();

		await AssertForbiddenAsync(client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/leads"));
		await AssertForbiddenAsync(client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/quick-inquiry-options"));
		await AssertForbiddenAsync(client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/pipeline-stages"));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Permission gate inquiry",
				PrimaryContactNo = "9000000001"
			},
			JsonOptions));
		await AssertForbiddenAsync(client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle"));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/duplicates/merge",
			new MergeAdmissionCrmDuplicateLeadDto
			{
				DuplicateLeadId = Guid.NewGuid(),
				Reason = "Permission gate merge attempt."
			},
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = Guid.Parse(TestAuthHandler.TestUserId) },
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/priority",
			new ChangeAdmissionCrmLeadPriorityDto
			{
				Priority = AdmissionCrmLeadPriority.Hot,
				Reason = "Permission gate priority attempt."
			},
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/stage",
			new ChangeAdmissionCrmLeadStageDto
			{
				Stage = AdmissionInquiryStage.FollowUp,
				Reason = "Permission gate stage attempt."
			},
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/registration-form",
			new IssueAdmissionCrmRegistrationFormDto
			{
				FormNumber = "CRM-PERMISSION-001",
				Amount = 250
			},
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/archive",
			new ArchiveAdmissionCrmLeadDto { Reason = "Permission gate archive attempt." },
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/tasks",
			new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = "Permission gate task",
				DueAt = DateTime.UtcNow.AddDays(1)
			},
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/automation/stale-archive/run",
			content: null));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = Guid.NewGuid() },
			JsonOptions));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{examRecordId}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.Present
			},
			JsonOptions));
		await AssertForbiddenAsync(client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/analytics/summary"));
		await AssertForbiddenAsync(client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/communication/message-templates/preview",
			new PreviewAdmissionCrmMessageTemplateDto { TemplateId = Guid.NewGuid() },
			JsonOptions));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Bulk_Operations_Permission_To_Override_Protected_Stage_Guards()
	{
		var lead = await CreateCrmLeadAsync("Protected stage override permission inquiry");
		var readyResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
			new ChangeAdmissionCrmLeadStageDto
			{
				Stage = AdmissionInquiryStage.ExamReady,
				Reason = "Applicant is ready for exam scheduling."
			},
			JsonOptions);
		readyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await readyResponse.Content.ReadAsStringAsync());

		using var changeStageOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage);
		var forbiddenResponse = await changeStageOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
			new ChangeAdmissionCrmLeadStageDto
			{
				Stage = AdmissionInquiryStage.ExamScheduled,
				Reason = "Attempted protected workflow override.",
				OverrideWorkflowGuards = true
			},
			JsonOptions);
		forbiddenResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await forbiddenResponse.Content.ReadAsStringAsync());

		using var authorizedOverrideClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations);
		var allowedResponse = await authorizedOverrideClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
			new ChangeAdmissionCrmLeadStageDto
			{
				Stage = AdmissionInquiryStage.ExamScheduled,
				Reason = "Admissions supervisor approved the protected workflow override.",
				OverrideWorkflowGuards = true
			},
			JsonOptions);
		allowedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await allowedResponse.Content.ReadAsStringAsync());
		var changed = await allowedResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		changed.ShouldNotBeNull();
		changed.Stage.ShouldBe(AdmissionInquiryStage.ExamScheduled);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		timeline.Items.ShouldContain(x =>
			x.EventType == AdmissionCrmTimelineEventType.StageChanged &&
			x.Details != null &&
			x.Details.Contains("Workflow guards overridden."));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Dedicated_Reopen_Permission_And_Audit_For_Stale_Lead()
	{
		var lead = await CreateCrmLeadAsync("Stale lead dedicated reopen permission inquiry");
		await MarkCrmLeadStaleAsync(lead.Id);

		using var overrideClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations);
		var bypassResponse = await overrideClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
			new ChangeAdmissionCrmLeadStageDto
			{
				Stage = AdmissionInquiryStage.FollowUp,
				Reason = "Attempted to bypass the dedicated reopen workflow.",
				OverrideWorkflowGuards = true
			},
			JsonOptions);
		bypassResponse.StatusCode.ShouldBe(
			HttpStatusCode.BadRequest,
			await bypassResponse.Content.ReadAsStringAsync());
		var bypassBody = await bypassResponse.Content.ReadAsStringAsync();
		bypassBody.ShouldContain("AdmissionCrm:StaleLeadRequiresReopen");

		using var changeStageOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage);
		var forbiddenReopenResponse = await changeStageOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/reopen",
			new ReopenAdmissionCrmLeadDto { Reason = "Attempted without reopen permission." },
			JsonOptions);
		forbiddenReopenResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await forbiddenReopenResponse.Content.ReadAsStringAsync());

		using var reopenClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen);
		var reopenResponse = await reopenClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/reopen",
			new ReopenAdmissionCrmLeadDto { Reason = "Parent resumed the admission inquiry." },
			JsonOptions);
		reopenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reopenResponse.Content.ReadAsStringAsync());
		var reopened = await reopenResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		reopened.ShouldNotBeNull();
		reopened.Stage.ShouldBe(AdmissionInquiryStage.FollowUp);
		reopened.ReopenedAt.ShouldNotBeNull();
		reopened.ReopenReason.ShouldBe("Parent resumed the admission inquiry.");

		var timeline = await GetCrmTimelineAsync(lead.Id);
		timeline.Items.ShouldContain(x =>
			x.EventType == AdmissionCrmTimelineEventType.Reopened &&
			x.Details == "Parent resumed the admission inquiry.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Not_Expose_Crm_Lead_To_Another_Tenant()
	{
		var lead = await CreateCrmLeadAsync("Tenant isolation inquiry");
		using var otherTenantClient = Factory.CreateClient();
		otherTenantClient.DefaultRequestHeaders.Add(
			TestAuthHandler.TestTenantIdHeader,
			Guid.NewGuid().ToString());

		var response = await otherTenantClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_List_Crm_Leads_By_Academic_Year()
	{
		var input = new CreateAdmissionCrmLeadDto
		{
			AdmissionIntent = "Admission inquiry for LKG",
			FirstName = "List",
			Email = "parent.crm@example.com",
			Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
			ParentGuardianName = "Meera Shah"
		};

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			input,
			JsonOptions);
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		created.ShouldNotBeNull();

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads?maxResultCount=25&filterText=meera");

		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var list = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmLeadDto>>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.Select(x => x.Id).ShouldContain(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Sort_Crm_Leads_By_Applicant_Name()
	{
		var campaign = "Sorting " + Guid.NewGuid().ToString("N")[..8];
		foreach (var firstName in new[] { "Zara", "Aarav" })
		{
			var response = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Sorting verification",
					PrimaryContactNo = "979797" + Random.Shared.Next(1000, 9999),
					Email = $"{firstName.ToLowerInvariant()}.{Guid.NewGuid():N}@example.com",
					Channel = AdmissionInquiryChannel.WalkInReception,
					FirstName = firstName,
					ParentGuardianName = "Sorting Parent",
					CampaignName = campaign,
					DesiredClassName = "Grade 4"
				},
				JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads?campaignName={Uri.EscapeDataString(campaign)}&sorting=ApplicantName%20ASC&maxResultCount=25");

		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var list = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmLeadDto>>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.Select(x => x.FirstName).ShouldBe(new[] { "Aarav", "Zara" });
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Default_Cycle_Configuration()
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/cycle");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var configuration = await response.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		configuration.ShouldNotBeNull();
		configuration.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		configuration.IsActive.ShouldBeTrue();
		configuration.RegistrationFormRequiredBeforeExam.ShouldBeFalse();
		configuration.StaleAfterDays.ShouldBe(30);
		configuration.ArchiveAfterDays.ShouldBe(60);
		configuration.DefaultLeadSlaHours.ShouldBe(24);
		configuration.DefaultExamPassingPercentage.ShouldBe(40m);
		configuration.MeritRankingMode.ShouldBe(AdmissionCrmMeritRankingMode.MarksThenInquiryDate);
		configuration.ExamReadinessFields.ShouldBe(
			AdmissionCrmExamReadinessField.Contact |
			AdmissionCrmExamReadinessField.AdmissionIntent |
			AdmissionCrmExamReadinessField.DesiredClass);
		configuration.LeadScoringRules.BaseScore.ShouldBe(20);
		configuration.LeadScoringRules.WalkInReceptionChannelScore.ShouldBe(15);
		configuration.LeadScoringRules.MaxScore.ShouldBe(100);
		configuration.LeadRoutingSettings.IsEnabled.ShouldBeFalse();
		configuration.LeadRoutingSettings.Rules.ShouldBeEmpty();
		configuration.JourneyRules.ShouldBeEmpty();
		configuration.ApplicantChecklist.ShouldBeEmpty();
		configuration.ChannelSettings.Count.ShouldBe(Enum.GetValues<AdmissionInquiryChannel>().Length);
		configuration.ChannelSettings.ShouldAllBe(x => x.IsEnabled);
		configuration.ChannelSettings.Select(x => x.Channel).Distinct().Count()
			.ShouldBe(configuration.ChannelSettings.Count);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Cycle_Configuration_Update_After_Journey_Is_Locked()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var cycleResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle");
		cycleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cycleResponse.Content.ReadAsStringAsync());
		var cycle = await cycleResponse.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		cycle.ShouldNotBeNull();

		var lockResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/journey/lock",
			new AdmissionCrmJourneyLockDto { Reason = "Regression test for immutable admission journeys." },
			JsonOptions);
		lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
		var lockedCycle = await lockResponse.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		lockedCycle.ShouldNotBeNull();
		lockedCycle.JourneyLocked.ShouldBeTrue();

		var cycleUpdate = new UpdateAdmissionCrmCycleConfigurationDto
		{
			CycleName = cycle.CycleName,
			IsActive = cycle.IsActive,
			RegistrationFormRequiredBeforeExam = cycle.RegistrationFormRequiredBeforeExam,
			EntranceExamRequired = cycle.EntranceExamRequired,
			StaleAfterDays = cycle.StaleAfterDays,
			ArchiveAfterDays = cycle.ArchiveAfterDays,
			DefaultLeadSlaHours = cycle.DefaultLeadSlaHours,
			SlaPolicy = cycle.SlaPolicy,
			ActivationCancellationWindowDays = cycle.ActivationCancellationWindowDays,
			DefaultExamPassingPercentage = cycle.DefaultExamPassingPercentage,
			MeritRankingMode = cycle.MeritRankingMode,
			ExamReadinessFields = cycle.ExamReadinessFields,
			LeadScoringRules = cycle.LeadScoringRules,
			LeadRoutingSettings = cycle.LeadRoutingSettings,
			JourneyRules = cycle.JourneyRules,
			ApplicantChecklist = cycle.ApplicantChecklist,
			ChannelSettings = cycle.ChannelSettings,
			Notes = "This update must not bypass the journey lock."
		};
		var updateResponse = await PutCycleConfigurationWithImpactAsync(academicYearId, cycleUpdate);

		updateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await updateResponse.Content.ReadAsStringAsync());
		(await updateResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:JourneyLocked");

		var persistedResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle");
		persistedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await persistedResponse.Content.ReadAsStringAsync());
		var persisted = await persistedResponse.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		persisted.ShouldNotBeNull();
		persisted.JourneyLocked.ShouldBeTrue();
		persisted.Notes.ShouldBe(cycle.Notes);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Copy_School_Default_Cycle_Configuration_To_New_Academic_Year()
	{
		var defaultName = $"School CRM Default {Guid.NewGuid():N}";
		var schoolDefault = await UpdateSchoolDefaultCycleAsync(new UpdateAdmissionCrmCycleConfigurationDto
		{
			CycleName = defaultName,
			IsActive = true,
			RegistrationFormRequiredBeforeExam = true,
			StaleAfterDays = 14,
			ArchiveAfterDays = 45,
			DefaultLeadSlaHours = 8,
			DefaultExamPassingPercentage = 55m,
			MeritRankingMode = AdmissionCrmMeritRankingMode.LeadScoreThenMarks,
			ExamReadinessFields =
				AdmissionCrmExamReadinessField.Contact |
				AdmissionCrmExamReadinessField.AdmissionIntent |
				AdmissionCrmExamReadinessField.DesiredClass |
				AdmissionCrmExamReadinessField.StudentName,
			LeadScoringRules = new AdmissionCrmLeadScoringRulesDto
			{
				BaseScore = 35,
				WalkInReceptionChannelScore = 22,
				MaxScore = 90
			},
			LeadRoutingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault(),
			JourneyRules = new List<AdmissionCrmJourneyRuleDto>(),
			ApplicantChecklist =
			[
				new AdmissionCrmApplicantChecklistItemDto
				{
					Key = "birth-certificate",
					Label = "Birth certificate",
					Details = "Bring one photocopy for verification.",
					IsRequired = true,
					DisplayOrder = 10
				},
				new AdmissionCrmApplicantChecklistItemDto
				{
					Key = "previous-report-card",
					Label = "Previous report card",
					Details = "Optional for nursery and LKG inquiries.",
					IsRequired = false,
					DisplayOrder = 20
				}
			],
			ChannelSettings =
			[
				new AdmissionCrmChannelSettingDto
				{
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					IsEnabled = true,
					DefaultOwnerUserId = Guid.Parse(TestAuthHandler.TestUserId),
					DisplayOrder = 5
				}
			],
			Notes = "School-level defaults copied into new admission cycles."
		});

		try
		{
			schoolDefault.AcademicYearId.ShouldBe(Guid.Empty);
			schoolDefault.CycleName.ShouldBe(defaultName);

			var academicYearId = await CreateAcademicYearAsync();
			var cycleResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle");

			cycleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cycleResponse.Content.ReadAsStringAsync());
			var cycle = await cycleResponse.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
			cycle.ShouldNotBeNull();
			cycle.Id.ShouldNotBe(schoolDefault.Id);
			cycle.AcademicYearId.ShouldBe(academicYearId);
			cycle.CycleName.ShouldBe(defaultName);
			cycle.RegistrationFormRequiredBeforeExam.ShouldBeTrue();
			cycle.StaleAfterDays.ShouldBe(14);
			cycle.ArchiveAfterDays.ShouldBe(45);
			cycle.DefaultLeadSlaHours.ShouldBe(8);
			cycle.DefaultExamPassingPercentage.ShouldBe(55m);
			cycle.MeritRankingMode.ShouldBe(AdmissionCrmMeritRankingMode.LeadScoreThenMarks);
			cycle.ExamReadinessFields.ShouldBe(
				AdmissionCrmExamReadinessField.Contact |
				AdmissionCrmExamReadinessField.AdmissionIntent |
				AdmissionCrmExamReadinessField.DesiredClass |
				AdmissionCrmExamReadinessField.StudentName);
			cycle.LeadScoringRules.BaseScore.ShouldBe(35);
			cycle.LeadScoringRules.WalkInReceptionChannelScore.ShouldBe(22);
			cycle.LeadScoringRules.MaxScore.ShouldBe(90);
			cycle.ApplicantChecklist.Count.ShouldBe(2);
			cycle.ApplicantChecklist.Single(x => x.Key == "birth-certificate").IsRequired.ShouldBeTrue();
			cycle.ApplicantChecklist.Single(x => x.Key == "previous-report-card").IsRequired.ShouldBeFalse();
			var websiteChannel = cycle.ChannelSettings.Single(x =>
				x.Channel == AdmissionInquiryChannel.SchoolWebsiteForm);
			websiteChannel.IsEnabled.ShouldBeTrue();
			websiteChannel.DefaultOwnerUserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
			websiteChannel.DisplayOrder.ShouldBe(5);
			cycle.Notes.ShouldBe("School-level defaults copied into new admission cycles.");
		}
		finally
		{
			await ResetSchoolDefaultCycleAsync();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Apply_Cycle_Channel_Defaults_And_Reject_Disabled_Channel()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var source = await CreateInquirySourceAsync($"CRM Website Source {Guid.NewGuid():N}");
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);

		var channelSettings = originalCycle.ChannelSettings
			.Select(setting => new AdmissionCrmChannelSettingDto
			{
				Channel = setting.Channel,
				IsEnabled = setting.Channel != AdmissionInquiryChannel.WhatsAppInquiry,
				DefaultInquirySourceId = setting.Channel == AdmissionInquiryChannel.SchoolWebsiteForm
					? source.Id
					: setting.DefaultInquirySourceId,
				DefaultFormTemplateId = setting.DefaultFormTemplateId,
				DefaultOwnerUserId = setting.Channel == AdmissionInquiryChannel.SchoolWebsiteForm
					? ownerUserId
					: setting.DefaultOwnerUserId,
				DefaultAcknowledgementTemplateId = setting.DefaultAcknowledgementTemplateId,
				DisplayOrder = setting.DisplayOrder
			})
			.ToList();

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			channelSettings: channelSettings);

		try
		{
			var response = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Website channel default inquiry",
					PrimaryContactNo = "95555" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					FirstName = "Channel"
				},
				JsonOptions);

			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			lead.ShouldNotBeNull();
			lead.InquirySourceId.ShouldBe(source.Id);
			lead.InquirySourceSnapshot.ShouldBe(source.DisplayName);
			lead.OwnerUserId.ShouldBe(ownerUserId);

			var blockedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Disabled WhatsApp channel inquiry",
					FirstName = "Disabled",
					PrimaryContactNo = "96666" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.WhatsAppInquiry
				},
				JsonOptions);

			blockedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await blockedResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:ChannelDisabled");
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				channelSettings: originalCycle.ChannelSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Send_Channel_Default_Acknowledgement_For_Staff_And_Public_Inquiries()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var acknowledgementTemplate = await CreateApprovedCrmMessageTemplateAsync(
			body:
				"Thanks {{parentName}}. We received {{studentName}} inquiry. Track here: {{statusLink}}",
			variables: ["parentName", "studentName", "statusLink"]);
		var channelSettings = originalCycle.ChannelSettings
			.Select(setting => new AdmissionCrmChannelSettingDto
			{
				Channel = setting.Channel,
				IsEnabled = true,
				DefaultInquirySourceId = setting.DefaultInquirySourceId,
				DefaultFormTemplateId = setting.DefaultFormTemplateId,
				DefaultOwnerUserId = setting.DefaultOwnerUserId,
				DefaultAcknowledgementTemplateId = setting.Channel == AdmissionInquiryChannel.SchoolWebsiteForm
					? acknowledgementTemplate.Id
					: setting.DefaultAcknowledgementTemplateId,
				DisplayOrder = setting.DisplayOrder
			})
			.ToList();

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			channelSettings: channelSettings);

		try
		{
			var staffLeadResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Staff acknowledgement inquiry",
					PrimaryContactNo = "97777" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					FirstName = "Acknowledge",
					ParentGuardianName = "Staff Parent"
				},
				JsonOptions);

			staffLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await staffLeadResponse.Content.ReadAsStringAsync());
			var staffLead = await staffLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			staffLead.ShouldNotBeNull();
			var staffLogs = await GetCrmDeliveryLogsAsync(staffLead.Id, acknowledgementTemplate.Id);
			staffLogs.TotalCount.ShouldBe(1);
			staffLogs.Items[0].Status.ShouldBe(AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus);
			staffLogs.Items[0].BodySnapshot.ShouldContain("Staff Parent");

			var publicTemplate = await CreatePublicInquiryTemplateAsync(
				"Public acknowledgement " + Guid.NewGuid().ToString("N")[..8]);
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using var anonymousClient = CreateAnonymousClient();
			var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{publicTemplate.Id}",
				JsonOptions);
			form.ShouldNotBeNull();
			var publicSubmitResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{publicTemplate.Id}/submit",
				new PublicAdmissionInquirySubmitDto
				{
					ConsentVersion = form.ConsentVersion,
					ConsentAccepted = true,
					CommunicationPreferences = ["WhatsApp"],
					FieldValues = new Dictionary<string, JsonElement>
					{
						["studentFirstName"] = JsonSerializer.SerializeToElement("Public", JsonOptions),
						["parentPhone"] = JsonSerializer.SerializeToElement(
							"97888" + Random.Shared.Next(10000, 99999),
							JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement(
							"Public acknowledgement inquiry",
							JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement("Grade 1", JsonOptions)
					}
				},
				JsonOptions);

			publicSubmitResponse.StatusCode.ShouldBe(
				HttpStatusCode.OK,
				await publicSubmitResponse.Content.ReadAsStringAsync());
			var publicResult = await publicSubmitResponse.Content
				.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
			publicResult.ShouldNotBeNull();
			var publicLogs = await GetCrmDeliveryLogsAsync(publicResult.LeadId, acknowledgementTemplate.Id);
			publicLogs.TotalCount.ShouldBe(1);
			publicLogs.Items[0].Status.ShouldBe(AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus);
			publicLogs.Items[0].BodySnapshot.ShouldContain(publicResult.StatusPath);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				channelSettings: originalCycle.ChannelSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Set_Manual_Lead_Priority_Without_Changing_Score()
	{
		var lead = await CreateCrmLeadAsync("Priority inquiry for Grade 4");
		lead.Priority.ShouldBe(AdmissionCrmLeadPriority.Warm);
		var originalLeadScore = lead.LeadScore;

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/priority",
			new ChangeAdmissionCrmLeadPriorityDto
			{
				Priority = AdmissionCrmLeadPriority.Hot,
				Reason = "Parent has high intent and visited campus."
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var changed = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		changed.ShouldNotBeNull();
		changed.Priority.ShouldBe(AdmissionCrmLeadPriority.Hot);
		changed.LeadScore.ShouldBe(originalLeadScore);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads?priority={(int) AdmissionCrmLeadPriority.Hot}&maxResultCount=200");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var filtered = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmLeadDto>>(JsonOptions);
		filtered.ShouldNotBeNull();
		filtered.Items.Select(x => x.Id).ShouldContain(lead.Id);
		filtered.Items.ShouldAllBe(x => x.Priority == AdmissionCrmLeadPriority.Hot);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.PriorityChanged &&
			x.Details != null &&
			x.Details.Contains("Warm -> Hot") &&
			x.Details.Contains("Parent has high intent and visited campus.")).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Use_Cycle_Configured_Lead_Scoring_Rules()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var scoringRules = AdmissionCrmLeadScoringRulesDto.CreateDefault();
		scoringRules.BaseScore = 5;
		scoringRules.BothContactChannelsScore = 0;
		scoringRules.ParentGuardianScore = 0;
		scoringRules.DesiredClassScore = 20;
		scoringRules.WalkInReceptionChannelScore = 30;

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			scoringRules);

		try
		{
			var lead = await CreateCrmLeadForClassAsync(
				"Configured scoring inquiry",
				"Scoring Class " + Guid.NewGuid().ToString("N")[..8],
				"Scoring Parent");

			lead.LeadScore.ShouldBe(55);

			var scoreResponse = await Client.PostAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/score/recalculate",
				content: null);
			scoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scoreResponse.Content.ReadAsStringAsync());
			var scored = await scoreResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			scored.ShouldNotBeNull();
			scored.LeadScore.ShouldBe(55);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Assign_New_Lead_From_Cycle_Routing_Rules()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.Rules.Add(new AdmissionCrmLeadRoutingRuleDto
		{
			RuleName = "Quick call owner",
			IsEnabled = true,
			Priority = 10,
			OwnerUserId = ownerUserId,
			Channel = AdmissionInquiryChannel.QuickCallEntry
		});

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			originalCycle.LeadScoringRules,
			routingSettings);

		try
		{
			var response = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Routing inquiry for Grade 2",
					FirstName = "Routing",
					PrimaryContactNo = "98888" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					DesiredClassName = "Grade 2"
				},
				JsonOptions);

			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			lead.ShouldNotBeNull();
			lead.OwnerUserId.ShouldBe(ownerUserId);

			var timeline = await GetCrmTimelineAsync(lead.Id);
			timeline.Items.Select(x => x.EventType).ShouldContain(AdmissionCrmTimelineEventType.OwnerAssigned);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules,
				originalCycle.LeadRoutingSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Manual_Stage_Transition_When_Not_Configured()
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
			var updateResponse = await PutPipelineStageWithImpactAsync(
				Factory.SeededAcademicYearId,
				newInquiryStage.Id,
				new UpdateAdmissionCrmPipelineStageDto
				{
					StageName = newInquiryStage.StageName,
					DisplayOrder = newInquiryStage.DisplayOrder,
					IsEnabled = true,
					ColorHex = newInquiryStage.ColorHex,
					SlaTargetHours = newInquiryStage.SlaTargetHours,
					OwnerResponsibility = newInquiryStage.OwnerResponsibility,
					AllowedNextStages = [AdmissionInquiryStage.FollowUp]
				});
			updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());

			var lead = await CreateCrmLeadAsync("Restricted transition inquiry for Grade 3");
			var response = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
				new ChangeAdmissionCrmLeadStageDto
				{
					Stage = AdmissionInquiryStage.ExamReady,
					Reason = "Counsellor attempted a restricted transition."
				},
				JsonOptions);

			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
			var body = await response.Content.ReadAsStringAsync();
			body.ShouldContain("AdmissionCrm:StageTransitionNotAllowed");
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
	public async Task Should_Change_Lead_Stage_With_Reason_And_Run_Stage_Journey()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var stageTaskTitle = "Manual stage journey " + Guid.NewGuid().ToString("N")[..8];
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules:
			[
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Manual exam-ready journey",
					IsEnabled = true,
					Priority = 10,
					Trigger = AdmissionCrmJourneyTrigger.StageReached,
					Stage = AdmissionInquiryStage.ExamReady,
					CreateTask = true,
					TaskTitle = stageTaskTitle,
					TaskDueOffsetHours = 2,
					TaskNotes = "Prepare the applicant for entrance exam scheduling."
				}
			]);

		try
		{
			var lead = await CreateCrmLeadAsync("Manual stage movement inquiry for Grade 3");

			var response = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
				new ChangeAdmissionCrmLeadStageDto
				{
					Stage = AdmissionInquiryStage.ExamReady,
					Reason = "Counsellor confirmed exam readiness."
				},
				JsonOptions);

			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var changed = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			changed.ShouldNotBeNull();
			changed.Stage.ShouldBe(AdmissionInquiryStage.ExamReady);

			var timeline = await GetCrmTimelineAsync(lead.Id);
			timeline.Items.Any(x =>
				x.EventType == AdmissionCrmTimelineEventType.StageChanged &&
				x.Details != null &&
				x.Details.Contains("Counsellor confirmed exam readiness.")).ShouldBeTrue();

			var tasks = await GetCrmTasksAsync(lead.Id);
			tasks.Items.Select(x => x.Title).ShouldContain(stageTaskTitle);
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
	public async Task Should_Add_Call_Note_To_Lead_Timeline()
	{
		var lead = await CreateCrmLeadAsync("Call note inquiry for Grade 3");

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/notes",
			new CreateAdmissionCrmLeadNoteDto
			{
				IsCallNote = true,
				Note = "Parent asked for transport and exam slot details."
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var note = await response.Content.ReadFromJsonAsync<AdmissionCrmTimelineEventDto>(JsonOptions);
		note.ShouldNotBeNull();
		note.EventType.ShouldBe(AdmissionCrmTimelineEventType.NoteAdded);
		note.Title.ShouldBe("Call note added");
		note.Details.ShouldNotBeNull();
		note.Details.ShouldContain("transport");

		var timeline = await GetCrmTimelineAsync(lead.Id);
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.NoteAdded &&
			x.Title == "Call note added" &&
			x.Details != null &&
			x.Details.Contains("transport")).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Round_Robin_New_Leads_Across_Cycle_Owner_Pool()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var owners = await GetEligibleCrmOwnersAsync();
		var firstOwnerUserId = owners[0].Id;
		var secondOwnerUserId = owners[1].Id;
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.RoutingStrategy = AdmissionCrmLeadRoutingStrategy.RoundRobin;
		routingSettings.OwnerPoolUserIds.AddRange([firstOwnerUserId, secondOwnerUserId]);

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			originalCycle.LeadScoringRules,
			routingSettings);

		try
		{
			var workload = (await GetCrmAnalyticsSummaryAsync()).StaffWorkload;
			var routedLeadCount = GetOwnerCounts(firstOwnerUserId, workload).Total +
				GetOwnerCounts(secondOwnerUserId, workload).Total;
			var ownerPool = new[] { firstOwnerUserId, secondOwnerUserId };
			var firstLead = await CreateCrmLeadAsync("Round robin routing inquiry one");
			var secondLead = await CreateCrmLeadAsync("Round robin routing inquiry two");
			var thirdLead = await CreateCrmLeadAsync("Round robin routing inquiry three");

			firstLead.OwnerUserId.ShouldBe(ownerPool[routedLeadCount % ownerPool.Length]);
			secondLead.OwnerUserId.ShouldBe(ownerPool[(routedLeadCount + 1) % ownerPool.Length]);
			thirdLead.OwnerUserId.ShouldBe(ownerPool[(routedLeadCount + 2) % ownerPool.Length]);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules,
				originalCycle.LeadRoutingSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Route_New_Lead_To_Least_Open_Lead_Owner()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var owners = await GetEligibleCrmOwnersAsync();
		var firstOwnerUserId = owners[0].Id;
		var secondOwnerUserId = owners[1].Id;
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.RoutingStrategy = AdmissionCrmLeadRoutingStrategy.LeastOpenLeads;
		routingSettings.OwnerPoolUserIds.AddRange([firstOwnerUserId, secondOwnerUserId]);

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			originalCycle.LeadScoringRules,
			routingSettings);

		try
		{
			var workload = (await GetCrmAnalyticsSummaryAsync()).StaffWorkload;
			var ownerCounts = new Dictionary<Guid, (int Open, int Total)>
			{
				[firstOwnerUserId] = GetOwnerCounts(firstOwnerUserId, workload),
				[secondOwnerUserId] = GetOwnerCounts(secondOwnerUserId, workload)
			};
			var expectedFirstOwner = LeastLoadedOwner(ownerCounts);
			var firstLead = await CreateCrmLeadAsync("Least workload routing inquiry one");
			ownerCounts[expectedFirstOwner] = (
				ownerCounts[expectedFirstOwner].Open + 1,
				ownerCounts[expectedFirstOwner].Total + 1);
			var expectedSecondOwner = LeastLoadedOwner(ownerCounts);
			var secondLead = await CreateCrmLeadAsync("Least workload routing inquiry two");

			firstLead.OwnerUserId.ShouldBe(expectedFirstOwner);
			secondLead.OwnerUserId.ShouldBe(expectedSecondOwner);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules,
				originalCycle.LeadRoutingSettings);
		}
	}

	private static (int Open, int Total) GetOwnerCounts(
		Guid ownerUserId,
		IReadOnlyList<AdmissionCrmStaffWorkloadDto> workload)
	{
		var ownerWorkload = workload.FirstOrDefault(item => item.OwnerUserId == ownerUserId);
		return ownerWorkload == null
			? (0, 0)
			: (ownerWorkload.OpenLeadCount, ownerWorkload.LeadCount);
	}

	private static Guid LeastLoadedOwner(IReadOnlyDictionary<Guid, (int Open, int Total)> ownerCounts)
	{
		return ownerCounts
			.OrderBy(item => item.Value.Open)
			.ThenBy(item => item.Value.Total)
			.ThenBy(item => item.Key)
			.First()
			.Key;
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Route_New_Lead_By_Branch_And_Preferred_Language_Rules()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var owners = await GetEligibleCrmOwnersAsync();
		var branchLanguageOwnerUserId = owners[0].Id;
		var fallbackOwnerUserId = owners[1].Id;
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.DefaultOwnerUserId = fallbackOwnerUserId;
		routingSettings.Rules.Add(new AdmissionCrmLeadRoutingRuleDto
		{
			RuleName = "North campus Hindi owner",
			IsEnabled = true,
			Priority = 10,
			OwnerUserId = branchLanguageOwnerUserId,
			CampusBranchContains = "North",
			PreferredLanguageContains = "Hindi"
		});

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			originalCycle.LeadScoringRules,
			routingSettings);

		try
		{
			var matchingResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Routing inquiry for North campus",
					FirstName = "North",
					PrimaryContactNo = "98753" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					CampusBranchName = "North Campus",
					PreferredCommunicationLanguage = "Hindi",
					DesiredClassName = "Grade 1"
				},
				JsonOptions);

			matchingResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await matchingResponse.Content.ReadAsStringAsync());
			var matchingLead = await matchingResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			matchingLead.ShouldNotBeNull();
			matchingLead.CampusBranchName.ShouldBe("North Campus");
			matchingLead.PreferredCommunicationLanguage.ShouldBe("Hindi");
			matchingLead.OwnerUserId.ShouldBe(branchLanguageOwnerUserId);

			var unmatchedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Routing inquiry for South campus",
					FirstName = "South",
					PrimaryContactNo = "98754" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					CampusBranchName = "South Campus",
					PreferredCommunicationLanguage = "English",
					DesiredClassName = "Grade 1"
				},
				JsonOptions);

			unmatchedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await unmatchedResponse.Content.ReadAsStringAsync());
			var unmatchedLead = await unmatchedResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			unmatchedLead.ShouldNotBeNull();
			unmatchedLead.OwnerUserId.ShouldBe(fallbackOwnerUserId);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules,
				originalCycle.LeadRoutingSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Route_New_Lead_By_Target_Standard_Rule()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var owners = await GetEligibleCrmOwnersAsync();
		var standardOwnerUserId = owners[0].Id;
		var fallbackOwnerUserId = owners[1].Id;
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.DefaultOwnerUserId = fallbackOwnerUserId;
		routingSettings.Rules.Add(new AdmissionCrmLeadRoutingRuleDto
		{
			RuleName = "Seeded standard owner",
			IsEnabled = true,
			Priority = 10,
			OwnerUserId = standardOwnerUserId,
			TargetStandardId = Factory.SeededStandardId
		});

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			originalCycle.LeadScoringRules,
			routingSettings);

		try
		{
			var matchingResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Routing inquiry for seeded standard",
					FirstName = "Seeded",
					PrimaryContactNo = "98751" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.ManualBackOffice,
					TargetStandardId = Factory.SeededStandardId,
					DesiredClassName = "Seeded standard"
				},
				JsonOptions);

			matchingResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await matchingResponse.Content.ReadAsStringAsync());
			var matchingLead = await matchingResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			matchingLead.ShouldNotBeNull();
			matchingLead.OwnerUserId.ShouldBe(standardOwnerUserId);

			var unmatchedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Routing inquiry for another standard",
					FirstName = "Another",
					PrimaryContactNo = "98752" + Random.Shared.Next(10000, 99999),
					Channel = AdmissionInquiryChannel.ManualBackOffice,
					DesiredClassName = "Another standard"
				},
				JsonOptions);

			unmatchedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await unmatchedResponse.Content.ReadAsStringAsync());
			var unmatchedLead = await unmatchedResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			unmatchedLead.ShouldNotBeNull();
			unmatchedLead.OwnerUserId.ShouldBe(fallbackOwnerUserId);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				originalCycle.LeadScoringRules,
				originalCycle.LeadRoutingSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Default_Pipeline_Stages_In_Order()
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/pipeline-stages");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var stages = await response.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmPipelineStageDto>>(JsonOptions);
		stages.ShouldNotBeNull();
		stages.TotalCount.ShouldBe(10);
		stages.Items.Select(x => x.SystemStage).ShouldContain(AdmissionInquiryStage.NewInquiry);
		stages.Items.Select(x => x.SystemStage).ShouldContain(AdmissionInquiryStage.ExamReady);
		stages.Items.Select(x => x.SystemStage).ShouldContain(AdmissionInquiryStage.Archived);
		stages.Items.ShouldAllBe(x => x.IsSystem);
		stages.Items.ShouldAllBe(x => x.IsEnabled);
		stages.Items.Select(x => x.DisplayOrder).ShouldBe(stages.Items.Select(x => x.DisplayOrder).OrderBy(x => x));

		var newInquiryStage = stages.Items.Single(x => x.SystemStage == AdmissionInquiryStage.NewInquiry);
		newInquiryStage.ColorHex.ShouldBe("#1976D2");
		newInquiryStage.SlaTargetHours.ShouldBe(24);
		newInquiryStage.OwnerResponsibility.ShouldBe("Admissions counselor");
		newInquiryStage.AllowedNextStages.ShouldContain(AdmissionInquiryStage.FollowUp);
		newInquiryStage.AllowedNextStages.ShouldContain(AdmissionInquiryStage.ExamReady);

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
			var updateResponse = await PutPipelineStageWithImpactAsync(
				Factory.SeededAcademicYearId,
				newInquiryStage.Id,
				new UpdateAdmissionCrmPipelineStageDto
				{
					StageName = "New Lead",
					DisplayOrder = newInquiryStage.DisplayOrder,
					IsEnabled = true,
					ColorHex = "#00AA66",
					SlaTargetHours = 8,
					OwnerResponsibility = "Front desk admission owner",
					AllowedNextStages =
					[
						AdmissionInquiryStage.FollowUp,
						AdmissionInquiryStage.ExamReady
					]
				});

			updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
			var updatedStage = await updateResponse.Content
				.ReadFromJsonAsync<AdmissionCrmPipelineStageDto>(JsonOptions);
			updatedStage.ShouldNotBeNull();
			updatedStage.StageName.ShouldBe("New Lead");
			updatedStage.ColorHex.ShouldBe("#00AA66");
			updatedStage.SlaTargetHours.ShouldBe(8);
			updatedStage.OwnerResponsibility.ShouldBe("Front desk admission owner");
			updatedStage.AllowedNextStages.ShouldBe(
				[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.ExamReady],
				ignoreOrder: false);
		}
		finally
		{
			await PutPipelineStageWithImpactAsync(
				Factory.SeededAcademicYearId,
				newInquiryStage.Id,
				originalStage);
		}
	}

}
