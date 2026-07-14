using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
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
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Complete_Secure_Versioned_Registration_Workflow()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var templateInput = CreateTemplateInput("Primary registration " + unique);
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
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Aarav"
			},
			JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();

		var issueResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/issue",
			new IssueAdmissionCrmRegistrationApplicationDto
			{
				RegistrationTemplateId = published.Id,
				FormNumberSeriesId = series.Id
			},
			JsonOptions);
		issueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await issueResponse.Content.ReadAsStringAsync());
		var issued = await issueResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		issued.ShouldNotBeNull();
		issued.FormNumber.ShouldBe(series.NextPreview);
		issued.RegistrationTemplateVersion.ShouldBe(1);
		issued.ApplicantStatusPath.ShouldNotBeNullOrWhiteSpace();

		var currentRegistrationResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registrations/current");
		currentRegistrationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await currentRegistrationResponse.Content.ReadAsStringAsync());
		var currentRegistration = await currentRegistrationResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		currentRegistration.ShouldNotBeNull();
		currentRegistration.Id.ShouldBe(issued.Id);

		var statusParts = issued.ApplicantStatusPath!.Split('/', StringSplitOptions.RemoveEmptyEntries);
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
				responses = new { parentOccupation = "Engineer" }
			},
			JsonOptions);
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await saveResponse.Content.ReadAsStringAsync());
		var saved = await saveResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		saved.ShouldNotBeNull();
		saved.Status.ShouldBe(AdmissionCrmRegistrationStatus.PartiallyCompleted);
		saved.CompletionPercentage.ShouldBeGreaterThan(0);

		var incompleteSubmitResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/submit",
			new
			{
				accessSessionToken = status.AccessSessionToken,
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

		var resubmitResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/submit",
			new
			{
				accessSessionToken = status.AccessSessionToken,
				responses = new { parentOccupation = "Senior Engineer", declarationAccepted = true }
			},
			JsonOptions);
		resubmitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resubmitResponse.Content.ReadAsStringAsync());
		var resubmitted = await resubmitResponse.Content.ReadFromJsonAsync<AdmissionCrmRegistrationApplicationDto>(JsonOptions);
		resubmitted.ShouldNotBeNull();
		resubmitted.Status.ShouldBe(AdmissionCrmRegistrationStatus.Resubmitted);

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

		var firstUpload = await UploadDocumentAsync(tenantId, token, status.AccessSessionToken!, "birth-certificate", "birth.pdf", "first-version");
		firstUpload.StatusCode.ShouldBe(HttpStatusCode.OK, await firstUpload.Content.ReadAsStringAsync());
		var uploadedChecklist = await firstUpload.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentChecklistDto>(JsonOptions);
		uploadedChecklist.ShouldNotBeNull();
		var firstDocument = uploadedChecklist.Documents.Single(x => x.RequirementKey == "birth-certificate");
		firstDocument.Status.ShouldBe(AdmissionCrmApplicantDocumentStatus.Uploaded);

		var rejectResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/documents/{firstDocument.Id}/review",
			new ReviewAdmissionCrmApplicantDocumentDto { Approve = false, Reason = "The scan is not readable." },
			JsonOptions);
		rejectResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await rejectResponse.Content.ReadAsStringAsync());
		var rejected = await rejectResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantDocumentDto>(JsonOptions);
		rejected.ShouldNotBeNull();
		rejected.DecisionReason.ShouldBe("The scan is not readable.");

		var replacementUpload = await UploadDocumentAsync(tenantId, token, status.AccessSessionToken!, "birth-certificate", "birth.pdf", "clear-second-version");
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

		var stageResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
			new ChangeAdmissionCrmLeadStageDto
			{
				Stage = AdmissionInquiryStage.ExamReady,
				Reason = "Applicant confirmed interest and exam readiness."
			},
			JsonOptions);
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
		var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
		file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
		form.Add(file, "File", fileName);
		return await Client.PostAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/registration/documents",
			form);
	}
}
