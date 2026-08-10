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
public class AdmissionCrmPublicApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Public_Form_Template_Without_Consent()
	{
		var input = new CreateAdmissionCrmFormTemplateDto
		{
			TemplateName = "Campaign inquiry without consent",
			Channel = AdmissionInquiryChannel.CampaignLink,
			IsPublic = true,
			Fields =
			[
				new AdmissionCrmFormFieldDto
				{
					Key = "parentPhone",
					Label = "Parent phone",
					FieldType = AdmissionCrmFormFieldType.Phone,
					SystemField = "PrimaryContactNo",
					Required = true,
					DisplayOrder = 10
				}
			]
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-templates",
			input,
			JsonOptions);

		response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain("AdmissionCrm:PublicFormConsentRequired");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Submit_Public_Form_With_Consent_And_Create_Crm_Lead()
	{
		var template = await CreatePublicInquiryTemplateAsync("Public inquiry " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();

		var formResponse = await anonymousClient.GetAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}");
		formResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await formResponse.Content.ReadAsStringAsync());
		var form = await formResponse.Content.ReadFromJsonAsync<PublicAdmissionInquiryFormDto>(JsonOptions);
		form.ShouldNotBeNull();
		form.TemplateId.ShouldBe(template.Id);
		form.PrivacyNoticeUrl.ShouldBe("https://school.example/privacy");
		form.ConsentVersion.ShouldNotBeNullOrWhiteSpace();

		var submissionKey = Guid.NewGuid().ToString("N");
		var submitRequest = new PublicAdmissionInquirySubmitDto
		{
			SubmissionKey = submissionKey,
			ConsentVersion = form.ConsentVersion,
			ConsentAccepted = true,
			CommunicationPreferences = ["WhatsApp", "Email"],
			FieldValues = new Dictionary<string, JsonElement>
			{
				["studentFirstName"] = JsonSerializer.SerializeToElement("Riya", JsonOptions),
				["parentPhone"] = JsonSerializer.SerializeToElement("9988776655", JsonOptions),
				["parentEmail"] = JsonSerializer.SerializeToElement("riya.parent@example.com", JsonOptions),
				["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for Grade 1", JsonOptions),
				["desiredClass"] = JsonSerializer.SerializeToElement("Grade 1", JsonOptions),
				["campusBranch"] = JsonSerializer.SerializeToElement("North Campus", JsonOptions),
				["preferredLanguage"] = JsonSerializer.SerializeToElement("Hindi", JsonOptions),
				["campaignName"] = JsonSerializer.SerializeToElement("Monsoon QR Drive", JsonOptions)
			}
		};
		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			submitRequest,
			JsonOptions);

		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var result = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.LeadId.ShouldNotBe(Guid.Empty);
		result.StatusToken.ShouldNotBeNullOrWhiteSpace();
		result.StatusPath.ShouldContain(result.StatusToken);
		result.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);

		var retryResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			submitRequest,
			JsonOptions);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await retryResponse.Content.ReadAsStringAsync());
		var retryResult = await retryResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
		retryResult.ShouldNotBeNull();
		retryResult.LeadId.ShouldBe(result.LeadId);
		retryResult.StatusToken.ShouldBe(result.StatusToken);

		await AssertPublicSubmissionPersistedAsync(result.LeadId, submissionKey);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Assign_Configured_Owner_From_Public_Staff_Shared_Link()
	{
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.OwnerPoolUserIds.Add(ownerUserId);
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			leadRoutingSettings: routingSettings);

		try
		{
			var template = await CreatePublicInquiryTemplateAsync(
				"Staff shared public inquiry " + Guid.NewGuid().ToString("N")[..8],
				AdmissionInquiryChannel.StaffSharedLink);
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using var anonymousClient = CreateAnonymousClient();

			var formResponse = await anonymousClient.GetAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}");
			formResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await formResponse.Content.ReadAsStringAsync());
			var form = await formResponse.Content.ReadFromJsonAsync<PublicAdmissionInquiryFormDto>(JsonOptions);
			form.ShouldNotBeNull();

			var submitResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
				new PublicAdmissionInquirySubmitDto
				{
					ConsentVersion = form.ConsentVersion,
					ConsentAccepted = true,
					CommunicationPreferences = ["WhatsApp"],
					FieldValues = new Dictionary<string, JsonElement>
					{
						["studentFirstName"] = JsonSerializer.SerializeToElement("Aarav", JsonOptions),
						["parentPhone"] = JsonSerializer.SerializeToElement("9977553311", JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry from staff link", JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement("Grade 2", JsonOptions),
						["ownerUserId"] = JsonSerializer.SerializeToElement(ownerUserId.ToString("D"), JsonOptions)
					}
				},
				JsonOptions);

			submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
			var result = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
			result.ShouldNotBeNull();

			var leadResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/leads/{result.LeadId}");
			leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
			var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			lead.ShouldNotBeNull();
			lead.Channel.ShouldBe(AdmissionInquiryChannel.StaffSharedLink);
			lead.OwnerUserId.ShouldBe(ownerUserId);

			var timelineResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/leads/{result.LeadId}/timeline");
			timelineResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await timelineResponse.Content.ReadAsStringAsync());
			var timeline = await timelineResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmTimelineEventDto>>(JsonOptions);
			timeline.ShouldNotBeNull();
			timeline.Items.ShouldContain(x =>
				x.EventType == AdmissionCrmTimelineEventType.OwnerAssigned &&
				x.Title == "Owner assigned from staff-shared link");

			await AssertPublicSubmissionContainsAsync(result.LeadId, "ownerUserId");
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				leadRoutingSettings: originalCycle.LeadRoutingSettings);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Not_Render_Public_Form_When_Channel_Is_Disabled()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var template = await CreatePublicInquiryTemplateAsync(
			"Disabled public channel " + Guid.NewGuid().ToString("N")[..8]);
		var channelSettings = originalCycle.ChannelSettings
			.Select(setting => new AdmissionCrmChannelSettingDto
			{
				Channel = setting.Channel,
				IsEnabled = setting.Channel != AdmissionInquiryChannel.SchoolWebsiteForm,
				DefaultInquirySourceId = setting.DefaultInquirySourceId,
				DefaultFormTemplateId = setting.DefaultFormTemplateId,
				DefaultOwnerUserId = setting.DefaultOwnerUserId,
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
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using var anonymousClient = CreateAnonymousClient();
			var formResponse = await anonymousClient.GetAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}");

			formResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			(await formResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:PublicFormNotFound");
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
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Record_Public_Form_Duplicate_Internally_Without_Exposing_Details()
	{
		var duplicateContact = "98888" + Random.Shared.Next(10000, 99999);
		var existingLead = await CreateCrmLeadWithContactAsync(
			"Existing public duplicate inquiry",
			duplicateContact,
			AdmissionInquiryChannel.WalkInReception);
		var template = await CreatePublicInquiryTemplateAsync("Public duplicate " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();
		var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
			JsonOptions);
		form.ShouldNotBeNull();

		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			new PublicAdmissionInquirySubmitDto
			{
				ConsentVersion = form.ConsentVersion,
				ConsentAccepted = true,
				CommunicationPreferences = ["WhatsApp"],
				FieldValues = new Dictionary<string, JsonElement>
				{
					["studentFirstName"] = JsonSerializer.SerializeToElement("Duplicate", JsonOptions),
					["parentPhone"] = JsonSerializer.SerializeToElement(duplicateContact, JsonOptions),
					["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for Grade 4", JsonOptions),
					["desiredClass"] = JsonSerializer.SerializeToElement("Grade 4", JsonOptions)
				}
			},
			JsonOptions);

		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var responseBody = await submitResponse.Content.ReadAsStringAsync();
		responseBody.ShouldNotContain("likelyDuplicate", Case.Insensitive);
		responseBody.ShouldNotContain(existingLead.Id.ToString(), Case.Insensitive);
		var result = JsonSerializer.Deserialize<PublicAdmissionInquirySubmitResultDto>(
			responseBody,
			JsonOptions);
		result.ShouldNotBeNull();

		var staffLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{result.LeadId}");
		staffLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await staffLeadResponse.Content.ReadAsStringAsync());
		var staffLead = await staffLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staffLead.ShouldNotBeNull();
		staffLead.LikelyDuplicateCount.ShouldBeGreaterThan(0);
		staffLead.LikelyDuplicates.Select(x => x.LeadId).ShouldContain(existingLead.Id);
		staffLead.DuplicateContinueReason.ShouldBe(
			"Public form submitted with likely duplicate details. Review before follow-up.");

		var timeline = await GetCrmTimelineAsync(result.LeadId);
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.DuplicateContinued &&
			x.Title == "Possible duplicate detected").ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Record_Public_Form_Profile_Duplicate_Internally()
	{
		var parentName = "Public Profile Parent " + Guid.NewGuid().ToString("N")[..8];
		var desiredClassName = "Public Profile Grade " + Guid.NewGuid().ToString("N")[..6];
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Existing public profile duplicate inquiry",
				PrimaryContactNo = "93000" + Random.Shared.Next(10000, 99999),
				Email = "public.profile.original." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = AdmissionInquiryChannel.WalkInReception,
				FirstName = "Naina",
				ParentGuardianName = parentName,
				DesiredClassName = desiredClassName
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var existingLead = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		existingLead.ShouldNotBeNull();
		var template = await CreatePublicInquiryTemplateAsync(
			"Public profile duplicate " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();
		var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
			JsonOptions);
		form.ShouldNotBeNull();

		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			new PublicAdmissionInquirySubmitDto
			{
				ConsentVersion = form.ConsentVersion,
				ConsentAccepted = true,
				CommunicationPreferences = ["WhatsApp"],
				FieldValues = new Dictionary<string, JsonElement>
				{
					["studentFirstName"] = JsonSerializer.SerializeToElement("Naina", JsonOptions),
					["parentName"] = JsonSerializer.SerializeToElement(parentName, JsonOptions),
					["parentPhone"] = JsonSerializer.SerializeToElement(
						"94000" + Random.Shared.Next(10000, 99999),
						JsonOptions),
					["admissionIntent"] = JsonSerializer.SerializeToElement(
						"Second public profile duplicate inquiry",
						JsonOptions),
					["desiredClass"] = JsonSerializer.SerializeToElement(desiredClassName, JsonOptions)
				}
			},
			JsonOptions);

		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var responseBody = await submitResponse.Content.ReadAsStringAsync();
		responseBody.ShouldNotContain("likelyDuplicate", Case.Insensitive);
		responseBody.ShouldNotContain(existingLead.Id.ToString(), Case.Insensitive);
		var result = JsonSerializer.Deserialize<PublicAdmissionInquirySubmitResultDto>(
			responseBody,
			JsonOptions);
		result.ShouldNotBeNull();

		var staffLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{result.LeadId}");
		staffLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await staffLeadResponse.Content.ReadAsStringAsync());
		var staffLead = await staffLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staffLead.ShouldNotBeNull();
		staffLead.LikelyDuplicateCount.ShouldBeGreaterThan(0);
		var duplicate = staffLead.LikelyDuplicates.Single(x => x.LeadId == existingLead.Id);
		duplicate.MatchReason.ShouldBe("Applicant name, parent name, and desired class");
		duplicate.ConfidenceScore.ShouldBe(80);
		duplicate.MatchedFields.ShouldContain("Parent or guardian");
		staffLead.DuplicateContinueReason.ShouldBe(
			"Public form submitted with likely duplicate details. Review before follow-up.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Record_Public_Form_Dob_Duplicate_Internally()
	{
		var dateOfBirth = new DateTimeOffset(2017, 9, 18, 0, 0, 0, TimeSpan.Zero);
		var desiredClassName = "Public DOB Grade " + Guid.NewGuid().ToString("N")[..6];
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Existing public DOB duplicate inquiry",
				PrimaryContactNo = "97000" + Random.Shared.Next(10000, 99999),
				Email = "public.dob.original." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = AdmissionInquiryChannel.WalkInReception,
				FirstName = "Kabir",
				DateOfBirth = dateOfBirth,
				DesiredClassName = desiredClassName
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var existingLead = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		existingLead.ShouldNotBeNull();
		var template = await CreatePublicInquiryTemplateAsync(
			"Public DOB duplicate " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();
		var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
			JsonOptions);
		form.ShouldNotBeNull();

		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			new PublicAdmissionInquirySubmitDto
			{
				ConsentVersion = form.ConsentVersion,
				ConsentAccepted = true,
				CommunicationPreferences = ["WhatsApp"],
				FieldValues = new Dictionary<string, JsonElement>
				{
					["studentFirstName"] = JsonSerializer.SerializeToElement("Kabir", JsonOptions),
					["studentDateOfBirth"] = JsonSerializer.SerializeToElement("2017-09-18", JsonOptions),
					["parentPhone"] = JsonSerializer.SerializeToElement(
						"98000" + Random.Shared.Next(10000, 99999),
						JsonOptions),
					["admissionIntent"] = JsonSerializer.SerializeToElement(
						"Second public DOB duplicate inquiry",
						JsonOptions),
					["desiredClass"] = JsonSerializer.SerializeToElement(desiredClassName, JsonOptions)
				}
			},
			JsonOptions);

		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var responseBody = await submitResponse.Content.ReadAsStringAsync();
		responseBody.ShouldNotContain("likelyDuplicate", Case.Insensitive);
		responseBody.ShouldNotContain(existingLead.Id.ToString(), Case.Insensitive);
		var result = JsonSerializer.Deserialize<PublicAdmissionInquirySubmitResultDto>(
			responseBody,
			JsonOptions);
		result.ShouldNotBeNull();

		var staffLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{result.LeadId}");
		staffLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await staffLeadResponse.Content.ReadAsStringAsync());
		var staffLead = await staffLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staffLead.ShouldNotBeNull();
		staffLead.DateOfBirth.ShouldNotBeNull();
		var duplicate = staffLead.LikelyDuplicates.Single(x => x.LeadId == existingLead.Id);
		duplicate.MatchReason.ShouldBe("Applicant name, date of birth, and desired class");
		duplicate.ConfidenceScore.ShouldBe(85);
		duplicate.MatchedFields.ShouldContain("Date of birth");
		staffLead.DuplicateContinueReason.ShouldBe(
			"Public form submitted with likely duplicate details. Review before follow-up.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Issue_And_Verify_Public_Status_Otp()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			applicantChecklist:
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
					Details = "Optional for early-years applicants.",
					IsRequired = false,
					DisplayOrder = 20
				}
			]);

		var template = await CreatePublicInquiryTemplateAsync("Status inquiry " + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using var anonymousClient = CreateAnonymousClient();
			var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
				JsonOptions);
			form.ShouldNotBeNull();

			var submitResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
				new PublicAdmissionInquirySubmitDto
				{
					ConsentVersion = form.ConsentVersion,
					ConsentAccepted = true,
					CommunicationPreferences = ["SMS"],
					FieldValues = new Dictionary<string, JsonElement>
					{
						["studentFirstName"] = JsonSerializer.SerializeToElement("Kabir", JsonOptions),
						["parentPhone"] = JsonSerializer.SerializeToElement("9911223344", JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for UKG", JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement("UKG", JsonOptions)
					}
				},
				JsonOptions);
			submitResponse.EnsureSuccessStatusCode();
			var submit = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
			submit.ShouldNotBeNull();

			var siblingSubmitResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
				new PublicAdmissionInquirySubmitDto
				{
					ConsentVersion = form.ConsentVersion,
					ConsentAccepted = true,
					CommunicationPreferences = ["SMS"],
					FieldValues = new Dictionary<string, JsonElement>
					{
						["studentFirstName"] = JsonSerializer.SerializeToElement("Kavya", JsonOptions),
						["parentPhone"] = JsonSerializer.SerializeToElement("9911223344", JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for Grade 2", JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement("Grade 2", JsonOptions)
					}
				},
				JsonOptions);
			siblingSubmitResponse.EnsureSuccessStatusCode();
			var siblingSubmit = await siblingSubmitResponse.Content
				.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
			siblingSubmit.ShouldNotBeNull();

			var challengeResponse = await anonymousClient.PostAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/otp",
				content: null);
			challengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await challengeResponse.Content.ReadAsStringAsync());
			var challenge = await challengeResponse.Content
				.ReadFromJsonAsync<AdmissionApplicantStatusOtpChallengeDto>(JsonOptions);
			challenge.ShouldNotBeNull();
			challenge.MaskedDestination.ShouldContain("3344");
			challenge.DeliveryStatus.ShouldNotBeNullOrWhiteSpace();
			challenge.OtpExpiresAt.ToUniversalTime().ShouldBeGreaterThan(DateTime.UtcNow);

			var immediateResendResponse = await anonymousClient.PostAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/otp",
				content: null);
			immediateResendResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			var immediateResendBody = await immediateResendResponse.Content.ReadAsStringAsync();
			immediateResendBody.ShouldContain("AdmissionCrm:StatusOtpResendTooSoon");

			var otpCode = await GetEncryptedOtpCodeForLeadAsync(submit.LeadId);
			var verifyResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/verify",
				new VerifyAdmissionApplicantStatusOtpDto { OtpCode = otpCode, Language = "en" },
				JsonOptions);

			verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyResponse.Content.ReadAsStringAsync());
			var status = await verifyResponse.Content.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
			status.ShouldNotBeNull();
			status.LeadId.ShouldBe(submit.LeadId);
			status.Language.ShouldBe("en");
			status.SupportedLanguages.ShouldBe(["en", "gu", "hi"]);
			status.DisplayName.ShouldBe("Kabir");
			status.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);
			status.StageName.ShouldNotBeNullOrWhiteSpace();
			status.Checklist.Count.ShouldBeGreaterThanOrEqualTo(5);
			status.Checklist.Single(x => x.Key == "inquiry-received").Status.ShouldBe("Completed");
			status.Checklist.Single(x => x.Key == "entrance-exam-schedule").Status.ShouldBe("Pending");
			status.ApplicantChecklist.Count.ShouldBe(2);
			status.ApplicantChecklist.Single(x => x.Key == "birth-certificate").Status.ShouldBe("Required");
			status.ApplicantChecklist.Single(x => x.Key == "previous-report-card").Status.ShouldBe("Optional");
			status.Activity.Count.ShouldBeGreaterThanOrEqualTo(2);
			status.Activity.Select(x => x.Title).ShouldContain("Inquiry submitted");
			status.Activity.Any(x =>
				x.Type == "Message" &&
				x.Title.Contains("Sms message", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
			status.Activity.Any(x =>
				x.Details != null &&
				x.Details.Contains("duplicate", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
			status.Exam.ShouldBeNull();
			status.AccessSessionToken.ShouldNotBeNullOrWhiteSpace();
			status.FamilyApplications.Count.ShouldBe(2);
			status.FamilyApplications.Select(x => x.DisplayName).ShouldContain("Kabir");
			status.FamilyApplications.Select(x => x.DisplayName).ShouldContain("Kavya");
			status.CommunicationPreferences.HasPhoneContact.ShouldBeTrue();
			status.CommunicationPreferences.HasEmailContact.ShouldBeFalse();
			status.CommunicationPreferences.Sms.ShouldBeTrue();
			status.CanWithdrawApplication.ShouldBeTrue();

			using var siblingStatusRequest = new HttpRequestMessage(
				HttpMethod.Get,
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{siblingSubmit.StatusToken}?language=en");
			siblingStatusRequest.Headers.Add("X-Admission-Access-Session", status.AccessSessionToken);
			var siblingStatusResponse = await anonymousClient.SendAsync(siblingStatusRequest);
			siblingStatusResponse.StatusCode.ShouldBe(
				HttpStatusCode.OK,
				await siblingStatusResponse.Content.ReadAsStringAsync());
			var siblingStatus = await siblingStatusResponse.Content
				.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
			siblingStatus.ShouldNotBeNull();
			siblingStatus.LeadId.ShouldBe(siblingSubmit.LeadId);
			siblingStatus.DisplayName.ShouldBe("Kavya");
			siblingStatus.FamilyApplications.Count.ShouldBe(2);

			var familyChallengeResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/family/otp",
				new StartAdmissionFamilyAccessDto { Contact = "9911223344" },
				JsonOptions);
			familyChallengeResponse.StatusCode.ShouldBe(
				HttpStatusCode.OK,
				await familyChallengeResponse.Content.ReadAsStringAsync());
			var familyChallenge = await familyChallengeResponse.Content
				.ReadFromJsonAsync<AdmissionApplicantStatusOtpChallengeDto>(JsonOptions);
			familyChallenge.ShouldNotBeNull();
			familyChallenge.ChallengeId.ShouldNotBeNull();
			familyChallenge.DeliveryStatus.ShouldContain("If active applications match");

			var familyOtpCode = await GetEncryptedOtpCodeForLeadAsync(siblingSubmit.LeadId);
			var familyVerifyResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/family/verify",
				new VerifyAdmissionFamilyAccessDto
				{
					ChallengeId = familyChallenge.ChallengeId.Value,
					OtpCode = familyOtpCode,
					Language = "en"
				},
				JsonOptions);
			familyVerifyResponse.StatusCode.ShouldBe(
				HttpStatusCode.OK,
				await familyVerifyResponse.Content.ReadAsStringAsync());
			var familyStatus = await familyVerifyResponse.Content
				.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
			familyStatus.ShouldNotBeNull();
			familyStatus.FamilyApplications.Count.ShouldBe(2);

			var preferencesResponse = await anonymousClient.PutAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/communication-preferences",
				new UpdatePublicAdmissionCommunicationPreferencesDto
				{
					AccessSessionToken = familyStatus.AccessSessionToken!,
					WhatsApp = false,
					Sms = true,
					Email = false
				}, JsonOptions);
			preferencesResponse.StatusCode.ShouldBe(
				HttpStatusCode.OK,
				await preferencesResponse.Content.ReadAsStringAsync());
			var preferencesStatus = await preferencesResponse.Content
				.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
			preferencesStatus.ShouldNotBeNull();
			preferencesStatus.CommunicationPreferences.WhatsApp.ShouldBeFalse();
			preferencesStatus.CommunicationPreferences.Sms.ShouldBeTrue();

			var withdrawResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/withdraw",
				new WithdrawPublicAdmissionApplicationDto
				{
					AccessSessionToken = familyStatus.AccessSessionToken!,
					Reason = "Family chose another school."
				}, JsonOptions);
			withdrawResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await withdrawResponse.Content.ReadAsStringAsync());
			var withdrawnStatus = await withdrawResponse.Content
				.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
			withdrawnStatus.ShouldNotBeNull();
			withdrawnStatus.Stage.ShouldBe(AdmissionInquiryStage.Archived);
			withdrawnStatus.CanWithdrawApplication.ShouldBeFalse();
			var withdrawnTimeline = await GetCrmTimelineAsync(submit.LeadId);
			withdrawnTimeline.Items.Any(x =>
				x.EventType == AdmissionCrmTimelineEventType.FamilyApplicationWithdrawn).ShouldBeTrue();
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(originalCycle, originalCycle.ExamReadinessFields);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Allow_Only_Verified_Portal_Guardian_To_Access_Authorized_Siblings()
	{
		var template = await CreatePublicInquiryTemplateAsync(
			"Delegated family access " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();
		var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}", JsonOptions);
		form.ShouldNotBeNull();

		async Task<PublicAdmissionInquirySubmitResultDto> SubmitAsync(string name, string phone, string grade)
		{
			var response = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
				new PublicAdmissionInquirySubmitDto
				{
					ConsentVersion = form.ConsentVersion,
					ConsentAccepted = true,
					CommunicationPreferences = ["SMS"],
					FieldValues = new Dictionary<string, JsonElement>
					{
						["studentFirstName"] = JsonSerializer.SerializeToElement(name, JsonOptions),
						["parentPhone"] = JsonSerializer.SerializeToElement(phone, JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement($"Admission inquiry for {grade}", JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement(grade, JsonOptions)
					}
				}, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			return (await response.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions))!;
		}

		var first = await SubmitAsync("Aarav", "9911000001", "Grade 1");
		var second = await SubmitAsync("Anaya", "9911000002", "Grade 3");
		var firstProfileResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{first.LeadId}/household-identity",
			new CreateAdmissionCrmApplicantIdentityProfileDto
			{
				HouseholdDisplayName = "Shah household",
				PreferredLanguage = "gu",
				Applicant = new AdmissionCrmPersonInputDto
				{
					FirstName = "Aarav",
					Phone = "9911000001",
					Provenance = AdmissionCrmPersonProvenance.ApplicantSupplied
				}
			}, JsonOptions);
		firstProfileResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstProfileResponse.Content.ReadAsStringAsync());
		var firstProfile = await firstProfileResponse.Content
			.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		firstProfile.ShouldNotBeNull();

		var guardianResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{first.LeadId}/household-identity/guardians",
			new CreateAdmissionCrmGuardianRelationshipDto
			{
				Guardian = new AdmissionCrmPersonInputDto
				{
					FirstName = "Nisha",
					LastName = "Shah",
					Phone = "9876500099",
					Provenance = AdmissionCrmPersonProvenance.StaffSupplied
				},
				RelationshipType = AdmissionCrmGuardianRelationshipType.Mother,
				IsPrimaryContact = false,
				IsAccountableContact = true,
				HasPortalAccess = true,
				CommunicationPriority = 1
			}, JsonOptions);
		guardianResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await guardianResponse.Content.ReadAsStringAsync());
		var profileWithGuardian = await guardianResponse.Content
			.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		var firstGuardian = profileWithGuardian!.Guardians.ShouldHaveSingleItem();
		var unverifiedChallengeResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/family/otp",
			new StartAdmissionFamilyAccessDto { Contact = "9876500099" }, JsonOptions);
		unverifiedChallengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
			await unverifiedChallengeResponse.Content.ReadAsStringAsync());
		var unverifiedChallenge = await unverifiedChallengeResponse.Content
			.ReadFromJsonAsync<AdmissionApplicantStatusOtpChallengeDto>(JsonOptions);
		(await StatusAccessExistsAsync(unverifiedChallenge!.ChallengeId!.Value)).ShouldBeFalse();
		var verifyFirst = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/household-identity/guardians/{firstGuardian.Id}/verify-authority",
			new VerifyAdmissionCrmGuardianAuthorityDto { EvidenceReference = "BIRTH-CERT-DELEGATED-1" }, JsonOptions);
		verifyFirst.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyFirst.Content.ReadAsStringAsync());

		var secondProfileResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{second.LeadId}/household-identity",
			new CreateAdmissionCrmApplicantIdentityProfileDto
			{
				ExistingHouseholdId = firstProfile.HouseholdId,
				HouseholdDisplayName = "Shah household",
				Applicant = new AdmissionCrmPersonInputDto
				{
					FirstName = "Anaya",
					Phone = "9911000002",
					Provenance = AdmissionCrmPersonProvenance.ApplicantSupplied
				}
			}, JsonOptions);
		secondProfileResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondProfileResponse.Content.ReadAsStringAsync());
		var secondGuardianResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{second.LeadId}/household-identity/guardians",
			new CreateAdmissionCrmGuardianRelationshipDto
			{
				ExistingGuardianPersonId = firstGuardian.GuardianPersonId,
				RelationshipType = AdmissionCrmGuardianRelationshipType.Mother,
				IsAccountableContact = true,
				HasPortalAccess = true,
				CommunicationPriority = 1
			}, JsonOptions);
		secondGuardianResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondGuardianResponse.Content.ReadAsStringAsync());
		var secondProfile = await secondGuardianResponse.Content
			.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		var verifySecond = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/household-identity/guardians/{secondProfile!.Guardians.ShouldHaveSingleItem().Id}/verify-authority",
			new VerifyAdmissionCrmGuardianAuthorityDto { EvidenceReference = "BIRTH-CERT-DELEGATED-2" }, JsonOptions);
		verifySecond.StatusCode.ShouldBe(HttpStatusCode.OK, await verifySecond.Content.ReadAsStringAsync());
		await AssertDelegatedFamilyAccessSetupAsync(firstGuardian.GuardianPersonId, first.LeadId, second.LeadId);

		var eventStartsAt = DateTime.UtcNow.AddDays(2);
		var createEventResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/events",
			new CreateUpdateAdmissionCrmEventDto
			{
				Title = "Family campus visit",
				EventType = AdmissionCrmEventType.CampusVisit,
				StartsAt = eventStartsAt,
				EndsAt = eventStartsAt.AddHours(1),
				Location = "Main reception",
				Capacity = 2,
				Instructions = "Bring the applicant's inquiry reference.",
				AllowFamilySelfScheduling = true
			}, JsonOptions);
		createEventResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createEventResponse.Content.ReadAsStringAsync());
		var admissionEvent = await createEventResponse.Content.ReadFromJsonAsync<AdmissionCrmEventDto>(JsonOptions);
		var publishEventResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/events/{admissionEvent!.Id}/publish", null);
		publishEventResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await publishEventResponse.Content.ReadAsStringAsync());

		var challengeResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/family/otp",
			new StartAdmissionFamilyAccessDto { Contact = "9876500099" }, JsonOptions);
		challengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await challengeResponse.Content.ReadAsStringAsync());
		var challenge = await challengeResponse.Content
			.ReadFromJsonAsync<AdmissionApplicantStatusOtpChallengeDto>(JsonOptions);
		challenge.ShouldNotBeNull();
		var otpCode = await GetEncryptedOtpCodeForChallengeAsync(challenge.ChallengeId!.Value);
		var verifyResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/family/verify",
			new VerifyAdmissionFamilyAccessDto
			{
				ChallengeId = challenge.ChallengeId.Value,
				OtpCode = otpCode,
				Language = "gu"
			}, JsonOptions);
		verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyResponse.Content.ReadAsStringAsync());
		var family = await verifyResponse.Content.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
		family.ShouldNotBeNull();
		family.FamilyApplications.Select(x => x.DisplayName).ShouldBe(["Anaya", "Aarav"], ignoreOrder: true);
		var aaravToken = family.FamilyApplications.Single(x => x.LeadId == first.LeadId).StatusToken;
		using var eventsRequest = new HttpRequestMessage(HttpMethod.Get,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{aaravToken}/events");
		eventsRequest.Headers.Add("X-Admission-Access-Session", family.AccessSessionToken);
		var eventsResponse = await anonymousClient.SendAsync(eventsRequest);
		eventsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await eventsResponse.Content.ReadAsStringAsync());
		var publicEvents = await eventsResponse.Content.ReadFromJsonAsync<AdmissionCrmPublicEventListDto>(JsonOptions);
		publicEvents!.Events.ShouldHaveSingleItem().Id.ShouldBe(admissionEvent.Id);

		var reserveResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{aaravToken}/events/{admissionEvent.Id}/reserve",
			new ReserveAdmissionCrmPublicEventDto { AccessSessionToken = family.AccessSessionToken! }, JsonOptions);
		reserveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reserveResponse.Content.ReadAsStringAsync());
		var reservedEvent = await reserveResponse.Content.ReadFromJsonAsync<AdmissionCrmPublicEventDto>(JsonOptions);
		reservedEvent!.RegistrationStatus.ShouldBe(AdmissionCrmEventRegistrationStatus.Reserved);
		reservedEvent.AvailableCount.ShouldBe(1);

		var cancelResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{aaravToken}/events/{admissionEvent.Id}/cancel",
			new CancelAdmissionCrmPublicEventRegistrationDto
			{
				AccessSessionToken = family.AccessSessionToken!,
				Reason = "Plans changed."
			}, JsonOptions);
		cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cancelResponse.Content.ReadAsStringAsync());
		var cancelledEvent = await cancelResponse.Content.ReadFromJsonAsync<AdmissionCrmPublicEventDto>(JsonOptions);
		cancelledEvent!.RegistrationStatus.ShouldBe(AdmissionCrmEventRegistrationStatus.Cancelled);
		cancelledEvent.AvailableCount.ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Applicant_Checklist_Progress_And_Show_Public_Status()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			applicantChecklist:
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
					Details = "Optional for early-years applicants.",
					IsRequired = false,
					DisplayOrder = 20
				}
			]);

		var template = await CreatePublicInquiryTemplateAsync("Checklist status inquiry " + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using var anonymousClient = CreateAnonymousClient();
			var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
				JsonOptions);
			form.ShouldNotBeNull();

			var submitResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
				new PublicAdmissionInquirySubmitDto
				{
					ConsentVersion = form.ConsentVersion,
					ConsentAccepted = true,
					CommunicationPreferences = ["SMS"],
					FieldValues = new Dictionary<string, JsonElement>
					{
						["studentFirstName"] = JsonSerializer.SerializeToElement("Meera", JsonOptions),
						["parentPhone"] = JsonSerializer.SerializeToElement(
							"99770" + Random.Shared.Next(10000, 99999),
							JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement(
							"Admission inquiry for Grade 2",
							JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement("Grade 2", JsonOptions)
					}
				},
				JsonOptions);
			submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
			var submit = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
			submit.ShouldNotBeNull();

			var checklistResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/leads/{submit.LeadId}/applicant-checklist");
			checklistResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await checklistResponse.Content.ReadAsStringAsync());
			var checklist = await checklistResponse.Content
				.ReadFromJsonAsync<List<AdmissionCrmApplicantChecklistProgressDto>>(JsonOptions);
			checklist.ShouldNotBeNull();
			checklist.Single(x => x.Key == "birth-certificate").Status
				.ShouldBe(AdmissionCrmApplicantChecklistProgressStatus.Pending);

			var updateResponse = await Client.PutAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{submit.LeadId}/applicant-checklist/birth-certificate",
				new UpdateAdmissionCrmApplicantChecklistProgressDto
				{
					Status = AdmissionCrmApplicantChecklistProgressStatus.Received,
					Notes = "Parent submitted original for verification."
				},
				JsonOptions);
			updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
			var updated = await updateResponse.Content
				.ReadFromJsonAsync<AdmissionCrmApplicantChecklistProgressDto>(JsonOptions);
			updated.ShouldNotBeNull();
			updated.Status.ShouldBe(AdmissionCrmApplicantChecklistProgressStatus.Received);
			updated.StatusLabel.ShouldBe("Received");
			updated.IsComplete.ShouldBeTrue();
			updated.Notes.ShouldBe("Parent submitted original for verification.");
			updated.StatusChangedAt.ShouldNotBeNull();

			var timeline = await GetCrmTimelineAsync(submit.LeadId);
			timeline.Items.Any(x => x.EventType == AdmissionCrmTimelineEventType.ApplicantChecklistUpdated)
				.ShouldBeTrue();

			var challengeResponse = await anonymousClient.PostAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/otp",
				content: null);
			challengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await challengeResponse.Content.ReadAsStringAsync());
			var otpCode = await GetEncryptedOtpCodeForLeadAsync(submit.LeadId);
			var verifyResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/verify",
				new VerifyAdmissionApplicantStatusOtpDto { OtpCode = otpCode },
				JsonOptions);
			verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyResponse.Content.ReadAsStringAsync());
			var status = await verifyResponse.Content.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
			status.ShouldNotBeNull();

			var birthCertificate = status.ApplicantChecklist.Single(x => x.Key == "birth-certificate");
			birthCertificate.Status.ShouldBe("Received");
			birthCertificate.StatusCode.ShouldBe("Received");
			birthCertificate.IsRequired.ShouldBeTrue();
			birthCertificate.IsComplete.ShouldBeTrue();
			birthCertificate.Details.ShouldBe("Bring one photocopy for verification.");
			birthCertificate.StatusChangedAt.ShouldNotBeNull();

			var reportCard = status.ApplicantChecklist.Single(x => x.Key == "previous-report-card");
			reportCard.Status.ShouldBe("Optional");
			reportCard.StatusCode.ShouldBe("Pending");
			reportCard.IsRequired.ShouldBeFalse();
			reportCard.IsComplete.ShouldBeFalse();
			status.Activity.Any(x => x.Title == "Applicant checklist updated").ShouldBeTrue();
			status.Activity.Any(x =>
				x.Details != null &&
				x.Details.Contains("Parent submitted original", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(originalCycle, originalCycle.ExamReadinessFields);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Lock_Public_Status_Otp_After_Repeated_Invalid_Attempts()
	{
		var template = await CreatePublicInquiryTemplateAsync("Status lock inquiry " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();
		var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
			JsonOptions);
		form.ShouldNotBeNull();

		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			new PublicAdmissionInquirySubmitDto
			{
				ConsentVersion = form.ConsentVersion,
				ConsentAccepted = true,
				CommunicationPreferences = ["SMS"],
				FieldValues = new Dictionary<string, JsonElement>
				{
					["studentFirstName"] = JsonSerializer.SerializeToElement("Aarav", JsonOptions),
					["parentPhone"] = JsonSerializer.SerializeToElement("9922334455", JsonOptions),
					["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for Grade 1", JsonOptions),
					["desiredClass"] = JsonSerializer.SerializeToElement("Grade 1", JsonOptions)
				}
			},
			JsonOptions);
		submitResponse.EnsureSuccessStatusCode();
		var submit = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
		submit.ShouldNotBeNull();

		var challengeResponse = await anonymousClient.PostAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/otp",
			content: null);
		challengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await challengeResponse.Content.ReadAsStringAsync());

		for (var attempt = 0; attempt < 5; attempt++)
		{
			var invalidResponse = await anonymousClient.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/verify",
				new VerifyAdmissionApplicantStatusOtpDto { OtpCode = "000000" },
				JsonOptions);
			invalidResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			var invalidBody = await invalidResponse.Content.ReadAsStringAsync();
			invalidBody.ShouldContain("AdmissionCrm:InvalidStatusOtp");
		}

		var failedAttemptCount = await GetStatusOtpFailedAttemptCountForLeadAsync(submit.LeadId);
		failedAttemptCount.ShouldBe(5);

		var lockedResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/verify",
			new VerifyAdmissionApplicantStatusOtpDto { OtpCode = "000000" },
			JsonOptions);
		lockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var lockedBody = await lockedResponse.Content.ReadAsStringAsync();
		lockedBody.ShouldContain("AdmissionCrm:StatusOtpLocked");

		var otpCode = await GetEncryptedOtpCodeForLeadAsync(submit.LeadId);
		var correctAfterLockResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/verify",
			new VerifyAdmissionApplicantStatusOtpDto { OtpCode = otpCode },
			JsonOptions);
		correctAfterLockResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var correctAfterLockBody = await correctAfterLockResponse.Content.ReadAsStringAsync();
		correctAfterLockBody.ShouldContain("AdmissionCrm:StatusOtpLocked");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Send_Public_Status_Otp_Through_Configured_Provider()
	{
		var template = await CreatePublicInquiryTemplateAsync("Provider OTP inquiry " + Guid.NewGuid().ToString("N")[..8]);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var providerResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.Sms}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "SMS Sandbox",
				SenderId = "school-sms",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto()
			},
			JsonOptions);
		providerResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await providerResponse.Content.ReadAsStringAsync());

		using var anonymousClient = CreateAnonymousClient();
		var form = await anonymousClient.GetFromJsonAsync<PublicAdmissionInquiryFormDto>(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}",
			JsonOptions);
		form.ShouldNotBeNull();

		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			new PublicAdmissionInquirySubmitDto
			{
				ConsentVersion = form.ConsentVersion,
				ConsentAccepted = true,
				CommunicationPreferences = ["SMS"],
				FieldValues = new Dictionary<string, JsonElement>
				{
					["studentFirstName"] = JsonSerializer.SerializeToElement("Anaya", JsonOptions),
					["parentPhone"] = JsonSerializer.SerializeToElement("9922334455", JsonOptions),
					["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for Grade 2", JsonOptions),
					["desiredClass"] = JsonSerializer.SerializeToElement("Grade 2", JsonOptions)
				}
			},
			JsonOptions);
		submitResponse.EnsureSuccessStatusCode();
		var submit = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
		submit.ShouldNotBeNull();

		var challengeResponse = await anonymousClient.PostAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{submit.StatusToken}/otp",
			content: null);
		challengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await challengeResponse.Content.ReadAsStringAsync());
		var challenge = await challengeResponse.Content
			.ReadFromJsonAsync<AdmissionApplicantStatusOtpChallengeDto>(JsonOptions);
		challenge.ShouldNotBeNull();
		challenge.DeliveryConfigured.ShouldBeTrue();
		challenge.DeliveryStatus.ShouldContain("Queued for SMS Sandbox");

		var otpCode = await GetEncryptedOtpCodeForLeadAsync(submit.LeadId);
		var logsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?leadId={submit.LeadId}&channel={(int) AdmissionCrmMessageChannel.Sms}");
		logsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await logsResponse.Content.ReadAsStringAsync());
		var logs = await logsResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>>(JsonOptions);
		logs.ShouldNotBeNull();
		var otpLog = logs.Items.Single(x => x.TemplateId == null);
		otpLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.QueuedStatus);
		otpLog.ProviderName.ShouldBe("SMS Sandbox");
		otpLog.BodySnapshot.ShouldContain("******");
		otpLog.BodySnapshot.ShouldNotContain(otpCode);
		otpLog.AttemptCount.ShouldBe(1);

		var timeline = await GetCrmTimelineAsync(submit.LeadId);
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.MessageTemplateChanged &&
			x.RelatedEntityId == otpLog.Id).ShouldBeTrue();
	}

}
