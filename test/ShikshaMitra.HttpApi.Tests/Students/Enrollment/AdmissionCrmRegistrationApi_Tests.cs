using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
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
public class AdmissionCrmRegistrationApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmRegistrationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Preserve_Registration_Template_Localizations()
	{
		var input = CreateTemplateInput("Localized registration " + Guid.NewGuid().ToString("N")[..8]);
		var section = input.Sections.Single();
		section.LocalizedTitles = new Dictionary<string, string>
		{
			["GU"] = "કુટુંબ અને ઘોષણા",
			["Hi"] = "परिवार और घोषणा"
		};
		var field = section.Fields.Single(item => item.Key == "studentFirstName");
		field.LocalizedLabels = new Dictionary<string, string>
		{
			["GU"] = "વિદ્યાર્થીનું નામ",
			["Hi"] = "विद्यार्थी का नाम"
		};
		var optionField = section.Fields.Single(item => item.Key == "parentOccupation");
		optionField.FieldType = AdmissionCrmFormFieldType.Select;
		optionField.MinimumLength = null;
		optionField.Options = ["Service", "Business"];
		optionField.LocalizedOptions = new Dictionary<string, IReadOnlyList<string>>
		{
			["HI"] = ["नौकरी", "व्यवसाय"]
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var template = await response.Content.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.Sections.Single().LocalizedTitles.Keys.ShouldBe(["gu", "hi"], ignoreOrder: true);
		template.Sections.Single().LocalizedTitles["gu"].ShouldBe("કુટુંબ અને ઘોષણા");
		var localizedLabels = template.Sections.Single().Fields
			.Single(item => item.Key == "studentFirstName")
			.LocalizedLabels;
		localizedLabels.Keys.ShouldBe(["gu", "hi"], ignoreOrder: true);
		localizedLabels["hi"].ShouldBe("विद्यार्थी का नाम");
		var localizedOptions = template.Sections.Single().Fields
			.Single(item => item.Key == "parentOccupation")
			.LocalizedOptions;
		localizedOptions.Keys.ShouldBe(["hi"]);
		localizedOptions["hi"].ShouldBe(["नौकरी", "व्यवसाय"]);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Registration_Field_Type_And_Options()
	{
		var invalidType = CreateTemplateInput("Invalid type " + Guid.NewGuid().ToString("N")[..8]);
		invalidType.Sections.Single().Fields[0].FieldType = (AdmissionCrmFormFieldType)999;
		var invalidTypeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			invalidType,
			JsonOptions);
		invalidTypeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var missingOptions = CreateTemplateInput("Missing options " + Guid.NewGuid().ToString("N")[..8]);
		missingOptions.Sections.Single().Fields[0].FieldType = AdmissionCrmFormFieldType.Select;
		missingOptions.Sections.Single().Fields[0].Options = [];
		var missingOptionsResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			missingOptions,
			JsonOptions);
		missingOptionsResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Owner_And_Follow_Up_Before_Standard_Registration()
	{
		var resources = await CreatePublishedRegistrationResourcesAsync("Gate owner");
		var playbookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.RegistrationBeforeExam);
		var lead = await CreateRegistrationLeadAsync("GateOwner");
		await SetLeadWorkflowStateAsync(lead.Id, AdmissionInquiryStage.NewInquiry, ownerUserId: null);
		var readinessResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/readiness");
		readinessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var readiness = await readinessResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationReadinessDto>(JsonOptions);
		readiness.ShouldNotBeNull();
		readiness.CanIssue.ShouldBeFalse();
		readiness.Blockers.ShouldContain(item => item.Code == "AdmissionCrm:RegistrationOwnerRequired");

		var unassignedResponse = await IssueRegistrationAsync(lead.Id, resources, playbookId);
		unassignedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await unassignedResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationOwnerRequired");
		var legacyUnassignedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registration-form",
			new IssueAdmissionCrmRegistrationFormDto
			{
				FormNumber = "LEGACY-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
				Amount = 1
			},
			JsonOptions);
		legacyUnassignedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await legacyUnassignedResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationOwnerRequired");

		await SetLeadWorkflowStateAsync(lead.Id, AdmissionInquiryStage.NewInquiry, Guid.NewGuid());
		var inquiryResponse = await IssueRegistrationAsync(lead.Id, resources, playbookId);
		inquiryResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await inquiryResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationFollowUpRequired");

		await SetLeadWorkflowStateAsync(lead.Id, AdmissionInquiryStage.FollowUp, Guid.NewGuid());
		var followUpResponse = await IssueRegistrationAsync(lead.Id, resources, playbookId);
		followUpResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await followUpResponse.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Issue_All_Access_Modes_Without_Duplicating_A_Shared_Draft()
	{
		foreach (var mode in Enum.GetValues<AdmissionCrmRegistrationAccessMode>())
		{
			var resources = await CreatePublishedRegistrationResourcesAsync("Access " + mode);
			var playbookId = await CreatePublishedRegistrationPlaybookAsync(
				AdmissionCrmRegistrationPath.RegistrationBeforeExam);
			var lead = await CreateRegistrationLeadAsync("Access" + mode);
			await SetLeadWorkflowStateAsync(lead.Id, AdmissionInquiryStage.FollowUp, Guid.NewGuid());

			var response = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/issue",
				new IssueAdmissionCrmRegistrationApplicationDto
				{
					RegistrationTemplateId = resources.TemplateId,
					FormNumberSeriesId = resources.SeriesId,
					PlaybookId = playbookId,
					AccessMode = mode
				},
				JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var result = await response.Content
				.ReadFromJsonAsync<AdmissionCrmRegistrationIssuanceResultDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.Registration.AccessMode.ShouldBe(mode);
			if (mode == AdmissionCrmRegistrationAccessMode.StaffAssisted)
			{
				result.Delivery.ShouldBeNull();
				result.DeliveryError.ShouldBeNull();
			}
			else
			{
				(result.Delivery != null || !string.IsNullOrWhiteSpace(result.DeliveryError)).ShouldBeTrue();
				var retryResponse = await Client.PostAsync(
					$"/api/student-enrollment/admissions-crm/registrations/{result.Registration.Id}/invitation/send",
					null);
				retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await retryResponse.Content.ReadAsStringAsync());
				var retryResult = await retryResponse.Content
					.ReadFromJsonAsync<AdmissionCrmRegistrationIssuanceResultDto>(JsonOptions);
				retryResult.ShouldNotBeNull();
				retryResult.Registration.Id.ShouldBe(result.Registration.Id);
				(retryResult.Delivery != null || !string.IsNullOrWhiteSpace(retryResult.DeliveryError)).ShouldBeTrue();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Enforce_Before_And_After_Exam_Registration_Paths()
	{
		var resources = await CreatePublishedRegistrationResourcesAsync("Gate sequence");
		var beforeExamPlaybookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.RegistrationBeforeExam);
		var lateLead = await CreateRegistrationLeadAsync("GateLate");
		await SetLeadWorkflowStateAsync(
			lateLead.Id,
			AdmissionInquiryStage.ExamScheduled,
			Guid.NewGuid());

		var lateResponse = await IssueRegistrationAsync(
			lateLead.Id,
			resources,
			beforeExamPlaybookId);
		lateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await lateResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationMustBeIssuedBeforeExam");

		var afterExamPlaybookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.RegistrationAfterExam);
		var resultLead = await CreateRegistrationLeadAsync("GateResult");
		await SetLeadWorkflowStateAsync(
			resultLead.Id,
			AdmissionInquiryStage.Eligible,
			Guid.NewGuid());

		var missingResultResponse = await IssueRegistrationAsync(
			resultLead.Id,
			resources,
			afterExamPlaybookId);
		missingResultResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingResultResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationRequiresEligibleExamResult");

		await AddEligibleExamResultAsync(resultLead.Id);
		var eligibleResponse = await IssueRegistrationAsync(
			resultLead.Id,
			resources,
			afterExamPlaybookId);
		eligibleResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await eligibleResponse.Content.ReadAsStringAsync());

		var overrideLead = await CreateRegistrationLeadAsync("GateOverride");
		await SetLeadWorkflowStateAsync(overrideLead.Id, AdmissionInquiryStage.ExamScheduled, Guid.NewGuid());
		await AddScheduledEligibilityOverrideAsync(overrideLead.Id);
		await SetLeadWorkflowStateAsync(overrideLead.Id, AdmissionInquiryStage.Eligible, Guid.NewGuid());
		var overrideResponse = await IssueRegistrationAsync(
			overrideLead.Id,
			resources,
			afterExamPlaybookId);
		overrideResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await overrideResponse.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData(AdmissionInquiryStage.Rejected)]
	[InlineData(AdmissionInquiryStage.Stale)]
	[InlineData(AdmissionInquiryStage.Archived)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Registration_For_Terminal_Lead(
		AdmissionInquiryStage terminalStage)
	{
		var resources = await CreatePublishedRegistrationResourcesAsync(
			"Gate terminal " + terminalStage);
		var playbookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.ExamWaived);
		var lead = await CreateRegistrationLeadAsync("Gate" + terminalStage);
		await SetLeadWorkflowStateAsync(lead.Id, terminalStage, Guid.NewGuid());

		var response = await IssueRegistrationAsync(lead.Id, resources, playbookId);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationNotAllowedForTerminalLead");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reserve_Direct_Registration_Path_For_Direct_Endpoint()
	{
		var resources = await CreatePublishedRegistrationResourcesAsync("Gate direct");
		var directPlaybookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.DirectRegistration);
		var lead = await CreateRegistrationLeadAsync("GateDirect");
		await SetLeadWorkflowStateAsync(
			lead.Id,
			AdmissionInquiryStage.FollowUp,
			Guid.NewGuid());

		var response = await IssueRegistrationAsync(lead.Id, resources, directPlaybookId);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:DirectRegistrationPathRequiresDirectEndpoint");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Waiver_Permission_For_Standard_And_Direct_Issuance()
	{
		var resources = await CreatePublishedRegistrationResourcesAsync("Waiver gate");
		var playbookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.ExamWaived);
		var lead = await CreateRegistrationLeadAsync("WaiverGate");
		await SetLeadWorkflowStateAsync(
			lead.Id,
			AdmissionInquiryStage.FollowUp,
			Guid.NewGuid());
		var waiverInput = new IssueAdmissionCrmRegistrationApplicationDto
		{
			RegistrationTemplateId = resources.TemplateId,
			FormNumberSeriesId = resources.SeriesId,
			PlaybookId = playbookId,
			WaiveFee = true,
			WaiverReason = "Approved hardship waiver."
		};

		using var issueOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm);
		var deniedStandardResponse = await issueOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/issue",
			waiverInput,
			JsonOptions);
		deniedStandardResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await deniedStandardResponse.Content.ReadAsStringAsync());

		using var waiverClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveRegistrationFee);
		var allowedStandardResponse = await waiverClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/issue",
			waiverInput,
			JsonOptions);
		allowedStandardResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await allowedStandardResponse.Content.ReadAsStringAsync());
		var standardIssuance = await allowedStandardResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationIssuanceResultDto>(JsonOptions);
		var standardRegistration = standardIssuance?.Registration;
		standardRegistration.ShouldNotBeNull();
		standardRegistration.FeeStatus.ShouldBe(AdmissionCrmRegistrationFeeStatus.Waived);

		CreateDirectAdmissionCrmRegistrationDto BuildDirectInput(string firstName) =>
			new()
			{
				Applicant = new CreateAdmissionCrmLeadDto
				{
					FirstName = firstName,
					ParentGuardianName = "Waiver Parent",
					PrimaryContactNo = "94" + Random.Shared.NextInt64(10000000, 99999999),
					AdmissionIntent = "Direct registration waiver",
					TargetStandardId = Factory.SeededStandardId,
					AcademicBoardId = Factory.SeededBoardId,
					AcademicMediumId = Factory.SeededMediumId,
					DesiredClassName = "Grade 1"
				},
				Registration = new IssueAdmissionCrmRegistrationApplicationDto
				{
					RegistrationTemplateId = resources.TemplateId,
					FormNumberSeriesId = resources.SeriesId,
					WaiveFee = true,
					WaiverReason = "Approved direct-registration hardship waiver."
				}
			};

		using var directWithoutWaiverClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm);
		var deniedDirectResponse = await directWithoutWaiverClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/registrations/direct",
			BuildDirectInput("DeniedDirectWaiver"),
			JsonOptions);
		deniedDirectResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await deniedDirectResponse.Content.ReadAsStringAsync());

		using var directWaiverClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveRegistrationFee);
		var allowedDirectResponse = await directWaiverClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/registrations/direct",
			BuildDirectInput("AllowedDirectWaiver"),
			JsonOptions);
		allowedDirectResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await allowedDirectResponse.Content.ReadAsStringAsync());
		var directRegistration = await allowedDirectResponse.Content
			.ReadFromJsonAsync<AdmissionCrmDirectRegistrationResultDto>(JsonOptions);
		directRegistration.ShouldNotBeNull();
		directRegistration.Registration.FeeStatus.ShouldBe(AdmissionCrmRegistrationFeeStatus.Waived);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Roll_Back_Failed_Verification_And_Complete_Secure_Versioned_Registration_Workflow()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var templateInput = CreateTemplateInput("Primary registration " + unique);
		templateInput.IsFallback = false;
		templateInput.Applicability = new AdmissionCrmPlaybookApplicabilityDto
		{
			Campus = "Workflow campus " + unique
		};
		var createTemplateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			templateInput,
			JsonOptions);
		createTemplateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createTemplateResponse.Content.ReadAsStringAsync());
		var template = await createTemplateResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();

		var publishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}/publish",
			null);
		publishResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await publishResponse.Content.ReadAsStringAsync());
		var published = await publishResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		published.ShouldNotBeNull();
		published.Status.ShouldBe(AdmissionCrmRegistrationTemplateStatus.Published);

		var immutableResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}",
			templateInput,
			JsonOptions);
		immutableResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var versionResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}/new-version",
			null);
		versionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await versionResponse.Content.ReadAsStringAsync());
		var nextVersion = await versionResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		nextVersion.ShouldNotBeNull();
		nextVersion.Version.ShouldBe(2);
		nextVersion.Status.ShouldBe(AdmissionCrmRegistrationTemplateStatus.Draft);

		var seriesResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-number-series",
			new AdmissionCrmFormNumberSeriesUpsertDto
			{
				Name = "Primary series " + unique,
				Prefix = "REG-" + unique.ToUpperInvariant() + "-",
				Padding = 4,
				NextNumber = 1,
				IsActive = true
			},
			JsonOptions);
		seriesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await seriesResponse.Content.ReadAsStringAsync());
		var series = await seriesResponse.Content.ReadFromJsonAsync<AdmissionCrmFormNumberSeriesDto>(JsonOptions);
		series.ShouldNotBeNull();

		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Admission inquiry for Grade 1",
				PrimaryContactNo = "98" + Random.Shared.NextInt64(10000000, 99999999),
				Email = $"aarav-{unique}@example.test",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Aarav"
			},
			JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		await SetLeadWorkflowStateAsync(
			lead.Id,
			AdmissionInquiryStage.FollowUp,
			Guid.NewGuid());
		var registrationPlaybookId = await CreatePublishedRegistrationPlaybookAsync(
			AdmissionCrmRegistrationPath.RegistrationBeforeExam);

		var issueInput = new IssueAdmissionCrmRegistrationApplicationDto
		{
			RegistrationTemplateId = published.Id,
			FormNumberSeriesId = series.Id,
			PlaybookId = registrationPlaybookId
		};
		var issueResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/issue",
			issueInput,
			JsonOptions);
		issueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await issueResponse.Content.ReadAsStringAsync());
		var issuance = await issueResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationIssuanceResultDto>(JsonOptions);
		var issued = issuance?.Registration;
		issued.ShouldNotBeNull();
		issued.FormNumber.ShouldBe(series.NextPreview);
		issued.RegistrationTemplateVersion.ShouldBe(1);
		issued.ApplicantStatusPath.ShouldNotBeNullOrWhiteSpace();
		var firstStatusPath = issued.ApplicantStatusPath;

		var duplicateIssueResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/issue",
			issueInput,
			JsonOptions);
		duplicateIssueResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await duplicateIssueResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationApplicationAlreadyIssued");

		var revokeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/revoke",
			new AdmissionCrmRegistrationReasonDto
			{
				Reason = "Applicant requested a controlled link replacement."
			},
			JsonOptions);
		revokeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await revokeResponse.Content.ReadAsStringAsync());

		var reissueResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/reissue",
			new AdmissionCrmRegistrationReissueDto
			{
				LinkExpiryDays = 14,
				Reason = "Applicant confirmed the replacement link."
			},
			JsonOptions);
		reissueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reissueResponse.Content.ReadAsStringAsync());
		var reissued = await reissueResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		reissued.ShouldNotBeNull();
		reissued.Id.ShouldBe(issued.Id);
		reissued.FormNumber.ShouldBe(issued.FormNumber);
		reissued.ApplicantStatusPath.ShouldNotBeNullOrWhiteSpace();
		reissued.ApplicantStatusPath.ShouldNotBe(firstStatusPath);
		issued = reissued;
		var issuedDocumentAvailabilityResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/documents/availability");
		issuedDocumentAvailabilityResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await issuedDocumentAvailabilityResponse.Content.ReadAsStringAsync());
		var issuedDocumentAvailability = (await issuedDocumentAvailabilityResponse.Content
			.ReadFromJsonAsync<AdmissionCrmGeneratedDocumentAvailabilityDto[]>(JsonOptions))!;
		issuedDocumentAvailability.Single(item =>
			item.DocumentType == AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement)
			.IsAvailable.ShouldBeTrue();
		issuedDocumentAvailability.Single(item =>
			item.DocumentType == AdmissionCrmGeneratedDocumentType.CorrectionNotice)
			.IsAvailable.ShouldBeFalse();
		var emailProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{lead.AcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.Email}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "Admissions email queue",
				SenderId = "admissions",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto()
			},
			JsonOptions);
		emailProviderResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await emailProviderResponse.Content.ReadAsStringAsync());
		var emailDocumentResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/documents/send-email",
			new SendAdmissionCrmGeneratedDocumentEmailDto
			{
				DocumentType = AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement,
				Language = "en"
			},
			JsonOptions);
		emailDocumentResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await emailDocumentResponse.Content.ReadAsStringAsync());
		var emailedDocument = await emailDocumentResponse.Content
			.ReadFromJsonAsync<AdmissionCrmGeneratedDocumentDeliveryDto>(JsonOptions);
		emailedDocument.ShouldNotBeNull();
		emailedDocument.Document.Content.ShouldNotBeEmpty();
		emailedDocument.Delivery.ShouldNotBeNull();
		emailedDocument.Delivery.GeneratedDocumentType.ShouldBe(
			AdmissionCrmGeneratedDocumentType.RegistrationAcknowledgement);
		emailedDocument.Delivery.AttachmentLanguage.ShouldBe("en");
		emailedDocument.Delivery.AttachmentFileName.ShouldBe(emailedDocument.Document.FileName);
		var documentRetryResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/communication/delivery-logs/{emailedDocument.Delivery.Id}/retry",
			new RetryAdmissionCrmMessageDeliveryDto { Reason = "Retry generated registration document" },
			JsonOptions);
		documentRetryResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await documentRetryResponse.Content.ReadAsStringAsync());
		var retriedDocumentDelivery = await documentRetryResponse.Content
			.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		retriedDocumentDelivery.ShouldNotBeNull();
		retriedDocumentDelivery.AttemptCount.ShouldBeGreaterThan(emailedDocument.Delivery.AttemptCount);

		var currentRegistrationResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/current");
		currentRegistrationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await currentRegistrationResponse.Content.ReadAsStringAsync());
		var currentRegistration = await currentRegistrationResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		currentRegistration.ShouldNotBeNull();
		currentRegistration.Id.ShouldBe(issued.Id);
		currentRegistration.ApplicantStatusPath.ShouldBe(issued.ApplicantStatusPath);

		var applicationResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}");
		applicationResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await applicationResponse.Content.ReadAsStringAsync());
		var application = await applicationResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		application.ShouldNotBeNull();
		application.ApplicantStatusPath.ShouldBe(issued.ApplicantStatusPath);

		var repeatedCurrentResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/current");
		repeatedCurrentResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await repeatedCurrentResponse.Content.ReadAsStringAsync());
		var repeatedCurrent = await repeatedCurrentResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		repeatedCurrent.ShouldNotBeNull();
		repeatedCurrent.ApplicantStatusPath.ShouldBe(currentRegistration.ApplicantStatusPath);

		using var viewOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
		var viewOnlyCurrentResponse = await viewOnlyClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/current");
		viewOnlyCurrentResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await viewOnlyCurrentResponse.Content.ReadAsStringAsync());
		var viewOnlyCurrent = await viewOnlyCurrentResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		viewOnlyCurrent.ShouldNotBeNull();
		viewOnlyCurrent.ApplicantStatusPath.ShouldBeNull();

		var viewOnlyApplicationResponse = await viewOnlyClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}");
		viewOnlyApplicationResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await viewOnlyApplicationResponse.Content.ReadAsStringAsync());
		var viewOnlyApplication = await viewOnlyApplicationResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		viewOnlyApplication.ShouldNotBeNull();
		viewOnlyApplication.ApplicantStatusPath.ShouldBeNull();

		var statusParts = repeatedCurrent.ApplicantStatusPath!.Split('/', StringSplitOptions.RemoveEmptyEntries);
		var tenantId = Guid.Parse(statusParts[^2]);
		var token = statusParts[^1];
		var otpResponse = await Client.PostAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/otp",
			null);
		otpResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await otpResponse.Content.ReadAsStringAsync());
		var otp = await GetOtpCodeAsync(tenantId, lead.Id);

		var verifyOtpResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/verify",
			new VerifyAdmissionApplicantStatusOtpDto { OtpCode = otp },
			JsonOptions);
		verifyOtpResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyOtpResponse.Content.ReadAsStringAsync());
		var status = await verifyOtpResponse.Content.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
		status.ShouldNotBeNull();
		status.AccessSessionToken.ShouldNotBeNullOrWhiteSpace();
		status.Registration.ShouldNotBeNull();
		status.Registration.Status.ShouldBe(AdmissionCrmRegistrationStatus.Opened);

		var saveResponse = await Client.PutAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration",
			new
			{
				accessSessionToken = status.AccessSessionToken,
				concurrencyStamp = status.Registration.ConcurrencyStamp,
				responses = new { parentOccupation = "Engineer" }
			},
			JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await saveResponse.Content.ReadAsStringAsync());
		var saved = await saveResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		saved.ShouldNotBeNull();
		saved.Status.ShouldBe(AdmissionCrmRegistrationStatus.PartiallyCompleted);
		saved.CompletionPercentage.ShouldBeGreaterThan(0);

		var staleSaveResponse = await Client.PutAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration",
			new
			{
				accessSessionToken = status.AccessSessionToken,
				concurrencyStamp = status.Registration.ConcurrencyStamp,
				responses = new { parentOccupation = "Stale overwrite" }
			},
			JsonOptions);
		staleSaveResponse.IsSuccessStatusCode.ShouldBeFalse();
		(await staleSaveResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RegistrationConcurrencyConflict");

		var incompleteSubmitResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/submit",
			new
			{
				accessSessionToken = status.AccessSessionToken,
				concurrencyStamp = saved.ConcurrencyStamp,
				responses = new { parentOccupation = "Engineer" }
			},
			JsonOptions);
		incompleteSubmitResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await incompleteSubmitResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:RequiredRegistrationFieldMissing");

		var submitResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/submit",
			new
			{
				accessSessionToken = status.AccessSessionToken,
				concurrencyStamp = saved.ConcurrencyStamp,
				responses = new { parentOccupation = "Engineer", declarationAccepted = true }
			},
			JsonOptions);
		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var submitted = await submitResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		submitted.ShouldNotBeNull();
		submitted.Status.ShouldBe(AdmissionCrmRegistrationStatus.Submitted);

		var correctionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/return-for-correction",
			new ReturnAdmissionCrmRegistrationForCorrectionDto
			{
				FieldKeys = ["parentOccupation"],
				Reason = "Please provide the full occupation title."
			},
			JsonOptions);
		correctionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await correctionResponse.Content.ReadAsStringAsync());
		var returnedForCorrection = await correctionResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		returnedForCorrection.ShouldNotBeNull();
		var correctionAvailabilityResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/documents/availability");
		var correctionAvailability = (await correctionAvailabilityResponse.Content
			.ReadFromJsonAsync<AdmissionCrmGeneratedDocumentAvailabilityDto[]>(JsonOptions))!;
		correctionAvailability.Single(item =>
			item.DocumentType == AdmissionCrmGeneratedDocumentType.CorrectionNotice)
			.IsAvailable.ShouldBeTrue();

		var resubmitResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/submit",
			new
			{
				accessSessionToken = status.AccessSessionToken,
				concurrencyStamp = returnedForCorrection.ConcurrencyStamp,
				responses = new { parentOccupation = "Senior Engineer", declarationAccepted = true }
			},
			JsonOptions);
		resubmitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resubmitResponse.Content.ReadAsStringAsync());
		var resubmitted = await resubmitResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		resubmitted.ShouldNotBeNull();
		resubmitted.Status.ShouldBe(AdmissionCrmRegistrationStatus.Resubmitted);

		using (Factory.FailNextAdmissionCrmTimelineSave(AdmissionCrmTimelineEventType.RegistrationVerified))
		{
			var failedVerification = await Client.PostAsync(
				$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/verify",
				null);
			failedVerification.StatusCode.ShouldBe(
				HttpStatusCode.Forbidden,
				await failedVerification.Content.ReadAsStringAsync());
		}
		var afterFailedVerificationResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}");
		var afterFailedVerification = (await afterFailedVerificationResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions))!;
		afterFailedVerification.Status.ShouldBe(AdmissionCrmRegistrationStatus.Resubmitted);

		var registrationVerifyResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/verify",
			null);
		registrationVerifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await registrationVerifyResponse.Content.ReadAsStringAsync());
		var verified = await registrationVerifyResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		verified.ShouldNotBeNull();
		verified.Status.ShouldBe(AdmissionCrmRegistrationStatus.Verified);

		var checklistCreateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/document-checklists",
			new AdmissionCrmDocumentChecklistTemplateUpsertDto
			{
				Name = "Primary documents " + unique,
				IsFallback = true,
				Requirements =
				[
					new AdmissionCrmDocumentRequirementDto
					{
						Key = "birth-certificate",
						Name = "Birth certificate",
						RequirementLevel = AdmissionCrmDocumentRequirementLevel.Required,
						BlocksAdmissionReadiness = true,
						RequiresOriginal = true,
						MaximumSizeMb = 2,
						AcceptedExtensions = ["pdf"],
						UploadInstructions = "Upload one clear PDF with all pages in order.",
						RejectionReasons =
						[
							"Document is unreadable.",
							"Details do not match the registration form."
						],
						DisplayOrder = 10
					}
				]
			},
			JsonOptions);
		checklistCreateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await checklistCreateResponse.Content.ReadAsStringAsync());
		var checklistTemplate = await checklistCreateResponse.Content.ReadFromJsonAsync<AdmissionCrmDocumentChecklistTemplateDto>(JsonOptions);
		checklistTemplate.ShouldNotBeNull();
		var checklistPublishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/document-checklists/{checklistTemplate.Id}/publish",
			null);
		checklistPublishResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await checklistPublishResponse.Content.ReadAsStringAsync());

		var assignChecklistResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/document-checklist",
			new AssignAdmissionCrmDocumentChecklistDto { ChecklistTemplateId = checklistTemplate.Id },
			JsonOptions);
		assignChecklistResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignChecklistResponse.Content.ReadAsStringAsync());
		var assignedChecklist = await assignChecklistResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentChecklistDto>(JsonOptions);
		assignedChecklist.ShouldNotBeNull();
		assignedChecklist.AdmissionReady.ShouldBeFalse();
		assignedChecklist.BlockingReasons.ShouldContain(x => x.Contains("Birth certificate"));
		var assignedBirthCertificate = assignedChecklist.Documents.Single(x =>
			x.RequirementKey == "birth-certificate");
		assignedBirthCertificate.UploadInstructions.ShouldBe(
			"Upload one clear PDF with all pages in order.");
		assignedBirthCertificate.RejectionReasons.ShouldContain("Document is unreadable.");

		var firstUpload = await UploadDocumentAsync(tenantId, token, status.AccessSessionToken!, "birth-certificate", "birth.pdf", "first-version");
		firstUpload.StatusCode.ShouldBe(HttpStatusCode.OK, await firstUpload.Content.ReadAsStringAsync());
		var uploadedChecklist = await firstUpload.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentChecklistDto>(JsonOptions);
		uploadedChecklist.ShouldNotBeNull();
		var firstDocument = uploadedChecklist.Documents.Single(x => x.RequirementKey == "birth-certificate");
		firstDocument.Status.ShouldBe(AdmissionCrmApplicantDocumentStatus.Uploaded);
		var downloadResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/documents/{firstDocument.Id}/download");
		downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await downloadResponse.Content.ReadAsStringAsync());
		downloadResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
		var downloadName = downloadResponse.Content.Headers.ContentDisposition?.FileNameStar ??
		                   downloadResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"');
		downloadName.ShouldBe("birth.pdf");
		(await downloadResponse.Content.ReadAsByteArrayAsync()).Take(5)
			.ShouldBe(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });

		using (var anonymousClient = CreateAnonymousClient())
		{
			var anonymousDownload = await anonymousClient.GetAsync(
				$"/api/student-enrollment/admissions-crm/documents/{firstDocument.Id}/download");
			anonymousDownload.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
		}

		var rejectResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/documents/{firstDocument.Id}/review",
			new ReviewAdmissionCrmApplicantDocumentDto { Approve = false, Reason = "The scan is not readable." },
			JsonOptions);
		rejectResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await rejectResponse.Content.ReadAsStringAsync());
		var rejected = await rejectResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentDto>(JsonOptions);
		rejected.ShouldNotBeNull();
		rejected.DecisionReason.ShouldBe("The scan is not readable.");

		var replacementUpload = await UploadDocumentFromStaffAsync(
			issued.Id,
			"birth-certificate",
			"birth.pdf",
			"clear-second-version");
		replacementUpload.StatusCode.ShouldBe(HttpStatusCode.OK, await replacementUpload.Content.ReadAsStringAsync());
		var replacementChecklist = await replacementUpload.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentChecklistDto>(JsonOptions);
		replacementChecklist.ShouldNotBeNull();
		var replacement = replacementChecklist.Documents.Single(x => x.RequirementKey == "birth-certificate");
		replacement.Version.ShouldBe(2);
		replacement.Id.ShouldNotBe(firstDocument.Id);

		var approveResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/documents/{replacement.Id}/review",
			new ReviewAdmissionCrmApplicantDocumentDto { Approve = true },
			JsonOptions);
		approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await approveResponse.Content.ReadAsStringAsync());

		var blockedReadinessResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/document-checklists/{assignedChecklist.AssignmentId}/confirm-readiness",
			new ConfirmAdmissionCrmReadinessDto(),
			JsonOptions);
		blockedReadinessResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var originalSeenResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/documents/{replacement.Id}/original-seen",
			null);
		originalSeenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await originalSeenResponse.Content.ReadAsStringAsync());

		var readinessResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/document-checklists/{assignedChecklist.AssignmentId}/confirm-readiness",
			new ConfirmAdmissionCrmReadinessDto(),
			JsonOptions);
		readinessResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await readinessResponse.Content.ReadAsStringAsync());
		var ready = await readinessResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentChecklistDto>(JsonOptions);
		ready.ShouldNotBeNull();
		ready.ReadinessConfirmed.ShouldBeTrue();
		ready.CompletionPercentage.ShouldBe(100);

		var queueSummaryResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/queues/summary");
		queueSummaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await queueSummaryResponse.Content.ReadAsStringAsync());
		var queueSummary = await queueSummaryResponse.Content.ReadFromJsonAsync<AdmissionCrmOperationsQueueSummaryDto>(JsonOptions);
		queueSummary.ShouldNotBeNull();
		queueSummary.Queues.Single(x => x.QueueType == AdmissionCrmOperationsQueueType.AdmissionReady).Count.ShouldBeGreaterThan(0);

		var admissionReadyQueueResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/queues/{(int)AdmissionCrmOperationsQueueType.AdmissionReady}?registrationStatus={(int)AdmissionCrmRegistrationStatus.AdmissionReady}&maxResultCount=50");
		admissionReadyQueueResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
			await admissionReadyQueueResponse.Content.ReadAsStringAsync());
		var admissionReadyQueue = await admissionReadyQueueResponse.Content
			.ReadFromJsonAsync<AdmissionCrmOperationsQueueDto>(JsonOptions);
		admissionReadyQueue.ShouldNotBeNull();
		var admissionReadyItem = admissionReadyQueue.Items.Single(x => x.LeadId == lead.Id);
		admissionReadyItem.Registration.ShouldNotBeNull();
		admissionReadyItem.Registration.Status.ShouldBe(AdmissionCrmRegistrationStatus.AdmissionReady);
		admissionReadyItem.Registration.FormNumber.ShouldBe(issued.FormNumber);
		admissionReadyItem.Registration.ReadinessConfirmed.ShouldBeTrue();

		var overviewResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/overview");
		overviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overviewResponse.Content.ReadAsStringAsync());
		var overview = await overviewResponse.Content.ReadFromJsonAsync<AdmissionCrmEnrollmentOverviewDto>(JsonOptions);
		overview.ShouldNotBeNull();
		overview.Queues.Single(x => x.QueueType == AdmissionCrmOperationsQueueType.AdmissionReady).Count
			.ShouldBeGreaterThan(0);
		overview.UrgentItems.Count.ShouldBeLessThanOrEqualTo(12);
		overview.UrgentItems.ShouldAllBe(x =>
			x.Item.LeadId != Guid.Empty &&
			(x.Workspace == "exams" ||
			 x.Workspace == "registrations" ||
			 x.Workspace == "offers" ||
			 x.Workspace == "activation" ||
			 x.Workspace == "operations"));

		var handoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{issued.Id}/admission-handoff");
		handoffResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await handoffResponse.Content.ReadAsStringAsync());
		var handoff = await handoffResponse.Content.ReadFromJsonAsync<AdmissionCrmAdmissionHandoffPackageDto>(JsonOptions);
		handoff.ShouldNotBeNull();
		handoff.FormNumber.ShouldBe(issued.FormNumber);
		handoff.RegistrationStatus.ShouldBe(AdmissionCrmRegistrationStatus.AdmissionReady);
		handoff.Documents.Single(x => x.RequirementKey == "birth-certificate").Status
			.ShouldBe(AdmissionCrmApplicantDocumentStatus.OriginalSeen);

		var analyticsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/analytics/summary");
		analyticsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await analyticsResponse.Content.ReadAsStringAsync());
		var analytics = await analyticsResponse.Content.ReadFromJsonAsync<AdmissionCrmAnalyticsSummaryDto>(JsonOptions);
		analytics.ShouldNotBeNull();
		analytics.RegistrationAnalytics.AdmissionReadyCount.ShouldBeGreaterThan(0);
		analytics.DocumentAnalytics.ReadinessConfirmedCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_And_Submit_Direct_Offline_Registration()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var templateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			CreateTemplateInput("Offline registration " + unique),
			JsonOptions);
		templateResponse.EnsureSuccessStatusCode();
		var template = await templateResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		(await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}/publish",
			null)).EnsureSuccessStatusCode();

		var seriesResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-number-series",
			new AdmissionCrmFormNumberSeriesUpsertDto
			{
				Name = "Offline series " + unique,
				Prefix = "OFF-" + unique.ToUpperInvariant() + "-",
				Padding = 4,
				NextNumber = 1,
				IsActive = true
			},
			JsonOptions);
		seriesResponse.EnsureSuccessStatusCode();
		var series = await seriesResponse.Content
			.ReadFromJsonAsync<AdmissionCrmFormNumberSeriesDto>(JsonOptions);
		series.ShouldNotBeNull();

		var directInput = new CreateDirectAdmissionCrmRegistrationDto
		{
			Applicant = new CreateAdmissionCrmLeadDto
			{
				FirstName = "Riya",
				ParentGuardianName = "Kavita Patel",
				PrimaryContactNo = "96" + Random.Shared.NextInt64(10000000, 99999999),
				AdmissionIntent = "Direct admission",
				TargetStandardId = Factory.SeededStandardId,
				AcademicBoardId = Factory.SeededBoardId,
				AcademicMediumId = Factory.SeededMediumId,
				DesiredClassName = "Grade 1"
			},
			Registration = new IssueAdmissionCrmRegistrationApplicationDto
			{
				RegistrationTemplateId = template.Id,
				FormNumberSeriesId = series.Id
			}
		};

		foreach (var permission in new[]
		         {
			         ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
			         ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm
		         })
		{
			using var restrictedClient = Factory.CreateClient();
			restrictedClient.DefaultRequestHeaders.Add(
				TestAuthHandler.TestTenantIdHeader,
				TestAuthHandler.TestTenantId);
			restrictedClient.DefaultRequestHeaders.Add(
				AlwaysGrantPermissionChecker.TestPermissionsHeader,
				permission);
			var forbiddenResponse = await restrictedClient.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/registrations/direct",
				directInput,
				JsonOptions);
			forbiddenResponse.StatusCode.ShouldBe(
				HttpStatusCode.Forbidden,
				await forbiddenResponse.Content.ReadAsStringAsync());
		}

		var directResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/registrations/direct",
			directInput,
			JsonOptions);
		directResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await directResponse.Content.ReadAsStringAsync());
		var direct = await directResponse.Content
			.ReadFromJsonAsync<AdmissionCrmDirectRegistrationResultDto>(JsonOptions);
		direct.ShouldNotBeNull();
		direct.Applicant.Stage.ShouldBe(AdmissionInquiryStage.Eligible);
		direct.Applicant.Channel.ShouldBe(AdmissionInquiryChannel.ManualBackOffice);
		direct.Applicant.IntendedRouteType.ShouldBe(AdmissionCrmRouteType.Fresh);
		direct.Applicant.IntendedRouteDefinitionId.ShouldNotBeNull();
		direct.Applicant.IntendedRouteNameSnapshot.ShouldBe("Fresh admission");
		direct.Registration.Status.ShouldBe(AdmissionCrmRegistrationStatus.Issued);
		direct.Registration.Provenance.ShouldBe(AdmissionCrmRegistrationProvenance.StaffAssistedCounter);
		direct.Registration.DataEntryState.ShouldBe(AdmissionCrmRegistrationDataEntryState.NotStarted);
		direct.Registration.Prefill["admissionRoute"].GetString()
			.ShouldBe(direct.Applicant.IntendedRouteNameSnapshot);
		direct.Registration.Prefill["admissionRouteDefinitionId"].GetGuid()
			.ShouldBe(direct.Applicant.IntendedRouteDefinitionId.Value);
		direct.Registration.Prefill["admissionRouteDefinitionVersion"].GetInt32()
			.ShouldBe(direct.Applicant.IntendedRouteDefinitionVersion.Value);

		var responses = new SaveAdmissionCrmRegistrationResponsesDto
		{
			ConcurrencyStamp = direct.Registration.ConcurrencyStamp,
			Responses = new Dictionary<string, JsonElement>
			{
				["parentOccupation"] = JsonSerializer.SerializeToElement("Teacher"),
				["declarationAccepted"] = JsonSerializer.SerializeToElement(true)
			}
		};
		var saveResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{direct.Registration.Id}/staff-responses",
			responses,
			JsonOptions);
		saveResponse.EnsureSuccessStatusCode();
		var saved = await saveResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		saved.ShouldNotBeNull();
		saved.Status.ShouldBe(AdmissionCrmRegistrationStatus.PartiallyCompleted);
		responses.ConcurrencyStamp = saved.ConcurrencyStamp;

		var submitPath = $"/api/student-enrollment/admissions-crm/registrations/{direct.Registration.Id}/staff-submit";
		var submitResponses = await Task.WhenAll(
			Client.PostAsJsonAsync(submitPath, responses, JsonOptions),
			Client.PostAsJsonAsync(submitPath, responses, JsonOptions));
		submitResponses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
		submitResponses.Count(response => !response.IsSuccessStatusCode).ShouldBe(1);
		var submitResponse = submitResponses.Single(response => response.StatusCode == HttpStatusCode.OK);
		var submitted = await submitResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		submitted.ShouldNotBeNull();
		submitted.Status.ShouldBe(AdmissionCrmRegistrationStatus.Submitted);
		submitted.DataEntryState.ShouldBe(AdmissionCrmRegistrationDataEntryState.Submitted);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Paper_Registration_With_Provenance_And_Timeline()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var desiredClassName = "Paper Grade " + unique;
		var templateInput = CreateTemplateInput("Paper registration " + unique);
		templateInput.IsFallback = false;
		templateInput.Applicability = new AdmissionCrmPlaybookApplicabilityDto
		{
			DesiredClass = desiredClassName
		};
		var templateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			templateInput,
			JsonOptions);
		templateResponse.EnsureSuccessStatusCode();
		var template = await templateResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		(await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}/publish",
			null)).EnsureSuccessStatusCode();

		var physicalFormNumber = "PAPER-" + unique.ToUpperInvariant();
		var paperInput = new CreatePaperAdmissionCrmRegistrationDto
		{
			Applicant = new CreateAdmissionCrmLeadDto
			{
				FirstName = "Kabir",
				ParentGuardianName = "Paper Parent",
				PrimaryContactNo = "95" + Random.Shared.NextInt64(10000000, 99999999),
				AdmissionIntent = "Paper registration",
				TargetStandardId = Factory.SeededStandardId,
				AcademicBoardId = Factory.SeededBoardId,
				AcademicMediumId = Factory.SeededMediumId,
				DesiredClassName = desiredClassName
			},
			Registration = new IssueAdmissionCrmRegistrationApplicationDto
			{
				RegistrationTemplateId = template.Id,
				ManualFormNumber = physicalFormNumber
			},
			PhysicalFormNumber = physicalFormNumber,
			PhysicalFormReceivedDate = DateTime.UtcNow.Date,
			ScannedFormAttachmentReference = "counter-scan-" + unique + ".pdf"
		};

		using var issueOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm);
		var deniedResponse = await issueOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/registrations/from-paper",
			paperInput,
			JsonOptions);
		deniedResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await deniedResponse.Content.ReadAsStringAsync());

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/registrations/from-paper",
			paperInput,
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content
			.ReadFromJsonAsync<AdmissionCrmDirectRegistrationResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.Applicant.Channel.ShouldBe(AdmissionInquiryChannel.ManualBackOffice);
		result.Applicant.Stage.ShouldBe(AdmissionInquiryStage.Eligible);
		result.Registration.Provenance.ShouldBe(AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper);
		result.Registration.DataEntryState.ShouldBe(AdmissionCrmRegistrationDataEntryState.InProgress);
		result.Registration.PhysicalFormNumber.ShouldBe(physicalFormNumber);
		result.Registration.PhysicalFormReceivedByUserId.ShouldNotBeNull();
		result.Registration.ScannedFormAttachmentReference.ShouldBe(paperInput.ScannedFormAttachmentReference);

		var queueResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/queues/{(int)AdmissionCrmOperationsQueueType.PendingPaperEntry}?maxResultCount=20");
		queueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await queueResponse.Content.ReadAsStringAsync());
		var paperQueue = await queueResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmOperationsQueueItemDto>>(JsonOptions);
		paperQueue.ShouldNotBeNull();
		var queueItem = paperQueue.Items.Single(x => x.RegistrationApplicationId == result.Registration.Id);
		queueItem.Title.ShouldBe("Enter paper registration responses");
		queueItem.Status.ShouldBe(AdmissionCrmRegistrationDataEntryState.InProgress.ToString());
		queueItem.Registration.ShouldNotBeNull();
		queueItem.Registration.Provenance.ShouldBe(AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper);
		queueItem.Registration.DataEntryState.ShouldBe(AdmissionCrmRegistrationDataEntryState.InProgress);
		queueItem.Registration.PhysicalFormNumber.ShouldBe(physicalFormNumber);

		var timelineResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{result.Applicant.Id}/timeline?maxResultCount=20");
		timelineResponse.EnsureSuccessStatusCode();
		var timeline = await timelineResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmTimelineEventDto>>(JsonOptions);
		timeline.ShouldNotBeNull();
		timeline.Items.ShouldContain(x => x.EventType == AdmissionCrmTimelineEventType.PaperRegistrationEntered);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Auto_Issue_Applicable_Registration_On_Configured_Stage()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var campus = "Auto campus " + unique;
		var templateInput = CreateTemplateInput("Auto issue registration " + unique);
		templateInput.IsFallback = false;
		templateInput.Applicability = new AdmissionCrmPlaybookApplicabilityDto { Campus = campus };
		templateInput.AutoIssueAtStage = AdmissionInquiryStage.ExamReady;
		var templateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			templateInput,
			JsonOptions);
		templateResponse.EnsureSuccessStatusCode();
		var template = await templateResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		(await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}/publish",
			null)).EnsureSuccessStatusCode();

		var seriesResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-number-series",
			new AdmissionCrmFormNumberSeriesUpsertDto
			{
				Name = "Auto series " + unique,
				Prefix = "AUTO-" + unique.ToUpperInvariant() + "-",
				Padding = 3,
				NextNumber = 1,
				Applicability = new AdmissionCrmPlaybookApplicabilityDto { Campus = campus },
				IsActive = true
			},
			JsonOptions);
		seriesResponse.EnsureSuccessStatusCode();

		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Grade 2 admission",
				DesiredClassName = "Grade 2",
				PrimaryContactNo = "97" + Random.Shared.NextInt64(10000000, 99999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				CampusBranchName = campus,
				FirstName = "Ishaan"
			},
			JsonOptions);
		leadResponse.EnsureSuccessStatusCode();
		var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		await SetLeadWorkflowStateAsync(
			lead.Id,
			AdmissionInquiryStage.FollowUp,
			Guid.NewGuid());
		var originalSequence = await UpdateCycleRegistrationSequenceAsync(
			registrationFormRequiredBeforeExam: true,
			entranceExamRequired: true);
		HttpResponseMessage stageResponse;
		try
		{
			stageResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
				new ChangeAdmissionCrmLeadStageDto
				{
					Stage = AdmissionInquiryStage.ExamReady,
					Reason = "Applicant confirmed interest and exam readiness."
				},
				JsonOptions);
		}
		finally
		{
			await UpdateCycleRegistrationSequenceAsync(
				originalSequence.RegistrationFormRequiredBeforeExam,
				originalSequence.EntranceExamRequired);
		}
		stageResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stageResponse.Content.ReadAsStringAsync());
		var updatedLead = await stageResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		updatedLead.ShouldNotBeNull();
		updatedLead.RegistrationFormIssuedAt.ShouldNotBeNull();
		updatedLead.RegistrationFormNumber.ShouldStartWith("AUTO-" + unique.ToUpperInvariant() + "-");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_List_Eligible_Lead_When_Registration_Invitation_Is_Pending()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Registration invitation " + unique,
				DesiredClassName = "Grade 3",
				PrimaryContactNo = "96" + Random.Shared.NextInt64(10000000, 99999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Pending",
				LastName = "Registration"
			},
			JsonOptions);
		leadResponse.EnsureSuccessStatusCode();
		var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();

		await SetLeadStageAsync(lead.Id, AdmissionInquiryStage.Eligible);

		var queueResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/queues/{(int)AdmissionCrmOperationsQueueType.RegistrationInvitationPending}?maxResultCount=50");
		queueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await queueResponse.Content.ReadAsStringAsync());
		var queue = await queueResponse.Content.ReadFromJsonAsync<AdmissionCrmOperationsQueueDto>(JsonOptions);
		queue.ShouldNotBeNull();
		var item = queue.Items.Single(x => x.LeadId == lead.Id);
		item.Status.ShouldBe("Ready to invite");
		item.RegistrationApplicationId.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Filter_Overview_And_Reject_Queue_Outside_Granted_Workspace()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",",
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks));

		var overviewResponse = await client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/overview");
		overviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overviewResponse.Content.ReadAsStringAsync());
		var overview = await overviewResponse.Content.ReadFromJsonAsync<AdmissionCrmEnrollmentOverviewDto>(JsonOptions);
		overview.ShouldNotBeNull();
		overview.Queues.ShouldContain(x => x.QueueType == AdmissionCrmOperationsQueueType.IncompleteRegistration);
		overview.Queues.ShouldNotContain(x => x.QueueType == AdmissionCrmOperationsQueueType.FinalApproval);
		overview.Queues.ShouldNotContain(x => x.QueueType == AdmissionCrmOperationsQueueType.ActivationRepair);
		overview.Queues.ShouldNotContain(x => x.QueueType == AdmissionCrmOperationsQueueType.UnassignedExam);
		overview.Queues.ShouldNotContain(x => x.QueueType == AdmissionCrmOperationsQueueType.HouseholdResolution);
		overview.UpcomingExamCount.ShouldBe(0);

		var forbiddenQueueResponse = await client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operations/queues/{(int)AdmissionCrmOperationsQueueType.FinalApproval}?maxResultCount=50");
		forbiddenQueueResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
			await forbiddenQueueResponse.Content.ReadAsStringAsync());
	}

	private static CreateAdmissionCrmRegistrationTemplateDto CreateTemplateInput(string name) => new()
	{
		Name = name,
		IsFallback = true,
		AllowPartialSave = true,
		LinkExpiryDays = 14,
		FeeRequired = true,
		FeeAmount = 250,
		Sections =
		[
			new AdmissionCrmRegistrationSectionDto
			{
				Key = "family",
				Title = "Family and declaration",
				DisplayOrder = 10,
				Fields =
				[
					new AdmissionCrmRegistrationFieldDto
					{
						Key = "studentFirstName",
						Label = "Student first name",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "firstName",
						Required = true,
						ParentEditable = false,
						DisplayOrder = 10
					},
					new AdmissionCrmRegistrationFieldDto
					{
						Key = "parentOccupation",
						Label = "Parent occupation",
						FieldType = AdmissionCrmFormFieldType.Text,
						Required = true,
						ParentEditable = true,
						MinimumLength = 2,
						DisplayOrder = 20
					},
					new AdmissionCrmRegistrationFieldDto
					{
						Key = "declarationAccepted",
						Label = "Declaration accepted",
						FieldType = AdmissionCrmFormFieldType.Checkbox,
						Required = true,
						ParentEditable = true,
						DisplayOrder = 30
					}
				]
			}
		]
	};

	private HttpClient CreateClientWithCrmPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			TestAuthHandler.TestTenantIdHeader,
			TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}

	private async Task<PublishedRegistrationResources> CreatePublishedRegistrationResourcesAsync(
		string name)
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var templateInput = CreateTemplateInput($"{name} {unique}");
		templateInput.IsFallback = false;
		templateInput.Applicability = new AdmissionCrmPlaybookApplicabilityDto
		{
			Campus = $"registration-gate-{unique}"
		};
		var templateResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/registration-templates",
			templateInput,
			JsonOptions);
		templateResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await templateResponse.Content.ReadAsStringAsync());
		var template = await templateResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRegistrationTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		var publishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/configuration/registration-templates/{template.Id}/publish",
			null);
		publishResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await publishResponse.Content.ReadAsStringAsync());

		var seriesResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-number-series",
			new AdmissionCrmFormNumberSeriesUpsertDto
			{
				Name = $"{name} series {unique}",
				Prefix = $"G-{unique.ToUpperInvariant()}-",
				Padding = 4,
				NextNumber = 1,
				IsActive = true
			},
			JsonOptions);
		seriesResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await seriesResponse.Content.ReadAsStringAsync());
		var series = await seriesResponse.Content
			.ReadFromJsonAsync<AdmissionCrmFormNumberSeriesDto>(JsonOptions);
		series.ShouldNotBeNull();

		return new PublishedRegistrationResources(template.Id, series.Id);
	}

	private async Task<AdmissionCrmLeadDto> CreateRegistrationLeadAsync(string suffix)
	{
		var sourceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/catalog/inquiry-sources",
			new AdmissionCrmInquirySourceUpsertDto
			{
				Code = "registration-" + Guid.NewGuid().ToString("N"),
				DisplayName = "Registration source " + suffix,
				AllowedChannels = [AdmissionInquiryChannel.SchoolWebsiteForm]
			},
			JsonOptions);
		sourceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await sourceResponse.Content.ReadAsStringAsync());
		var inquirySource = await sourceResponse.Content
			.ReadFromJsonAsync<AdmissionCrmInquirySourceDto>(JsonOptions);
		inquirySource.ShouldNotBeNull();
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				InquirySourceId = inquirySource.Id,
				AdmissionIntent = "Registration gate validation",
				PrimaryContactNo = "95" + Random.Shared.NextInt64(10000000, 99999999),
				Email = $"{suffix}.{Guid.NewGuid():N}@example.test",
				Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
				FirstName = suffix,
				ParentGuardianName = suffix + " Guardian",
				TargetStandardId = Factory.SeededStandardId,
				DesiredClassName = "Grade 4"
			},
			JsonOptions);
		response.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}

	private Task<HttpResponseMessage> IssueRegistrationAsync(
		Guid leadId,
		PublishedRegistrationResources resources,
		Guid playbookId)
	{
		return Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/registrations/issue",
			new IssueAdmissionCrmRegistrationApplicationDto
			{
				RegistrationTemplateId = resources.TemplateId,
				FormNumberSeriesId = resources.SeriesId,
				PlaybookId = playbookId
			},
			JsonOptions);
	}

	private async Task<Guid> CreatePublishedRegistrationPlaybookAsync(
		AdmissionCrmRegistrationPath registrationPath)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmPlaybook, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var unique = Guid.NewGuid().ToString("N");
			var playbook = new AdmissionCrmPlaybook(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededAcademicYearId,
				"Registration gate " + unique[..8],
				1,
				false,
				registrationPath,
				$"{{\"campus\":\"gate-{unique}\"}}",
				"[]",
				"[]",
				"[]",
				"[]",
				"[]",
				"[]",
				"Registration path API test.");
			playbook.Publish(DateTime.UtcNow);
			await repository.InsertAsync(playbook);
			await uow.CompleteAsync();
			return playbook.Id;
		}
	}

	private async Task SetLeadWorkflowStateAsync(
		Guid leadId,
		AdmissionInquiryStage stage,
		Guid? ownerUserId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await repository.GetAsync(leadId);
			lead.AssignOwner(ownerUserId);
			lead.ChangeStage(stage);
			await repository.UpdateAsync(lead);
			await uow.CompleteAsync();
		}
	}

	private async Task<CycleRegistrationSequence> UpdateCycleRegistrationSequenceAsync(
		bool registrationFormRequiredBeforeExam,
		bool entranceExamRequired)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmCycleConfiguration, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await repository.GetQueryableAsync();
			var configuration = queryable.FirstOrDefault(x =>
				x.AcademicYearId == Factory.SeededAcademicYearId);
			if (configuration == null)
			{
				configuration = new AdmissionCrmCycleConfiguration(
					Guid.NewGuid(),
					Guid.Parse(TestAuthHandler.TestTenantId),
					Factory.SeededAcademicYearId,
					"Registration gate test cycle");
				await repository.InsertAsync(configuration);
			}
			var original = new CycleRegistrationSequence(
				configuration.RegistrationFormRequiredBeforeExam,
				configuration.EntranceExamRequired);
			configuration.Update(
				configuration.CycleName,
				configuration.IsActive,
				registrationFormRequiredBeforeExam,
				entranceExamRequired,
				configuration.StaleAfterDays,
				configuration.ArchiveAfterDays,
				configuration.DefaultLeadSlaHours,
				configuration.ActivationCancellationWindowDays,
				configuration.DefaultExamPassingPercentage,
				configuration.MeritRankingMode,
				configuration.ExamReadinessFields,
				configuration.LeadScoringRulesJson,
				configuration.LeadRoutingRulesJson,
				configuration.JourneyRulesJson,
				configuration.ApplicantChecklistJson,
				configuration.ChannelSettingsJson,
				configuration.SlaPolicyJson,
				configuration.Notes);
			await repository.UpdateAsync(configuration);
			await uow.CompleteAsync();
			return original;
		}
	}

	private async Task AddEligibleExamResultAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var slotRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmExamSlot, Guid>>();
		var examRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmEntranceExamRecord, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var now = DateTimeOffset.UtcNow;
			var slot = new AdmissionCrmExamSlot(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededAcademicYearId,
				null,
				null,
				null,
				"Registration gate exam",
				null,
				null,
				now.AddDays(-2),
				now.AddDays(-2).AddHours(2),
				20,
				1);
			await slotRepository.InsertAsync(slot);
			var exam = new AdmissionCrmEntranceExamRecord(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededAcademicYearId,
				leadId,
				slot,
				false,
				null,
				now.AddDays(-3).UtcDateTime);
			exam.RecordResult(100, 40, 75, null, now.AddDays(-2).UtcDateTime);
			await examRepository.InsertAsync(exam);
			await uow.CompleteAsync();
		}
	}

	private async Task AddScheduledEligibilityOverrideAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var slotRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmExamSlot, Guid>>();
		var examRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmEntranceExamRecord, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var now = DateTimeOffset.UtcNow;
			var slot = new AdmissionCrmExamSlot(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				null, null, null, "Registration override exam", null, null, now.AddDays(2),
				now.AddDays(2).AddHours(2), 20, 1);
			await slotRepository.InsertAsync(slot);
			var exam = new AdmissionCrmEntranceExamRecord(Guid.NewGuid(), tenantId,
				Factory.SeededAcademicYearId, leadId, slot, false, null, now.UtcDateTime);
			exam.SetEligibilityDecision(AdmissionCrmEligibilityDecision.Eligible,
				"Authorized admission eligibility override.", true,
				"Principal approved a documented entrance-exam exception.", null, now.UtcDateTime);
			await examRepository.InsertAsync(exam);
			await uow.CompleteAsync();
		}
	}

	private sealed record PublishedRegistrationResources(Guid TemplateId, Guid SeriesId);
	private sealed record CycleRegistrationSequence(
		bool RegistrationFormRequiredBeforeExam,
		bool EntranceExamRequired);

	private async Task SetLeadStageAsync(Guid leadId, AdmissionInquiryStage stage)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await repository.GetAsync(leadId);
			lead.ChangeStage(stage);
			await repository.UpdateAsync(lead);
			await uow.CompleteAsync();
		}
	}

	private async Task<string> GetOtpCodeAsync(Guid tenantId, Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var encryptionService = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await repository.GetQueryableAsync();
			var access = queryable.Single(x => x.LeadId == leadId);
			access.EncryptedOtpCode.ShouldNotBeNullOrWhiteSpace();
			await uow.CompleteAsync();
			return encryptionService.Decrypt(access.EncryptedOtpCode!)!;
		}
	}

	private async Task<HttpResponseMessage> UploadDocumentAsync(
		Guid tenantId,
		string token,
		string accessSessionToken,
		string requirementKey,
		string fileName,
		string content)
	{
		using var form = new MultipartFormDataContent();
		form.Add(new StringContent(accessSessionToken), "AccessSessionToken");
		form.Add(new StringContent(requirementKey), "RequirementKey");
		var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\n" + content));
		file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
		form.Add(file, "File", fileName);
		return await Client.PostAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/documents",
			form);
	}

	private async Task<HttpResponseMessage> UploadDocumentFromStaffAsync(
		Guid registrationApplicationId,
		string requirementKey,
		string fileName,
		string content)
	{
		using var form = new MultipartFormDataContent();
		form.Add(new StringContent(requirementKey), "RequirementKey");
		var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\n" + content));
		file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
		form.Add(file, "File", fileName);
		return await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/registrations/{registrationApplicationId}/documents",
			form);
	}
}
