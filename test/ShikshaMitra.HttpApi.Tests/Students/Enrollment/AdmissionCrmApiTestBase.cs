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

public abstract class AdmissionCrmApiTestBase(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	protected async Task<AdmissionCrmFormTemplateDto> CreatePublicInquiryTemplateAsync(
		string templateName,
		AdmissionInquiryChannel channel = AdmissionInquiryChannel.SchoolWebsiteForm)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-templates",
			new CreateAdmissionCrmFormTemplateDto
			{
				TemplateName = templateName,
				Description = "Public form for API tests",
				Channel = channel,
				IsPublic = true,
				ConsentText = "I agree that the school may contact me about this admission inquiry.",
				PrivacyNoticeUrl = "https://school.example/privacy",
				Fields =
				[
					new AdmissionCrmFormFieldDto
					{
						Key = "studentFirstName",
						Label = "Student first name",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "FirstName",
						Required = true,
						DisplayOrder = 10
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "parentPhone",
						Label = "Parent phone",
						FieldType = AdmissionCrmFormFieldType.Phone,
						SystemField = "PrimaryContactNo",
						Required = true,
						DisplayOrder = 20
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "parentName",
						Label = "Parent name",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "ParentGuardianName",
						DisplayOrder = 25
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "studentDateOfBirth",
						Label = "Student date of birth",
						FieldType = AdmissionCrmFormFieldType.Date,
						SystemField = "DateOfBirth",
						DisplayOrder = 26
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "parentEmail",
						Label = "Parent email",
						FieldType = AdmissionCrmFormFieldType.Email,
						SystemField = "Email",
						DisplayOrder = 30
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "admissionIntent",
						Label = "Admission intent",
						FieldType = AdmissionCrmFormFieldType.TextArea,
						SystemField = "AdmissionIntent",
						Required = true,
						DisplayOrder = 40
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "desiredClass",
						Label = "Desired class",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "DesiredClassName",
						Required = true,
						DisplayOrder = 50
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "campusBranch",
						Label = "Campus branch",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "CampusBranchName",
						DisplayOrder = 60
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "preferredLanguage",
						Label = "Preferred language",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "PreferredCommunicationLanguage",
						DisplayOrder = 70
					},
					new AdmissionCrmFormFieldDto
					{
						Key = "siblingStudentName",
						Label = "Sibling student name",
						FieldType = AdmissionCrmFormFieldType.Text,
						SystemField = "Notes",
						Required = true,
						DisplayOrder = 80,
						SectionName = "Sibling details",
						HelperText = "Shown only when the applicant has a sibling in school.",
						Placeholder = "Existing sibling name",
						VisibleWhenFieldKey = "hasSibling",
						VisibleWhenValue = "true"
					}
				]
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var template = await response.Content.ReadFromJsonAsync<AdmissionCrmFormTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		return template;
	}

	protected async Task<AdmissionCrmLeadDto> CreateCrmLeadAsync(
		string admissionIntent,
		Guid? targetStandardId = null)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = admissionIntent,
				PrimaryContactNo = "989898" + Random.Shared.Next(1000, 9999),
				Email = "crm.lead." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = AdmissionInquiryChannel.WalkInReception,
				FirstName = "Aditi",
				ParentGuardianName = "Nisha Patel " + Guid.NewGuid().ToString("N")[..6],
				TargetStandardId = targetStandardId,
				CampusBranchName = "Main Campus",
				PreferredCommunicationLanguage = "English",
				DesiredClassName = "Grade 4"
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		return lead;
	}

	protected HttpClient CreateClientWithCrmPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}

	protected HttpClient CreateClientForCrmRole(string roleName)
	{
		var permissions = roleName == "Admissions Receptionist"
			? new[]
			{
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignOwner,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks
			}
			: throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Unsupported CRM role.");
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, roleName);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", permissions));
		return client;
	}

	protected async Task<AdmissionCrmRouteDefinition> CreateRouteDefinitionAsync(
		AdmissionCrmRouteType routeType,
		DateTime effectiveFrom,
		DateTime? effectiveUntil,
		bool publish)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var existingRoutes = await repository.GetListAsync(route =>
				route.TenantId == tenantId &&
				route.AcademicYearId == Factory.SeededAcademicYearId &&
				route.RouteType == routeType);
			var nextVersion = existingRoutes
				.Select(route => route.Version)
				.DefaultIfEmpty(0)
				.Max() + 1;
			var route = new AdmissionCrmRouteDefinition(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededAcademicYearId,
				$"QI-{suffix}",
				$"Quick inquiry {routeType} {suffix}",
				routeType,
				version: nextVersion,
				configurationJson: "{}",
				effectiveFrom,
				effectiveUntil);
			if (publish)
			{
				route.Submit(Guid.NewGuid());
				route.Publish(Guid.NewGuid());
			}

			await repository.InsertAsync(route);
			await uow.CompleteAsync();
			return route;
		}
	}

	protected async Task RetireRouteDefinitionAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var route = await repository.GetAsync(id);
			route.Retire();
			await repository.UpdateAsync(route);
			await uow.CompleteAsync();
		}
	}

	protected static async Task AssertForbiddenAsync(Task<HttpResponseMessage> request)
	{
		var response = await request;
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	protected async Task<AdmissionCrmLeadDto> CreateCrmLeadForClassAsync(
		string admissionIntent,
		string desiredClassName,
		string? parentGuardianName = null,
		bool omitParentGuardianName = false)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = admissionIntent,
				PrimaryContactNo = "989897" + Random.Shared.Next(1000, 9999),
				Email = "crm.class." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = AdmissionInquiryChannel.WalkInReception,
				FirstName = "Merit",
				ParentGuardianName = omitParentGuardianName
					? null
					: parentGuardianName ?? "Merit Parent " + Guid.NewGuid().ToString("N")[..6],
				DesiredClassName = desiredClassName
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		return lead;
	}

	protected async Task<AdmissionCrmLeadDto> CreateCrmLeadWithContactAsync(
		string admissionIntent,
		string primaryContactNo,
		AdmissionInquiryChannel channel,
		string desiredClassName = "Grade 4",
		string? campaignName = null,
		string? parentGuardianName = null,
		string? campusBranchName = null,
		string? preferredCommunicationLanguage = null,
		string? duplicateContinueReason = null)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = admissionIntent,
				PrimaryContactNo = primaryContactNo,
				Email = "crm.analytics." + Guid.NewGuid().ToString("N")[..8] + "@example.com",
				Channel = channel,
				CampaignName = campaignName,
				CampusBranchName = campusBranchName,
				PreferredCommunicationLanguage = preferredCommunicationLanguage,
				FirstName = "Analytics",
				ParentGuardianName = parentGuardianName ?? "Analytics Parent " + Guid.NewGuid().ToString("N")[..6],
				DesiredClassName = desiredClassName,
				DuplicateContinueReason = duplicateContinueReason
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		return lead;
	}

	protected async Task<AdmissionCrmMessageTemplateDto> CreateApprovedCrmMessageTemplateAsync(
		AdmissionCrmMessageChannel channel = AdmissionCrmMessageChannel.WhatsApp,
		string? body = null,
		IReadOnlyList<string>? variables = null)
	{
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates",
			new CreateAdmissionCrmMessageTemplateDto
			{
				TemplateName = "Communication template " + Guid.NewGuid().ToString("N")[..8],
				Channel = channel,
				Purpose = "Exam reminder",
				Body = body ?? "Dear {{parentName}}, {{studentName}} has an entrance exam on {{examDate}}.",
				Variables = variables?.ToList() ?? new List<string> { "parentName", "studentName", "examDate" }
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplateDto>(JsonOptions);
		created.ShouldNotBeNull();

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
		return approved;
	}

	protected async Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetCrmDeliveryLogsAsync(
		Guid leadId,
		Guid templateId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?leadId={leadId}&templateId={templateId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var logs = await response.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>>(
			JsonOptions);
		logs.ShouldNotBeNull();
		return logs;
	}

	protected async Task<AdmissionCrmMessageTemplateDto> CreateDraftCrmMessageTemplateAsync()
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates",
			new CreateAdmissionCrmMessageTemplateDto
			{
				TemplateName = "Draft journey template " + Guid.NewGuid().ToString("N")[..8],
				Channel = AdmissionCrmMessageChannel.WhatsApp,
				Purpose = "Draft journey failure test",
				Body = "Dear {{parentName}}, this template is intentionally not approved.",
				Variables = ["parentName"]
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var template = await response.Content.ReadFromJsonAsync<AdmissionCrmMessageTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.Status.ShouldBe(AdmissionCrmMessageTemplateStatus.Draft);
		return template;
	}

	protected async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetCrmTimelineAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/timeline?maxResultCount=20");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var timeline = await response.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmTimelineEventDto>>(JsonOptions);
		timeline.ShouldNotBeNull();
		return timeline;
	}

	protected async Task<PagedResultDto<AdmissionCrmFollowUpTaskDto>> GetCrmTasksAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/tasks?maxResultCount=20");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var tasks = await response.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		return tasks;
	}

	protected async Task<AdmissionCrmAnalyticsSummaryDto> GetCrmAnalyticsSummaryAsync()
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/analytics/summary");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var summary = await response.Content.ReadFromJsonAsync<AdmissionCrmAnalyticsSummaryDto>(JsonOptions);
		summary.ShouldNotBeNull();
		return summary;
	}

	protected async Task AgeCrmLeadActivityAsync(Guid leadId, DateTimeOffset lastActivityTime)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leadRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await leadRepository.GetAsync(leadId);
			typeof(AdmissionInquiryLead)
				.GetField("<LastActivityTime>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(lead, lastActivityTime);
			await leadRepository.UpdateAsync(lead);
			await uow.CompleteAsync();
		}
	}

	protected async Task MarkCrmLeadStaleAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leadRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await leadRepository.GetAsync(leadId);
			lead.MarkStale();
			await leadRepository.UpdateAsync(lead);
			await uow.CompleteAsync();
		}
	}

	protected async Task AgeCrmLeadCreationAsync(Guid leadId, DateTime creationTime)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leadRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await leadRepository.GetAsync(leadId);
			typeof(AdmissionInquiryLead)
				.GetProperty(nameof(AdmissionInquiryLead.CreationTime))!
				.SetValue(lead, creationTime);
			await leadRepository.UpdateAsync(lead);
			await uow.CompleteAsync();
		}
	}

	protected async Task<AdmissionCrmCycleConfigurationDto> GetCrmCycleConfigurationAsync()
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/cycle");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var cycle = await response.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		cycle.ShouldNotBeNull();
		return cycle;
	}

	protected async Task SetCrmAutomationReferencesAsync(
		IReadOnlyList<AdmissionCrmJourneyRuleDto> journeyRules,
		IReadOnlyList<AdmissionCrmChannelSettingDto> channelSettings)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var cycleRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmCycleConfiguration, Guid>>();
			var configuration = await cycleRepository.FindAsync(item =>
				item.TenantId == Guid.Parse(TestAuthHandler.TestTenantId) &&
				item.AcademicYearId == Factory.SeededAcademicYearId);
			configuration.ShouldNotBeNull();
			configuration.Update(
				configuration.CycleName,
				configuration.IsActive,
				configuration.RegistrationFormRequiredBeforeExam,
				configuration.EntranceExamRequired,
				configuration.StaleAfterDays,
				configuration.ArchiveAfterDays,
				configuration.DefaultLeadSlaHours,
				configuration.ActivationCancellationWindowDays,
				configuration.DefaultExamPassingPercentage,
				configuration.MeritRankingMode,
				configuration.ExamReadinessFields,
				configuration.LeadScoringRulesJson,
				configuration.LeadRoutingRulesJson,
				JsonSerializer.Serialize(journeyRules, JsonOptions),
				configuration.ApplicantChecklistJson,
				JsonSerializer.Serialize(channelSettings, JsonOptions),
				configuration.SlaPolicyJson,
				configuration.Notes);
			await cycleRepository.UpdateAsync(configuration, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	protected async Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultCycleAsync(
		UpdateAdmissionCrmCycleConfigurationDto input)
	{
		var response = await Client.PutAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration/cycle-default",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var cycle = await response.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		cycle.ShouldNotBeNull();
		return cycle;
	}

	protected async Task<AdmissionCrmInquirySourceDto> CreateInquirySourceAsync(string displayName)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/catalog/inquiry-sources",
			new AdmissionCrmInquirySourceUpsertDto
			{
				Code = $"test-{Guid.NewGuid():N}",
				DisplayName = displayName,
				AllowedChannels = [AdmissionInquiryChannel.SchoolWebsiteForm]
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var source = await response.Content.ReadFromJsonAsync<AdmissionCrmInquirySourceDto>(JsonOptions);
		source.ShouldNotBeNull();
		return source;
	}

	protected async Task ResetSchoolDefaultCycleAsync()
	{
		await UpdateSchoolDefaultCycleAsync(new UpdateAdmissionCrmCycleConfigurationDto
		{
			CycleName = "School Admissions CRM Defaults",
			IsActive = true,
			RegistrationFormRequiredBeforeExam = false,
			StaleAfterDays = 30,
			ArchiveAfterDays = 60,
			DefaultLeadSlaHours = 24,
			DefaultExamPassingPercentage = 40m,
			MeritRankingMode = AdmissionCrmMeritRankingMode.MarksThenInquiryDate,
			ExamReadinessFields =
				AdmissionCrmExamReadinessField.Contact |
				AdmissionCrmExamReadinessField.AdmissionIntent |
				AdmissionCrmExamReadinessField.DesiredClass,
			LeadScoringRules = AdmissionCrmLeadScoringRulesDto.CreateDefault(),
			LeadRoutingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault(),
			JourneyRules = new List<AdmissionCrmJourneyRuleDto>(),
			ApplicantChecklist = new List<AdmissionCrmApplicantChecklistItemDto>(),
			ChannelSettings = new List<AdmissionCrmChannelSettingDto>(),
			Notes = null
		});
	}

	protected async Task<Guid> CreateAcademicYearAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var startYear = 2400 + Random.Shared.Next(0, 1000);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYear = await academicYearManager.CreateAsync(
				$"AdmissionCrm-{suffix}",
				new DateTime(startYear, 4, 1),
				new DateTime(startYear + 1, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);
			await uow.CompleteAsync();
			return academicYear.Id;
		}
	}

	protected async Task<AdmissionCrmCycleConfigurationDto> UpdateCrmCycleConfigurationAsync(
		AdmissionCrmCycleConfigurationDto currentCycle,
		AdmissionCrmExamReadinessField examReadinessFields,
		AdmissionCrmLeadScoringRulesDto? leadScoringRules = null,
		AdmissionCrmLeadRoutingSettingsDto? leadRoutingSettings = null,
		IReadOnlyList<AdmissionCrmJourneyRuleDto>? journeyRules = null,
		IReadOnlyList<AdmissionCrmApplicantChecklistItemDto>? applicantChecklist = null,
		IReadOnlyList<AdmissionCrmChannelSettingDto>? channelSettings = null,
		bool? registrationFormRequiredBeforeExam = null)
	{
		var input = new UpdateAdmissionCrmCycleConfigurationDto
		{
			CycleName = currentCycle.CycleName,
			IsActive = currentCycle.IsActive,
			RegistrationFormRequiredBeforeExam =
					registrationFormRequiredBeforeExam ?? currentCycle.RegistrationFormRequiredBeforeExam,
			StaleAfterDays = currentCycle.StaleAfterDays,
			ArchiveAfterDays = currentCycle.ArchiveAfterDays,
			DefaultLeadSlaHours = currentCycle.DefaultLeadSlaHours,
			DefaultExamPassingPercentage = currentCycle.DefaultExamPassingPercentage,
			MeritRankingMode = currentCycle.MeritRankingMode,
			ExamReadinessFields = examReadinessFields,
			LeadScoringRules = leadScoringRules ?? currentCycle.LeadScoringRules,
			LeadRoutingSettings = leadRoutingSettings ?? currentCycle.LeadRoutingSettings,
			JourneyRules = (journeyRules ?? currentCycle.JourneyRules).ToList(),
			ApplicantChecklist = (applicantChecklist ?? currentCycle.ApplicantChecklist).ToList(),
			ChannelSettings = (channelSettings ?? currentCycle.ChannelSettings).ToList(),
			Notes = currentCycle.Notes
		};
		var response = await PutCycleConfigurationWithImpactAsync(
			Factory.SeededAcademicYearId,
			input);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var updated = await response.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		updated.ShouldNotBeNull();
		return updated;
	}

	protected async Task<HttpResponseMessage> PutCycleConfigurationWithImpactAsync(
		Guid academicYearId,
		UpdateAdmissionCrmCycleConfigurationDto input)
	{
		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration-safety/impact-preview",
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				ProposedCycleConfiguration = input
			},
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationImpactPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		input.ImpactPreviewFingerprint = preview.Fingerprint;

		return await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle",
			input,
			JsonOptions);
	}

	protected async Task<HttpResponseMessage> PutPipelineStageWithImpactAsync(
		Guid academicYearId,
		Guid stageId,
		UpdateAdmissionCrmPipelineStageDto input)
	{
		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration-safety/impact-preview",
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				PipelineStageId = stageId,
				ProposedPipelineStage = input
			},
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationImpactPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		input.ImpactPreviewFingerprint = preview.Fingerprint;

		return await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/pipeline-stages/{stageId}",
			input,
			JsonOptions);
	}

	protected async Task<AdmissionCrmEntranceExamDto> GetCrmEntranceExamByLeadAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/exam");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var exam = await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		return exam;
	}

	protected async Task<AdmissionCrmEntranceExamDto> ScheduleCrmEntranceExamAsync(
		Guid leadId,
		Guid slotId,
		bool overrideCapacity = false,
		string? capacityOverrideReason = null)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto
			{
				EntranceExamSlotId = slotId,
				OverrideCapacity = overrideCapacity,
				CapacityOverrideReason = capacityOverrideReason
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var exam = await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		return exam;
	}

	protected async Task<AdmissionCrmExamSlotDto> CreateAdmissionCrmExamSlotAsync(
		string suffix,
		int studentLimit,
		DateTimeOffset? startDate = null)
	{
		var start = startDate ?? DateTimeOffset.UtcNow.AddDays(7);
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
		var slot = await response.Content.ReadFromJsonAsync<AdmissionCrmExamSlotDto>(JsonOptions);
		slot.ShouldNotBeNull();
		return slot;
	}

	protected async Task AssertPublicSubmissionPersistedAsync(Guid leadId, string? submissionKey = null)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leadRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		var submissionRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionInquiryFormSubmission, Guid>>();
		var consentRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionInquiryConsentRecord, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await leadRepository.GetAsync(leadId);
			lead.FirstName.ShouldBe("Riya");
			lead.PrimaryContactNo.ShouldBe("9988776655");
			lead.Email.ShouldBe("riya.parent@example.com");
			lead.AdmissionIntent.ShouldBe("Admission inquiry for Grade 1");
			lead.DesiredClassName.ShouldBe("Grade 1");
			lead.CampusBranchName.ShouldBe("North Campus");
			lead.PreferredCommunicationLanguage.ShouldBe("Hindi");
			lead.CampaignName.ShouldBe("Monsoon QR Drive");

			var submissions = await submissionRepository.GetQueryableAsync();
			var submission = submissions.Single(x => x.LeadId == leadId);
			if (submissionKey != null)
			{
				submission.SubmissionKey.ShouldBe(submissionKey);
			}
			submission.SubmittedFieldsJson.ShouldContain("studentFirstName");
			submission.SubmittedFieldsJson.ShouldContain("campaignName");

			var consents = await consentRepository.GetQueryableAsync();
			var consent = consents.Single(x => x.LeadId == leadId);
			consent.ConsentTextSnapshot.ShouldContain("school may contact me");
			consent.PrivacyNoticeUrl.ShouldBe("https://school.example/privacy");
			consent.CommunicationPreferencesJson.ShouldContain("WhatsApp");

			await uow.CompleteAsync();
		}
	}

	protected async Task AssertPublicSubmissionContainsAsync(Guid leadId, string fieldKey)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var submissionRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionInquiryFormSubmission, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var submissions = await submissionRepository.GetQueryableAsync();
			var submission = submissions.Single(x => x.LeadId == leadId);
			submission.SubmittedFieldsJson.ShouldContain(fieldKey);

			await uow.CompleteAsync();
		}
	}

	protected async Task<string> GetEncryptedOtpCodeForLeadAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var statusAccessRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var encryptionService = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await statusAccessRepository.GetQueryableAsync();
			var access = queryable.Single(x => x.LeadId == leadId);
			access.EncryptedOtpCode.ShouldNotBeNullOrWhiteSpace();
			await uow.CompleteAsync();
			return encryptionService.Decrypt(access.EncryptedOtpCode!)!;
		}
	}

	protected async Task<string> GetEncryptedOtpCodeForChallengeAsync(Guid challengeId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var statusAccessRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var encryptionService = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var access = await statusAccessRepository.GetAsync(challengeId);
			access.EncryptedOtpCode.ShouldNotBeNullOrWhiteSpace();
			await uow.CompleteAsync();
			return encryptionService.Decrypt(access.EncryptedOtpCode!)!;
		}
	}

	protected async Task AssertDelegatedFamilyAccessSetupAsync(Guid guardianPersonId, params Guid[] leadIds)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var personRepository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmPerson, Guid>>();
		var guardianRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmGuardianRelationship, Guid>>();
		var accessRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var leadIdList = leadIds.ToList();
			var person = await personRepository.GetAsync(guardianPersonId);
			person.NormalizedPhone.ShouldBe("9876500099");
			var guardianQueryable = await guardianRepository.GetQueryableAsync();
			var relationships = guardianQueryable.Where(x =>
				x.GuardianPersonId == guardianPersonId && leadIdList.Contains(x.LeadId)).ToList();
			relationships.Count.ShouldBe(2);
			relationships.ShouldAllBe(x => x.IsActive && x.HasPortalAccess && x.AuthorityVerified);
			var accessQueryable = await accessRepository.GetQueryableAsync();
			accessQueryable.Count(x => leadIdList.Contains(x.LeadId) && x.EncryptedToken != null).ShouldBe(2);
			await uow.CompleteAsync();
		}
	}

	protected async Task<bool> StatusAccessExistsAsync(Guid challengeId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var exists = await repository.FindAsync(challengeId) != null;
			await uow.CompleteAsync();
			return exists;
		}
	}

	protected async Task<int> GetStatusOtpFailedAttemptCountForLeadAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var statusAccessRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await statusAccessRepository.GetQueryableAsync();
			var access = queryable.Single(x => x.LeadId == leadId);
			await uow.CompleteAsync();
			return access.OtpFailedAttemptCount;
		}
	}
}
