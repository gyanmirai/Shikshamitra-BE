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
public class AdmissionCrmSetupApi_Tests(ShikshaMitraApiTestFactory factory) : AdmissionCrmApiTestBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Expose_Safe_Public_Admission_Portal_Branding()
	{
		using var anonymousClient = CreateAnonymousClient();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		var response = await anonymousClient.GetAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/portal");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var portal = await response.Content.ReadFromJsonAsync<PublicAdmissionPortalDto>(JsonOptions);
		portal.ShouldNotBeNull();
		portal.SchoolName.ShouldNotBeNullOrWhiteSpace();
		portal.SupportedLanguages.ShouldContain("en");
		portal.DefaultLanguage.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Anonymous_Crm_Workspace_Access()
	{
		using var anonymousClient = CreateAnonymousClient();
		anonymousClient.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);

		var response = await anonymousClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Expose_Admission_Cycles_To_Configuration_Managers()
	{
		var cyclesResponse = await Client.GetAsync(
			"/api/student-enrollment/admissions-crm/setup/cycles?skipCount=0&maxResultCount=10");
		cyclesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cyclesResponse.Content.ReadAsStringAsync());
		var cycles = await cyclesResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmCycleListItemDto>>(JsonOptions);
		cycles.ShouldNotBeNull();
		cycles.Items.ShouldContain(cycle => cycle.AcademicYearId == Factory.SeededAcademicYearId);
		var seededCycle = cycles.Items.Single(cycle => cycle.AcademicYearId == Factory.SeededAcademicYearId);
		seededCycle.CycleName.ShouldNotBeNullOrWhiteSpace();
		seededCycle.AcademicYearName.ShouldNotBeNullOrWhiteSpace();
		seededCycle.ApplicantCount.ShouldBeGreaterThanOrEqualTo(0);

		var summaryResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/summary");
		summaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await summaryResponse.Content.ReadAsStringAsync());
		var summary = await summaryResponse.Content
			.ReadFromJsonAsync<AdmissionCrmCycleSummaryDto>(JsonOptions);
		summary.ShouldNotBeNull();
		summary.Cycle.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		summary.Readiness.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		summary.Policy.DefaultLeadSlaHours.ShouldBeGreaterThan(0);
		summary.ApplicationStagePolicy.Stages.ShouldContain(stage =>
			stage.StageKey == AdmissionCrmApplicationStageKey.Inquiry &&
			stage.IsOperationallyBacked);
		summary.ApplicationStagePolicy.Stages.ShouldContain(stage =>
			stage.StageKey == AdmissionCrmApplicationStageKey.ClassPlacement &&
			!stage.IsOperationallyBacked);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Admission_Cycle_Setup_For_Receptionist()
	{
		using var receptionistClient = CreateClientForCrmRole("Admissions Receptionist");
		var cyclesResponse = await receptionistClient.GetAsync(
			"/api/student-enrollment/admissions-crm/setup/cycles?skipCount=0&maxResultCount=10");
		cyclesResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var summaryResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/summary");
		summaryResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var stagePolicyResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy");
		stagePolicyResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Application_Stage_Policy_With_Impact_Preview()
	{
		var getResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getResponse.Content.ReadAsStringAsync());
		var policy = await getResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicationStagePolicyDto>(JsonOptions);
		policy.ShouldNotBeNull();
		policy.Stages.ShouldContain(stage => stage.StageKey == AdmissionCrmApplicationStageKey.Activation);

		var reordered = policy.Stages
			.Select(stage => new UpdateAdmissionCrmApplicationStagePolicyItemDto
			{
				StageKey = stage.StageKey,
				DisplayOrder = stage.StageKey == AdmissionCrmApplicationStageKey.Activation
					? 1
					: stage.DisplayOrder + 1,
				IsEnabled = stage.IsEnabled
			})
			.OrderBy(stage => stage.DisplayOrder)
			.ToList();
		var update = new UpdateAdmissionCrmApplicationStagePolicyDto { Stages = reordered };

		var rejectedResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy",
			update,
			JsonOptions);
		rejectedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await rejectedResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:StagePolicyImpactPreviewRequired");

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy/preview",
			update,
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var impact = await previewResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicationStagePolicyImpactDto>(JsonOptions);
		impact.ShouldNotBeNull();
		impact.ImpactFingerprint.ShouldNotBeNullOrWhiteSpace();

		update.ExpectedImpactFingerprint = impact.ImpactFingerprint;
		var updatedResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy",
			update,
			JsonOptions);
		updatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updatedResponse.Content.ReadAsStringAsync());
		var updated = await updatedResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicationStagePolicyDto>(JsonOptions);
		updated.ShouldNotBeNull();
		updated.Stages.First(stage => stage.StageKey == AdmissionCrmApplicationStageKey.Activation)
			.DisplayOrder.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Disabling_Required_Application_Stage()
	{
		var getResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy");
		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getResponse.Content.ReadAsStringAsync());
		var policy = await getResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicationStagePolicyDto>(JsonOptions);
		policy.ShouldNotBeNull();

		var update = new UpdateAdmissionCrmApplicationStagePolicyDto
		{
			Stages = policy.Stages.Select(stage => new UpdateAdmissionCrmApplicationStagePolicyItemDto
			{
				StageKey = stage.StageKey,
				DisplayOrder = stage.DisplayOrder,
				IsEnabled = stage.StageKey != AdmissionCrmApplicationStageKey.Inquiry && stage.IsEnabled
			}).ToList(),
			ExpectedImpactFingerprint = policy.ImpactFingerprint
		};

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/setup/cycles/{Factory.SeededAcademicYearId}/stage-policy/preview",
			update,
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:RequiredApplicationStageDisabled");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Allow_Admissions_Receptionist_To_Load_Pipeline_And_Create_Open_Quick_Inquiry()
	{
		using var receptionistClient = CreateClientForCrmRole("Admissions Receptionist");
		var academicYearId = Factory.SeededAcademicYearId;

		var stagesResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/pipeline-stages");
		stagesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stagesResponse.Content.ReadAsStringAsync());
		var stages = await stagesResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmPipelineStageDto>>(JsonOptions);
		stages.ShouldNotBeNull();
		stages.Items.ShouldContain(stage => stage.SystemStage == AdmissionInquiryStage.NewInquiry);

		var optionsResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/quick-inquiry-options");
		optionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optionsResponse.Content.ReadAsStringAsync());
		var options = await optionsResponse.Content
			.ReadFromJsonAsync<AdmissionCrmQuickInquiryOptionsDto>(JsonOptions);
		options.ShouldNotBeNull();
		options.AcademicYearId.ShouldBe(academicYearId);
		options.CycleName.ShouldNotBeNullOrWhiteSpace();
		options.Channels.ShouldNotBeEmpty();
		options.Channels.Select(channel => channel.DisplayOrder)
			.ShouldBe(options.Channels.Select(channel => channel.DisplayOrder).OrderBy(order => order));

		var fullConfigurationResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle");
		fullConfigurationResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var createResponse = await receptionistClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Reception quick inquiry for Grade 1",
				Channel = options.Channels.FirstOrDefault(channel => channel.IsDefault)?.Channel
						  ?? options.Channels[0].Channel,
				FirstName = "Aarav",
				MiddleName = "Kumar",
				LastName = "Patel",
				ParentGuardianName = "Nisha Patel",
				PrimaryContactNo = "9876543201",
				Email = "reception.parent@example.com",
				TargetStandardId = Factory.SeededStandardId,
				AcademicBoardId = Factory.SeededBoardId,
				AcademicMediumId = Factory.SeededMediumId,
				AcademicMediumStageId = Factory.SeededMediumStageId,
				DesiredClassName = "Untrusted client class label"
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		created.ShouldNotBeNull();
		created.ApplicantNumber.ShouldBeGreaterThan(0);
		created.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);
		created.FirstName.ShouldBe("Aarav");
		created.MiddleName.ShouldBe("Kumar");
		created.LastName.ShouldBe("Patel");
		created.DesiredClassName.ShouldNotBe("Untrusted client class label");
		created.DesiredClassName.ShouldNotBeNullOrWhiteSpace();

		var openResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{created.Id}");
		openResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await openResponse.Content.ReadAsStringAsync());
		var opened = await openResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		opened.ShouldNotBeNull();
		opened.Id.ShouldBe(created.Id);
		opened.ApplicantNumber.ShouldBe(created.ApplicantNumber);
		opened.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);

		var applicantNumberSearchResponse = await receptionistClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/leads?filterText=%23{created.ApplicantNumber:D4}");
		applicantNumberSearchResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await applicantNumberSearchResponse.Content.ReadAsStringAsync());
		var applicantNumberSearch = await applicantNumberSearchResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmLeadDto>>(JsonOptions);
		applicantNumberSearch.ShouldNotBeNull();
		applicantNumberSearch.Items.ShouldContain(item =>
			item.Id == created.Id && item.ApplicantNumber == created.ApplicantNumber);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Phantom_Academic_Year_Before_Initializing_Pipeline_Configuration()
	{
		var missingAcademicYearId = Guid.NewGuid();
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{missingAcademicYearId}/configuration/pipeline-stages");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:AcademicYearNotFound");

		var optionsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{missingAcademicYearId}/quick-inquiry-options");
		optionsResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await optionsResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:AcademicYearNotFound");

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{missingAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Phantom academic year guard",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Guard",
				PrimaryContactNo = "9876500000"
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await createResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:AcademicYearNotFound");

		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var cycleRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmCycleConfiguration, Guid>>();
			var stageRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmPipelineStage, Guid>>();
			var leadRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
			(await cycleRepository.AnyAsync(item => item.AcademicYearId == missingAcademicYearId))
				.ShouldBeFalse();
			(await stageRepository.AnyAsync(item => item.AcademicYearId == missingAcademicYearId))
				.ShouldBeFalse();
			(await leadRepository.AnyAsync(item => item.AcademicYearId == missingAcademicYearId))
				.ShouldBeFalse();
			await uow.CompleteAsync();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Expose_Operational_Metadata_With_Either_Add_Or_View_Permission()
	{
		foreach (var permission in new[]
				 {
					 ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
					 ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View
				 })
		{
			using var client = CreateClientWithCrmPermissions(permission);
			var optionsResponse = await client.GetAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/quick-inquiry-options");
			optionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optionsResponse.Content.ReadAsStringAsync());

			var stagesResponse = await client.GetAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/pipeline-stages");
			stagesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stagesResponse.Content.ReadAsStringAsync());
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Add_Permission_For_Operational_Standard_Options()
	{
		var endpoint =
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operational-standard-options";

		using var quickInquiryClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add);
		var quickInquiryResponse = await quickInquiryClient.GetAsync(endpoint);
		quickInquiryResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await quickInquiryResponse.Content.ReadAsStringAsync());

		using var paperRegistrationClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm);
		var paperRegistrationResponse = await paperRegistrationClient.GetAsync(endpoint);
		paperRegistrationResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await paperRegistrationResponse.Content.ReadAsStringAsync());

		using var viewOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
		var viewOnlyResponse = await viewOnlyClient.GetAsync(endpoint);
		viewOnlyResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var setupResponse = await quickInquiryClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/{Factory.SeededAcademicYearId}/standard-options");
		setupResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Only_Enabled_Ordered_Channels_And_Effective_Published_Routes()
	{
		var originalCycle = await CompleteWithinAsync(
			GetCrmCycleConfigurationAsync(),
			"load the original CRM cycle configuration");
		var channelSettings = originalCycle.ChannelSettings
			.Select(setting => new AdmissionCrmChannelSettingDto
			{
				Channel = setting.Channel,
				IsEnabled = setting.Channel != AdmissionInquiryChannel.WhatsAppInquiry,
				DefaultInquirySourceId = setting.DefaultInquirySourceId,
				DefaultFormTemplateId = setting.DefaultFormTemplateId,
				DefaultOwnerUserId = setting.DefaultOwnerUserId,
				DefaultAcknowledgementTemplateId = setting.DefaultAcknowledgementTemplateId,
				DisplayOrder = setting.Channel switch
				{
					AdmissionInquiryChannel.WalkInReception => 5,
					AdmissionInquiryChannel.QuickCallEntry => 10,
					_ => setting.DisplayOrder + 20
				}
			})
			.ToList();
		var activeRoute = await CompleteWithinAsync(
			CreateRouteDefinitionAsync(
				AdmissionCrmRouteType.Fresh,
				DateTime.UtcNow.AddDays(-1),
				DateTime.UtcNow.AddDays(1),
				publish: true),
			"create the active route definition");
		var futureRoute = await CompleteWithinAsync(
			CreateRouteDefinitionAsync(
				AdmissionCrmRouteType.SiblingPriority,
				DateTime.UtcNow.AddDays(1),
				DateTime.UtcNow.AddDays(2),
				publish: true),
			"create the future route definition");
		var draftRoute = await CompleteWithinAsync(
			CreateRouteDefinitionAsync(
				AdmissionCrmRouteType.Lateral,
				DateTime.UtcNow.AddDays(-1),
				DateTime.UtcNow.AddDays(1),
				publish: false),
			"create the draft route definition");

		await CompleteWithinAsync(
			UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				channelSettings: channelSettings),
			"update the channel settings");

		try
		{
			using var viewClient = CreateClientWithCrmPermissions(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
			var optionsResponse = await CompleteWithinAsync(
				viewClient.GetAsync(
					$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/quick-inquiry-options"),
				"load quick inquiry options");
			optionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await optionsResponse.Content.ReadAsStringAsync());
			var options = await optionsResponse.Content
				.ReadFromJsonAsync<AdmissionCrmQuickInquiryOptionsDto>(JsonOptions);
			options.ShouldNotBeNull();
			options.Channels.Select(channel => channel.Channel)
				.ShouldNotContain(AdmissionInquiryChannel.WhatsAppInquiry);
			options.Channels.Select(channel => channel.Channel).Take(2)
				.ShouldBe(new[]
				{
					AdmissionInquiryChannel.WalkInReception,
					AdmissionInquiryChannel.QuickCallEntry
				});
			options.Channels.ShouldAllBe(channel => !string.IsNullOrWhiteSpace(channel.Label));
			options.Channels.Count(channel => channel.IsDefault).ShouldBe(1);
			options.Routes.Select(route => route.RouteDefinitionId).ShouldContain(activeRoute.Id);
			options.Routes.Select(route => route.RouteDefinitionId).ShouldNotContain(futureRoute.Id);
			options.Routes.Select(route => route.RouteDefinitionId).ShouldNotContain(draftRoute.Id);
			options.Routes.Single(route => route.IsDefault).RouteDefinitionId.ShouldBe(
				options.Routes.First(route => route.RouteType == AdmissionCrmRouteType.Fresh).RouteDefinitionId);

			var createResponse = await CompleteWithinAsync(Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Fresh admission pathway snapshot",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Route",
					PrimaryContactNo = "9876543202",
					IntendedRouteDefinitionId = activeRoute.Id
				},
				JsonOptions),
				"create a lead with the active route");
			createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
			var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			created.ShouldNotBeNull();
			created.IntendedRouteType.ShouldBe(AdmissionCrmRouteType.Fresh);
			created.IntendedRouteDefinitionId.ShouldBe(activeRoute.Id);
			created.IntendedRouteDefinitionVersion.ShouldBe(activeRoute.Version);
			created.IntendedRouteNameSnapshot.ShouldBe(activeRoute.Name);

			var futureCreateResponse = await CompleteWithinAsync(Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Future route must not be assignable",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Future",
					PrimaryContactNo = "9876543203",
					IntendedRouteDefinitionId = futureRoute.Id
				},
				JsonOptions),
				"reject a lead with a future route");
			futureCreateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await futureCreateResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:PublishedRouteDefinitionRequired");

			var mismatchResponse = await CompleteWithinAsync(Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Route type mismatch must be rejected",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Mismatch",
					PrimaryContactNo = "9876543204",
					IntendedRouteDefinitionId = activeRoute.Id,
					IntendedRouteType = AdmissionCrmRouteType.Lateral
				},
				JsonOptions),
				"reject a route type mismatch");
			mismatchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await mismatchResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:IntendedRouteMismatch");

			var arbitraryEnumResponse = await CompleteWithinAsync(Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "An enum alone must not create a pathway snapshot",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Enum",
					PrimaryContactNo = "9876543207",
					IntendedRouteType = AdmissionCrmRouteType.Lateral
				},
				JsonOptions),
				"reject a route enum without a published definition");
			arbitraryEnumResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await arbitraryEnumResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:PublishedRouteDefinitionRequired");

			var missingFirstNameResponse = await CompleteWithinAsync(Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Staff-assisted capture requires applicant identity",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					PrimaryContactNo = "9876543208",
					IntendedRouteDefinitionId = activeRoute.Id
				},
				JsonOptions),
				"reject a staff-assisted lead without a first name");
			missingFirstNameResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await missingFirstNameResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:FirstNameRequired");

			var externalChannelBypassResponse = await CompleteWithinAsync(Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "An authenticated staff endpoint cannot impersonate a public form",
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					PrimaryContactNo = "9876543209",
					IntendedRouteDefinitionId = activeRoute.Id
				},
				JsonOptions),
				"reject an external-channel identity bypass");
			externalChannelBypassResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await externalChannelBypassResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:FirstNameRequired");
		}
		finally
		{
			await CompleteWithinAsync(
				RetireRouteDefinitionAsync(activeRoute.Id),
				"retire the active route definition");
			await CompleteWithinAsync(
				RetireRouteDefinitionAsync(futureRoute.Id),
				"retire the future route definition");
			await CompleteWithinAsync(
				UpdateCrmCycleConfigurationAsync(
					originalCycle,
					originalCycle.ExamReadinessFields,
					channelSettings: originalCycle.ChannelSettings),
				"restore the channel settings");
		}
	}

	private static async Task<T> CompleteWithinAsync<T>(Task<T> operation, string operationName)
	{
		try
		{
			return await operation.WaitAsync(TimeSpan.FromSeconds(30));
		}
		catch (TimeoutException exception)
		{
			throw new TimeoutException($"Timed out while attempting to {operationName}.", exception);
		}
	}

	private static async Task CompleteWithinAsync(Task operation, string operationName)
	{
		try
		{
			await operation.WaitAsync(TimeSpan.FromSeconds(30));
		}
		catch (TimeoutException exception)
		{
			throw new TimeoutException($"Timed out while attempting to {operationName}.", exception);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Malformed_Email_And_Standard_From_Another_Academic_Year()
	{
		var invalidEmailResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Invalid email inquiry",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Invalid",
				PrimaryContactNo = "9876543205",
				Email = "parent.example.com"
			},
			JsonOptions);
		invalidEmailResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var otherAcademicYearId = await CreateAcademicYearAsync();
		var academicMismatchResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{otherAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Cross-year placement must be rejected",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Crossyear",
				PrimaryContactNo = "9876543206",
				TargetStandardId = Factory.SeededStandardId
			},
			JsonOptions);
		academicMismatchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await academicMismatchResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicPlacementMismatch");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Only_Configured_Hr_Employee_Owners()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var eligibleStaff = await GetEligibleCrmOwnersAsync();
		var configuredOwners = eligibleStaff
			.Where(owner => owner.Email is "admissions-reception@ddvs.com" or "admissions-counselor@ddvs.com")
			.ToList();
		configuredOwners.Count.ShouldBe(2);
		var routingSettings = AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		routingSettings.IsEnabled = true;
		routingSettings.DefaultOwnerUserId = configuredOwners[0].Id;
		routingSettings.OwnerPoolUserIds.AddRange(configuredOwners.Select(owner => owner.Id));

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			originalCycle.LeadScoringRules,
			routingSettings);

		try
		{
			var response = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/workspace/staff?academicYearId={Factory.SeededAcademicYearId}&ownerOnly=true&maxResultCount=25");

			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var owners = await response.Content.ReadFromJsonAsync<List<AdmissionCrmStaffLookupDto>>(JsonOptions);
			owners.ShouldNotBeNull();
			owners.Count.ShouldBe(2);
			owners.ShouldAllBe(owner =>
				owner.EmployeeId != Guid.Empty &&
				!string.IsNullOrWhiteSpace(owner.EmployeeCode) &&
				owner.IsOwnerEligible);
			owners.Select(owner => owner.Email).ShouldContain("admissions-reception@ddvs.com");
			owners.Select(owner => owner.Email).ShouldContain("admissions-counselor@ddvs.com");
			owners.Select(owner => owner.Email).ShouldNotContain("admissions-approver@ddvs.com");
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

}
