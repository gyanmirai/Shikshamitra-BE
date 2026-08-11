using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
public class AdmissionCrmConfigurationApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Public_Form_Template_With_Consent_And_Fields()
	{
		var input = new CreateAdmissionCrmFormTemplateDto
		{
			TemplateName = "Public website inquiry",
			Description = "Website form for general admission inquiries",
			Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
			IsPublic = true,
			ConsentText = "I agree that the school may contact me about this admission inquiry.",
			PrivacyNoticeUrl = "https://school.example/privacy",
			Fields =
			[
				new AdmissionCrmFormFieldDto
				{
					Key = "parentPhone",
					Label = "Parent phone",
					FieldType = AdmissionCrmFormFieldType.Phone,
					SystemField = "PrimaryContactNo",
					SectionName = "Parent details",
					HelperText = "Use the primary admission follow-up number.",
					Placeholder = "9876543210",
					LocalizedLabels = new Dictionary<string, string> { ["gu"] = "માતાપિતાનો ફોન", ["hi"] = "अभिभावक का फ़ोन" },
					Required = true,
					DisplayOrder = 10
				},
				new AdmissionCrmFormFieldDto
				{
					Key = "targetClass",
					Label = "Target class",
					FieldType = AdmissionCrmFormFieldType.Select,
					SystemField = "TargetClass",
					DefaultValue = "Grade 1",
					Required = true,
					DisplayOrder = 20,
					Options = ["LKG", "UKG", "Grade 1"],
					LocalizedOptions = new Dictionary<string, IReadOnlyList<string>>
					{
						["gu"] = ["Balvatika 1", "Balvatika 2", "Dhoran 1"]
					}
				}
			],
			Localizations = new Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>
			{
				["gu"] = new() { TemplateName = "પ્રવેશ પૂછપરછ", ConsentText = "શાળા પ્રવેશ અંગે સંપર્ક કરી શકે તે માટે હું સંમત છું." },
				["hi"] = new() { TemplateName = "प्रवेश पूछताछ", ConsentText = "मैं सहमत हूँ कि विद्यालय प्रवेश के बारे में संपर्क कर सकता है।" }
			}
		};

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-templates",
			input,
			JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var template = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmFormTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		template.TemplateName.ShouldBe(input.TemplateName);
		template.IsPublic.ShouldBeTrue();
		template.IsActive.ShouldBeTrue();
		template.ConsentText.ShouldBe(input.ConsentText);
		template.Fields.Count.ShouldBe(2);
		template.Fields[0].SystemField.ShouldBe("PrimaryContactNo");
		template.Fields[0].SectionName.ShouldBe("Parent details");
		template.Fields[0].HelperText.ShouldBe("Use the primary admission follow-up number.");
		template.Fields[0].Placeholder.ShouldBe("9876543210");
		template.Fields[1].Options.ShouldBe(["LKG", "UKG", "Grade 1"]);
		template.Fields[1].DefaultValue.ShouldBe("Grade 1");
		template.Localizations["gu"].TemplateName.ShouldBe("પ્રવેશ પૂછપરછ");

		var gujaratiResponse = await Client.GetAsync(
			$"/api/public/student-enrollment/admissions-crm/{Guid.Parse(TestAuthHandler.TestTenantId)}/forms/{template.Id}?language=gu");
		gujaratiResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await gujaratiResponse.Content.ReadAsStringAsync());
		var gujarati = (await gujaratiResponse.Content.ReadFromJsonAsync<PublicAdmissionInquiryFormDto>(JsonOptions))!;
		gujarati.TemplateName.ShouldBe("પ્રવેશ પૂછપરછ");
		gujarati.ConsentText.ShouldContain("સંમત");
		gujarati.Fields[0].Label.ShouldBe("માતાપિતાનો ફોન");

		gujarati.Fields[1].Options.ShouldBe(["LKG", "UKG", "Grade 1"]);
		gujarati.Fields[1].OptionLabels.ShouldBe(["Balvatika 1", "Balvatika 2", "Dhoran 1"]);

		var hindiResponse = await Client.GetAsync(
			$"/api/public/student-enrollment/admissions-crm/{Guid.Parse(TestAuthHandler.TestTenantId)}/forms/{template.Id}?language=hi-IN");
		hindiResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await hindiResponse.Content.ReadAsStringAsync());
		var hindi = (await hindiResponse.Content.ReadFromJsonAsync<PublicAdmissionInquiryFormDto>(JsonOptions))!;
		hindi.TemplateName.ShouldBe("प्रवेश पूछताछ");
		hindi.ConsentText.ShouldContain("सहमत");
		hindi.Fields[0].Label.ShouldBe("अभिभावक का फ़ोन");
		hindi.Fields[1].Options.ShouldBe(["LKG", "UKG", "Grade 1"]);
		hindi.Fields[1].OptionLabels.ShouldBe(["LKG", "UKG", "Grade 1"]);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-templates?filterText=website");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var templates = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFormTemplateDto>>(JsonOptions);
		templates.ShouldNotBeNull();
		templates.Items.Select(x => x.Id).ShouldContain(template.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Inactive_Public_Form_Template_Draft_Atomically()
	{
		var templateName = "Inactive public inquiry " + Guid.NewGuid().ToString("N")[..8];
		var input = new CreateAdmissionCrmFormTemplateDto
		{
			TemplateName = templateName,
			Description = "Inactive duplicate draft for review",
			Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
			IsPublic = true,
			IsActive = false,
			ConsentText = "I agree that the school may contact me about this admission inquiry.",
			PrivacyNoticeUrl = "https://school.example/privacy",
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

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-templates",
			input,
			JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var template = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmFormTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.TemplateName.ShouldBe(templateName);
		template.IsPublic.ShouldBeTrue();
		template.IsActive.ShouldBeFalse();

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-templates?filterText={Uri.EscapeDataString(templateName)}&isActive=false");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var templates = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFormTemplateDto>>(JsonOptions);
		templates.ShouldNotBeNull();
		templates.Items.ShouldContain(item => item.Id == template.Id && !item.IsActive);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Update_And_Approve_Message_Template()
	{
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates",
			new CreateAdmissionCrmMessageTemplateDto
			{
				TemplateName = "Entrance exam reminder " + Guid.NewGuid().ToString("N")[..8],
				Channel = AdmissionCrmMessageChannel.WhatsApp,
				Purpose = "Exam reminder",
				Body = "Dear {{parentName}}, {{studentName}} has an entrance exam on {{examDate}}.",
				Variables = ["parentName", "studentName", "examDate"]
			},
			JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplateDto>(JsonOptions);
		created.ShouldNotBeNull();
		created.Status.ShouldBe(AdmissionCrmMessageTemplateStatus.Draft);
		created.IsActive.ShouldBeTrue();
		created.Variables.ShouldContain("examDate");

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates?channel={(int) AdmissionCrmMessageChannel.WhatsApp}&filterText=Entrance");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var list = await listResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageTemplateDto>>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.Select(x => x.Id).ShouldContain(created.Id);

		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/message-templates/{created.Id}",
			new UpdateAdmissionCrmMessageTemplateDto
			{
				TemplateName = created.TemplateName,
				Channel = AdmissionCrmMessageChannel.WhatsApp,
				Purpose = "Exam reminder",
				Body = "Reminder: {{studentName}} should report by {{reportingTime}}.",
				Variables = ["studentName", "reportingTime"]
			},
			JsonOptions);
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
		var updated = await updateResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplateDto>(JsonOptions);
		updated.ShouldNotBeNull();
		updated.Body.ShouldContain("reportingTime");
		updated.Variables.ShouldContain("reportingTime");

		var statusResponse = await Client.PatchAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/message-templates/{created.Id}/status",
			new UpdateAdmissionCrmMessageTemplateStatusDto
			{
				Status = AdmissionCrmMessageTemplateStatus.Approved,
				IsActive = true
			},
			JsonOptions);
		statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await statusResponse.Content.ReadAsStringAsync());
		var approved = await statusResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplateDto>(JsonOptions);
		approved.ShouldNotBeNull();
		approved.Status.ShouldBe(AdmissionCrmMessageTemplateStatus.Approved);
		approved.IsActive.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Protect_Approved_Message_Template_Used_By_Enabled_Automation()
	{
		var template = await CreateApprovedCrmMessageTemplateAsync();
		var originalCycle = await GetCrmCycleConfigurationAsync();

		try
		{
			var journeyRules = originalCycle.JourneyRules.ToList();
			journeyRules.Add(new AdmissionCrmJourneyRuleDto
			{
				RuleName = "Template retirement guard " + Guid.NewGuid().ToString("N")[..8],
				IsEnabled = true,
				Priority = 10,
				Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
				SendMessage = true,
				MessageTemplateId = template.Id
			});
			await SetCrmAutomationReferencesAsync(
				journeyRules,
				originalCycle.ChannelSettings);

			var retireResponse = await Client.PatchAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/configuration/message-templates/{template.Id}/status",
				new UpdateAdmissionCrmMessageTemplateStatusDto
				{
					Status = AdmissionCrmMessageTemplateStatus.Retired,
					IsActive = false
				},
				JsonOptions);
			retireResponse.StatusCode.ShouldBe(
				HttpStatusCode.BadRequest,
				await retireResponse.Content.ReadAsStringAsync());
			(await retireResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:MessageTemplateInUse");

			await SetCrmAutomationReferencesAsync(
				originalCycle.JourneyRules,
				originalCycle.ChannelSettings);
			var channelSettings = originalCycle.ChannelSettings
				.Select(setting => new AdmissionCrmChannelSettingDto
				{
					Channel = setting.Channel,
					IsEnabled = setting.IsEnabled ||
						setting.Channel == AdmissionInquiryChannel.WhatsAppInquiry,
					DefaultInquirySourceId = setting.DefaultInquirySourceId,
					DefaultFormTemplateId = setting.DefaultFormTemplateId,
					DefaultOwnerUserId = setting.DefaultOwnerUserId,
					DefaultAcknowledgementTemplateId =
						setting.Channel == AdmissionInquiryChannel.WhatsAppInquiry
							? template.Id
							: setting.DefaultAcknowledgementTemplateId,
					DisplayOrder = setting.DisplayOrder
				})
				.ToList();
			await SetCrmAutomationReferencesAsync(
				originalCycle.JourneyRules,
				channelSettings);

			var deactivateResponse = await Client.PatchAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/configuration/message-templates/{template.Id}/status",
				new UpdateAdmissionCrmMessageTemplateStatusDto
				{
					Status = AdmissionCrmMessageTemplateStatus.Approved,
					IsActive = false
				},
				JsonOptions);
			deactivateResponse.StatusCode.ShouldBe(
				HttpStatusCode.BadRequest,
				await deactivateResponse.Content.ReadAsStringAsync());
			(await deactivateResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:MessageTemplateInUse");
		}
		finally
		{
			await SetCrmAutomationReferencesAsync(
				originalCycle.JourneyRules,
				originalCycle.ChannelSettings);
		}

		var unreferencedRetireResponse = await Client.PatchAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/message-templates/{template.Id}/status",
			new UpdateAdmissionCrmMessageTemplateStatusDto
			{
				Status = AdmissionCrmMessageTemplateStatus.Retired,
				IsActive = false
			},
			JsonOptions);
		unreferencedRetireResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await unreferencedRetireResponse.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Email_Message_Template_Without_Subject()
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates",
			new CreateAdmissionCrmMessageTemplateDto
			{
				TemplateName = "Email without subject " + Guid.NewGuid().ToString("N")[..8],
				Channel = AdmissionCrmMessageChannel.Email,
				Purpose = "Exam reminder",
				Body = "Dear {{parentName}}, your exam is scheduled.",
				Variables = ["parentName"]
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Preview_Draft_Message_Template_Without_Sending()
	{
		var lead = await CreateCrmLeadAsync("Message preview inquiry for Grade 4");
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates",
			new CreateAdmissionCrmMessageTemplateDto
			{
				TemplateName = "Preview template " + Guid.NewGuid().ToString("N")[..8],
				Channel = AdmissionCrmMessageChannel.WhatsApp,
				Purpose = "Preview before approval",
				Body = "Hello {{studentName}} from {{campusBranch}}, language {{preferredLanguage}}, exam on {{examDate}}. {{unknownVariable}}",
				Variables = ["studentName", "campusBranch", "preferredLanguage", "examDate"]
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var template = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.Status.ShouldBe(AdmissionCrmMessageTemplateStatus.Draft);

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/message-templates/preview",
			new PreviewAdmissionCrmMessageTemplateDto
			{
				TemplateId = template.Id,
				LeadId = lead.Id,
				Variables = new Dictionary<string, string>
				{
					["examDate"] = "15 July"
				}
			},
			JsonOptions);

		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplatePreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.TemplateId.ShouldBe(template.Id);
		preview.LeadId.ShouldBe(lead.Id);
		preview.RenderedBody.ShouldContain("Aditi");
		preview.RenderedBody.ShouldContain("Main Campus");
		preview.RenderedBody.ShouldContain("English");
		preview.RenderedBody.ShouldContain("15 July");
		preview.UnresolvedVariables.ShouldContain("unknownVariable");

		var logsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?templateId={template.Id}");
		logsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await logsResponse.Content.ReadAsStringAsync());
		var logs = await logsResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>>(JsonOptions);
		logs.ShouldNotBeNull();
		logs.TotalCount.ShouldBe(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Configure_Provider_Respect_OptOut_And_Record_Message_Delivery_Log()
	{
		var lead = await CreateCrmLeadAsync("Communication inquiry for Grade 4");
		var template = await CreateApprovedCrmMessageTemplateAsync();

		var providerListResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers");
		providerListResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await providerListResponse.Content.ReadAsStringAsync());
		var providers = await providerListResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmCommunicationProviderDto>>(JsonOptions);
		providers.ShouldNotBeNull();
		providers.Items.Count.ShouldBeGreaterThanOrEqualTo(3);
		providers.Items.Any(x => x.Channel == AdmissionCrmMessageChannel.WhatsApp).ShouldBeTrue();

		var providerMissingResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/messages/send-template",
			new SendAdmissionCrmMessageTemplateDto
			{
				TemplateId = template.Id,
				Variables = new Dictionary<string, string>
				{
					["examDate"] = "15 July"
				}
			},
			JsonOptions);
		providerMissingResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await providerMissingResponse.Content.ReadAsStringAsync());
		var providerMissingLog = await providerMissingResponse.Content
			.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		providerMissingLog.ShouldNotBeNull();
		providerMissingLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus);
		providerMissingLog.AttemptCount.ShouldBe(0);
		providerMissingLog.BodySnapshot.ShouldContain("15 July");

		var updateProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "WhatsApp Sandbox",
				SenderId = "school-main",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto()
			},
			JsonOptions);
		updateProviderResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateProviderResponse.Content.ReadAsStringAsync());
		var provider = await updateProviderResponse.Content
			.ReadFromJsonAsync<AdmissionCrmCommunicationProviderDto>(JsonOptions);
		provider.ShouldNotBeNull();
		provider.Status.ShouldBe(AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus);
		provider.IsEnabled.ShouldBeTrue();

		var optOutResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/opt-outs",
			new CreateAdmissionCrmCommunicationOptOutDto
			{
				LeadId = lead.Id,
				Channel = AdmissionCrmMessageChannel.WhatsApp,
				ContactValue = lead.PrimaryContactNo!,
				Reason = "Parent asked not to receive WhatsApp messages."
			},
			JsonOptions);
		optOutResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optOutResponse.Content.ReadAsStringAsync());
		var optOut = await optOutResponse.Content
			.ReadFromJsonAsync<AdmissionCrmCommunicationOptOutDto>(JsonOptions);
		optOut.ShouldNotBeNull();
		optOut.IsActive.ShouldBeTrue();

		var activeOptOutsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/opt-outs?leadId={lead.Id}&channel={(int) AdmissionCrmMessageChannel.WhatsApp}&isActive=true&contactSearch={lead.PrimaryContactNo}");
		activeOptOutsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await activeOptOutsResponse.Content.ReadAsStringAsync());
		var activeOptOuts = await activeOptOutsResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmCommunicationOptOutDto>>(JsonOptions);
		activeOptOuts.ShouldNotBeNull();
		activeOptOuts.TotalCount.ShouldBe(1);
		activeOptOuts.Items.Single().Id.ShouldBe(optOut.Id);

		var blockedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/messages/send-template",
			new SendAdmissionCrmMessageTemplateDto { TemplateId = template.Id },
			JsonOptions);
		blockedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await blockedResponse.Content.ReadAsStringAsync());
		var blockedLog = await blockedResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		blockedLog.ShouldNotBeNull();
		blockedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus);
		blockedLog.AttemptCount.ShouldBe(0);

		var clearResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/opt-outs/clear",
			new ClearAdmissionCrmCommunicationOptOutDto
			{
				LeadId = lead.Id,
				Channel = AdmissionCrmMessageChannel.WhatsApp,
				ContactValue = lead.PrimaryContactNo!,
				Reason = "Parent opted back in at reception."
			},
			JsonOptions);
		clearResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await clearResponse.Content.ReadAsStringAsync());
		var cleared = await clearResponse.Content.ReadFromJsonAsync<AdmissionCrmCommunicationOptOutDto>(JsonOptions);
		cleared.ShouldNotBeNull();
		cleared.IsActive.ShouldBeFalse();

		var inactiveOptOutsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/opt-outs?channel={(int) AdmissionCrmMessageChannel.WhatsApp}&isActive=false&contactSearch={lead.PrimaryContactNo}");
		inactiveOptOutsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await inactiveOptOutsResponse.Content.ReadAsStringAsync());
		var inactiveOptOuts = await inactiveOptOutsResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmCommunicationOptOutDto>>(JsonOptions);
		inactiveOptOuts.ShouldNotBeNull();
		inactiveOptOuts.Items.Select(x => x.Id).ShouldContain(cleared.Id);

		var queuedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/messages/send-template",
			new SendAdmissionCrmMessageTemplateDto { TemplateId = template.Id },
			JsonOptions);
		queuedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await queuedResponse.Content.ReadAsStringAsync());
		var queuedLog = await queuedResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		queuedLog.ShouldNotBeNull();
		queuedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.QueuedStatus);
		queuedLog.AttemptCount.ShouldBe(1);
		queuedLog.ProviderName.ShouldBe("WhatsApp Sandbox");

		var retryResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/communication/delivery-logs/{providerMissingLog.Id}/retry",
			new RetryAdmissionCrmMessageDeliveryDto
			{
				Reason = "Provider configured after first attempt."
			},
			JsonOptions);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await retryResponse.Content.ReadAsStringAsync());
		var retriedLog = await retryResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		retriedLog.ShouldNotBeNull();
		retriedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.QueuedStatus);
		retriedLog.AttemptCount.ShouldBe(1);

		var manualProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "WhatsApp Sandbox",
				SenderId = "school-main",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto
				{
					Adapter = "manual",
					PayloadMode = "envelope",
					Headers = new Dictionary<string, string>
					{
						["X-School"] = "admission"
					},
					Parameters = new Dictionary<string, string>
					{
						["templateLanguage"] = "en"
					}
				}
			},
			JsonOptions);
		manualProviderResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await manualProviderResponse.Content.ReadAsStringAsync());
		var manualProvider = await manualProviderResponse.Content
			.ReadFromJsonAsync<AdmissionCrmCommunicationProviderDto>(JsonOptions);
		manualProvider.ShouldNotBeNull();
		manualProvider.Settings.Headers["X-School"].ShouldBeEmpty();
		manualProvider.Settings.Parameters["templateLanguage"].ShouldBeEmpty();
		manualProvider.Settings.HasStoredHeaderValues.ShouldBeTrue();
		manualProvider.Settings.HasStoredParameterValues.ShouldBeTrue();

		var manualDispatchResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/messages/send-template",
			new SendAdmissionCrmMessageTemplateDto { TemplateId = template.Id },
			JsonOptions);
		manualDispatchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await manualDispatchResponse.Content.ReadAsStringAsync());
		var manualDispatchLog = await manualDispatchResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		manualDispatchLog.ShouldNotBeNull();
		manualDispatchLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.QueuedStatus);
		manualDispatchLog.StatusDetails.ShouldContain("manual");

		var unsupportedProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "WhatsApp Sandbox",
				SenderId = "school-main",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto { Adapter = "unsupported-provider" },
				WebhookUrl = "https://provider.invalid/send"
			},
			JsonOptions);
		unsupportedProviderResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await unsupportedProviderResponse.Content.ReadAsStringAsync());

		var invalidWebhookProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "WhatsApp Sandbox",
				SenderId = "school-main",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto { Adapter = "webhook" },
				WebhookUrl = "ftp://provider.invalid/send"
			},
			JsonOptions);
		invalidWebhookProviderResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await invalidWebhookProviderResponse.Content.ReadAsStringAsync());

		var logsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?leadId={lead.Id}&status={AdmissionCrmMessageDeliveryLog.QueuedStatus}");
		logsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await logsResponse.Content.ReadAsStringAsync());
		var logs = await logsResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>>(JsonOptions);
		logs.ShouldNotBeNull();
		logs.Items.Select(x => x.Id).ShouldContain(queuedLog.Id);
		logs.Items.Select(x => x.Id).ShouldContain(retriedLog.Id);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.MessageTemplateChanged &&
			x.RelatedEntityId == queuedLog.Id).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reconcile_Delivery_Log_From_Secret_Protected_Public_Callback()
	{
		var lead = await CreateCrmLeadAsync("Callback inquiry for Grade 4");
		var template = await CreateApprovedCrmMessageTemplateAsync();
		const string callbackSecret = "crm-callback-secret";

		var updateProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "WhatsApp Callback Sandbox",
				SenderId = "school-main",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto
				{
					Adapter = "manual",
					CallbackSecret = callbackSecret
				}
			},
			JsonOptions);
		updateProviderResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateProviderResponse.Content.ReadAsStringAsync());
		var protectedProvider = await updateProviderResponse.Content
			.ReadFromJsonAsync<AdmissionCrmCommunicationProviderDto>(JsonOptions);
		protectedProvider.ShouldNotBeNull();
		protectedProvider.Settings.CallbackSecret.ShouldBeNull();
		protectedProvider.Settings.HasCallbackSecret.ShouldBeTrue();

		var sendResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/messages/send-template",
			new SendAdmissionCrmMessageTemplateDto { TemplateId = template.Id },
			JsonOptions);
		sendResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await sendResponse.Content.ReadAsStringAsync());
		var queuedLog = await sendResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		queuedLog.ShouldNotBeNull();
		queuedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.QueuedStatus);

		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var anonymousClient = CreateAnonymousClient();
		var invalidCallbackResponse = await PostSignedDeliveryCallbackAsync(
			anonymousClient,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Status = "delivered",
				StatusDetails = "Provider delivered the message.",
				ProviderMessageId = "provider-message-1"
			},
			"invalid-callback-secret");
		invalidCallbackResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var invalidCallbackBody = await invalidCallbackResponse.Content.ReadAsStringAsync();
		invalidCallbackBody.ShouldContain("AdmissionCrm:DeliveryCallbackSignatureInvalid");

		var callbackPayload = new ApplyAdmissionCrmDeliveryCallbackDto
		{
			Status = "delivered",
			StatusDetails = "Provider delivered the message.",
			ProviderMessageId = "provider-message-1"
		};
		var callbackTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
		var callbackNonce = Guid.NewGuid().ToString("N");
		var callbackResponse = await PostSignedDeliveryCallbackAsync(
			anonymousClient,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			callbackPayload,
			callbackSecret,
			callbackTimestamp,
			callbackNonce);
		callbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await callbackResponse.Content.ReadAsStringAsync());
		var deliveredLog = await callbackResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		deliveredLog.ShouldNotBeNull();
		deliveredLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.DeliveredStatus);
		deliveredLog.ProviderMessageId.ShouldBe("provider-message-1");
		deliveredLog.AttemptCount.ShouldBe(queuedLog.AttemptCount);
		deliveredLog.DeliveredAt.ShouldNotBeNull();
		deliveredLog.StatusDetails.ShouldContain("provider-message-1");

		var replayResponse = await PostSignedDeliveryCallbackAsync(
			anonymousClient,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			callbackPayload,
			callbackSecret,
			callbackTimestamp,
			callbackNonce);
		replayResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		(await replayResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:DeliveryCallbackReplayDetected");

		var lateSentCallbackResponse = await PostSignedDeliveryCallbackAsync(
			anonymousClient,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Status = "sent",
				StatusDetails = "Provider sent callback arrived late.",
				ProviderMessageId = "provider-message-late"
			},
			callbackSecret);
		lateSentCallbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lateSentCallbackResponse.Content.ReadAsStringAsync());
		var lateSentLog = await lateSentCallbackResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		lateSentLog.ShouldNotBeNull();
		lateSentLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.DeliveredStatus);
		lateSentLog.ProviderMessageId.ShouldBe("provider-message-1");
		lateSentLog.AttemptCount.ShouldBe(queuedLog.AttemptCount);
		lateSentLog.DeliveredAt.ShouldBe(deliveredLog.DeliveredAt);
		lateSentLog.StatusDetails.ShouldContain("provider-message-1");
		lateSentLog.StatusDetails.ShouldNotContain("provider-message-late");

		var providerMessageLogResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?providerMessageId=message-1");
		providerMessageLogResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await providerMessageLogResponse.Content.ReadAsStringAsync());
		var providerMessageLogs = await providerMessageLogResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>>(JsonOptions);
		providerMessageLogs.ShouldNotBeNull();
		providerMessageLogs.Items.Count(x => x.Id == queuedLog.Id).ShouldBe(1);
		providerMessageLogs.Items.All(x => x.ProviderMessageId?.Contains("message-1") == true)
			.ShouldBeTrue();

		var timeline = await GetCrmTimelineAsync(lead.Id);
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.MessageTemplateChanged &&
			x.RelatedEntityId == queuedLog.Id &&
			x.Title == "Message delivery callback received").ShouldBeTrue();
		timeline.Items.Any(x =>
			x.EventType == AdmissionCrmTimelineEventType.MessageTemplateChanged &&
			x.RelatedEntityId == queuedLog.Id &&
			x.Title == "Message delivery callback ignored" &&
			(x.Details ?? string.Empty).Contains("Ignored stale Sent callback")).ShouldBeTrue();

		var mappedProviderResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto
			{
				IsEnabled = true,
				ProviderName = "WhatsApp Mapped Callback Sandbox",
				SenderId = "school-main",
				Settings = new AdmissionCrmCommunicationProviderSettingsDto
				{
					Adapter = "manual",
					CallbackSecret = callbackSecret,
					CallbackMappings = new AdmissionCrmCommunicationCallbackMappingsDto
					{
						StatusPath = "event.status",
						StatusDetailsPath = "event.description",
						ProviderMessageIdPath = "message.id"
					}
				}
			},
			JsonOptions);
		mappedProviderResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await mappedProviderResponse.Content.ReadAsStringAsync());

		var mappedSendResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/messages/send-template",
			new SendAdmissionCrmMessageTemplateDto { TemplateId = template.Id },
			JsonOptions);
		mappedSendResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await mappedSendResponse.Content.ReadAsStringAsync());
		var mappedQueuedLog = await mappedSendResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		mappedQueuedLog.ShouldNotBeNull();
		mappedQueuedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.QueuedStatus);

		var mappedCallbackResponse = await PostSignedDeliveryCallbackAsync(
			anonymousClient,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{mappedQueuedLog.Id}/callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Payload = new Dictionary<string, JsonElement>
				{
					["event"] = JsonSerializer.SerializeToElement(new
					{
						status = "failed",
						description = "Mapped provider reported failure."
					}, JsonOptions),
					["message"] = JsonSerializer.SerializeToElement(new
					{
						id = "provider-message-mapped"
					}, JsonOptions),
					["auth"] = JsonSerializer.SerializeToElement(new
					{
						secret = callbackSecret
					}, JsonOptions)
				}
			},
			callbackSecret);
		mappedCallbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await mappedCallbackResponse.Content.ReadAsStringAsync());
		var mappedFailedLog = await mappedCallbackResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		mappedFailedLog.ShouldNotBeNull();
		mappedFailedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.FailedStatus);
		mappedFailedLog.ProviderMessageId.ShouldBe("provider-message-mapped");
		mappedFailedLog.StatusDetails.ShouldContain("Mapped provider reported failure.");
		mappedFailedLog.StatusDetails.ShouldContain("provider-message-mapped");

		var providerMessageCallbackResponse = await PostSignedDeliveryCallbackAsync(
			anonymousClient,
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/{Factory.SeededAcademicYearId}/{(int) AdmissionCrmMessageChannel.WhatsApp}/delivery-callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Payload = new Dictionary<string, JsonElement>
				{
					["event"] = JsonSerializer.SerializeToElement(new
					{
						status = "delivered",
						description = "Mapped provider delivered by provider message id."
					}, JsonOptions),
					["message"] = JsonSerializer.SerializeToElement(new
					{
						id = "provider-message-mapped"
					}, JsonOptions),
					["auth"] = JsonSerializer.SerializeToElement(new
					{
						secret = callbackSecret
					}, JsonOptions)
				}
			},
			callbackSecret);
		providerMessageCallbackResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await providerMessageCallbackResponse.Content.ReadAsStringAsync());
		var providerResolvedLog = await providerMessageCallbackResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		providerResolvedLog.ShouldNotBeNull();
		providerResolvedLog.Id.ShouldBe(mappedQueuedLog.Id);
		providerResolvedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.DeliveredStatus);
		providerResolvedLog.ProviderMessageId.ShouldBe("provider-message-mapped");
		providerResolvedLog.StatusDetails.ShouldContain("Mapped provider delivered by provider message id.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Run_Configured_Journey_Rules_For_New_Lead_And_Skip_Duplicates()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var template = await CreateApprovedCrmMessageTemplateAsync();
		var taskTitle = "Journey welcome call " + Guid.NewGuid().ToString("N")[..8];
		var journeyRules = new List<AdmissionCrmJourneyRuleDto>
		{
			new()
			{
				RuleName = "New lead welcome journey",
				IsEnabled = true,
				Priority = 10,
				Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
				CreateTask = true,
				TaskTitle = taskTitle,
				TaskDueOffsetHours = 4,
				TaskNotes = "Call the parent after the inquiry acknowledgement.",
				SendMessage = true,
				MessageTemplateId = template.Id
			}
		};

		var providerResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int) AdmissionCrmMessageChannel.WhatsApp}",
			new UpdateAdmissionCrmCommunicationProviderDto { IsEnabled = false },
			JsonOptions);
		providerResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await providerResponse.Content.ReadAsStringAsync());

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules: journeyRules);

		try
		{
			var lead = await CreateCrmLeadAsync("Journey automation inquiry for Grade 3");

			var tasksResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
			tasksResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await tasksResponse.Content.ReadAsStringAsync());
			var tasks = await tasksResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
			tasks.ShouldNotBeNull();
			var journeyTask = tasks.Items.Single(x => x.Title == taskTitle);
			journeyTask.Status.ShouldBe(AdmissionCrmTaskStatus.Pending);
			journeyTask.Notes.ShouldBe("Call the parent after the inquiry acknowledgement.");

			var logsResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?leadId={lead.Id}");
			logsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await logsResponse.Content.ReadAsStringAsync());
			var logs = await logsResponse.Content
				.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>>(JsonOptions);
			logs.ShouldNotBeNull();
			var journeyLog = logs.Items.Single(x => x.TemplateId == template.Id);
			journeyLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus);

			var runResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/journeys/run",
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					LeadId = lead.Id
				},
				JsonOptions);
			runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
			var result = await runResponse.Content
				.ReadFromJsonAsync<AdmissionCrmJourneyRunResultDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.EvaluatedLeadCount.ShouldBe(1);
			result.MatchedRuleCount.ShouldBe(1);
			result.CreatedTaskCount.ShouldBe(0);
			result.SentMessageCount.ShouldBe(0);
			result.SkippedExistingTaskCount.ShouldBe(1);
			result.SkippedExistingMessageCount.ShouldBe(1);
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
	public async Task Should_Run_Journey_Rule_Only_When_Lead_Matches_Configured_Conditions()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var taskTitle = "Conditioned journey task " + Guid.NewGuid().ToString("N")[..8];
		var journeyRules = new List<AdmissionCrmJourneyRuleDto>
		{
			new()
			{
				RuleName = "Conditioned quick-call journey",
				IsEnabled = true,
				Priority = 10,
				Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				CampaignContains = "north fair",
				LeadStage = AdmissionInquiryStage.NewInquiry,
				LeadPriority = AdmissionCrmLeadPriority.Warm,
				DesiredClassContains = "grade 4",
				CampusBranchContains = "north",
				PreferredLanguageContains = "english",
				MinimumLeadScore = 0,
				MaximumLeadScore = 100,
				DaysSinceLastActivity = 2,
				CreateTask = true,
				TaskTitle = taskTitle,
				TaskDueOffsetHours = 4,
				TaskNotes = "Call only when the conditioned journey matches.",
				SendMessage = false
			}
		};

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules: journeyRules);

		try
		{
			var matchingLead = await CreateCrmLeadWithContactAsync(
				"North Fair quick call inquiry",
				"989891" + Random.Shared.Next(1000, 9999),
				AdmissionInquiryChannel.QuickCallEntry,
				campaignName: "North Fair Campaign",
				campusBranchName: "North Campus",
				preferredCommunicationLanguage: "English");
			var freshTasks = await GetCrmTasksAsync(matchingLead.Id);
			freshTasks.Items.Select(x => x.Title).ShouldNotContain(taskTitle);

			await AgeCrmLeadActivityAsync(matchingLead.Id, DateTimeOffset.UtcNow.AddDays(-3));
			var matchingRunResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/journeys/run",
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					LeadId = matchingLead.Id
				},
				JsonOptions);
			matchingRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await matchingRunResponse.Content.ReadAsStringAsync());
			var matchingRun = await matchingRunResponse.Content
				.ReadFromJsonAsync<AdmissionCrmJourneyRunResultDto>(JsonOptions);
			matchingRun.ShouldNotBeNull();
			matchingRun.EvaluatedLeadCount.ShouldBe(1);
			matchingRun.MatchedRuleCount.ShouldBe(1);
			matchingRun.CreatedTaskCount.ShouldBe(1);

			var matchingTasks = await GetCrmTasksAsync(matchingLead.Id);
			var conditionedTask = matchingTasks.Items.Single(x => x.Title == taskTitle);
			conditionedTask.Notes.ShouldBe("Call only when the conditioned journey matches.");

			var nonMatchingLead = await CreateCrmLeadWithContactAsync(
				"South Fair quick call inquiry",
				"989892" + Random.Shared.Next(1000, 9999),
				AdmissionInquiryChannel.QuickCallEntry,
				campaignName: "South Fair Campaign",
				campusBranchName: "North Campus",
				preferredCommunicationLanguage: "English");
			await AgeCrmLeadActivityAsync(nonMatchingLead.Id, DateTimeOffset.UtcNow.AddDays(-3));
			var nonMatchingRunResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/journeys/run",
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					LeadId = nonMatchingLead.Id
				},
				JsonOptions);
			nonMatchingRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await nonMatchingRunResponse.Content.ReadAsStringAsync());
			var nonMatchingRun = await nonMatchingRunResponse.Content
				.ReadFromJsonAsync<AdmissionCrmJourneyRunResultDto>(JsonOptions);
			nonMatchingRun.ShouldNotBeNull();
			nonMatchingRun.EvaluatedLeadCount.ShouldBe(1);
			nonMatchingRun.MatchedRuleCount.ShouldBe(0);
			nonMatchingRun.CreatedTaskCount.ShouldBe(0);

			var nonMatchingTasks = await GetCrmTasksAsync(nonMatchingLead.Id);
			nonMatchingTasks.Items.Select(x => x.Title).ShouldNotContain(taskTitle);
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
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Run_Lead_Created_Journey_When_Public_Form_Submitted()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var taskTitle = "Public form welcome task " + Guid.NewGuid().ToString("N")[..8];
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules:
			[
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Public form lead-created journey",
					IsEnabled = true,
					Priority = 10,
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					CreateTask = true,
					TaskTitle = taskTitle,
					TaskDueOffsetHours = 2,
					TaskNotes = "Call the family after public form submission."
				}
			]);

		try
		{
			var template = await CreatePublicInquiryTemplateAsync(
				"Public journey inquiry " + Guid.NewGuid().ToString("N")[..8]);
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
						["parentPhone"] = JsonSerializer.SerializeToElement("9933445566", JsonOptions),
						["admissionIntent"] = JsonSerializer.SerializeToElement("Admission inquiry for Grade 1", JsonOptions),
						["desiredClass"] = JsonSerializer.SerializeToElement("Grade 1", JsonOptions)
					}
				},
				JsonOptions);
			submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
			var submit = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
			submit.ShouldNotBeNull();

			var tasks = await GetCrmTasksAsync(submit.LeadId);
			var publicJourneyTask = tasks.Items.Single(x => x.Title == taskTitle);
			publicJourneyTask.Status.ShouldBe(AdmissionCrmTaskStatus.Pending);
			publicJourneyTask.Notes.ShouldBe("Call the family after public form submission.");

			var replayResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/journeys/run",
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					LeadId = submit.LeadId
				},
				JsonOptions);
			replayResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await replayResponse.Content.ReadAsStringAsync());
			var replay = await replayResponse.Content
				.ReadFromJsonAsync<AdmissionCrmJourneyRunResultDto>(JsonOptions);
			replay.ShouldNotBeNull();
			replay.CreatedTaskCount.ShouldBe(0);
			replay.SkippedExistingTaskCount.ShouldBe(1);
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
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Record_Journey_Failure_Without_Blocking_Automatic_Workflows()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var draftTemplate = await CreateDraftCrmMessageTemplateAsync();
		var leadRuleName = "Lead created invalid message " + Guid.NewGuid().ToString("N")[..8];
		var examRuleName = "Exam scheduled invalid message " + Guid.NewGuid().ToString("N")[..8];
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules:
			[
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = leadRuleName,
					IsEnabled = true,
					Priority = 10,
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					SendMessage = true,
					MessageTemplateId = draftTemplate.Id
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = examRuleName,
					IsEnabled = true,
					Priority = 20,
					Trigger = AdmissionCrmJourneyTrigger.ExamScheduled,
					SendMessage = true,
					MessageTemplateId = draftTemplate.Id
				}
			]);

		try
		{
			var lead = await CreateCrmLeadAsync("Journey failure should not block lead creation");
			var leadTimeline = await GetCrmTimelineAsync(lead.Id);
			leadTimeline.Items.Any(x =>
				x.EventType == AdmissionCrmTimelineEventType.AutomationFailed &&
				x.Details != null &&
				x.Details.Contains(leadRuleName, StringComparison.Ordinal)).ShouldBeTrue();

			var slot = await CreateAdmissionCrmExamSlotAsync("Journey failure", studentLimit: 1);
			var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);
			exam.LeadId.ShouldBe(lead.Id);

			var examTimeline = await GetCrmTimelineAsync(lead.Id);
			examTimeline.Items.Any(x =>
				x.EventType == AdmissionCrmTimelineEventType.AutomationFailed &&
				x.Details != null &&
				x.Details.Contains(examRuleName, StringComparison.Ordinal)).ShouldBeTrue();

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
			replay.MatchedRuleCount.ShouldBe(1);
			replay.FailedActionCount.ShouldBe(1);
			replay.FailureMessages.Any(x => x.Contains(examRuleName, StringComparison.Ordinal)).ShouldBeTrue();

			var analytics = await GetCrmAnalyticsSummaryAsync();
			analytics.AutomationAnalytics.FailureCount.ShouldBeGreaterThanOrEqualTo(2);
			analytics.AutomationAnalytics.RecentFailures.Any(x =>
				x.LeadId == lead.Id &&
				x.Title == "Journey automation failed" &&
				x.Details != null &&
				x.Details.Contains(examRuleName, StringComparison.Ordinal)).ShouldBeTrue();
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

	private static async Task<HttpResponseMessage> PostSignedDeliveryCallbackAsync(
		HttpClient client,
		string url,
		ApplyAdmissionCrmDeliveryCallbackDto payload,
		string signingSecret,
		string? timestamp = null,
		string? nonce = null)
	{
		var rawBody = JsonSerializer.Serialize(payload, JsonOptions);
		timestamp ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
		nonce ??= Guid.NewGuid().ToString("N");
		var signedPayload = $"{timestamp}.{nonce}.{rawBody}";
		var signature = Convert.ToHexString(
			HMACSHA256.HashData(
				Encoding.UTF8.GetBytes(signingSecret),
				Encoding.UTF8.GetBytes(signedPayload)))
			.ToLowerInvariant();

		using var request = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
		};
		request.Headers.Add("X-Admission-Callback-Signature", signature);
		request.Headers.Add("X-Admission-Callback-Timestamp", timestamp);
		request.Headers.Add("X-Admission-Callback-Nonce", nonce);
		return await client.SendAsync(request);
	}

}
