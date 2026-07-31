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
public class AdmissionCrmApi_Tests : ShikshaMitraApiTestBase
{
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

	public AdmissionCrmApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
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
		opened.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);
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
		var originalCycle = await GetCrmCycleConfigurationAsync();
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
		var activeRoute = await CreateRouteDefinitionAsync(
			AdmissionCrmRouteType.Fresh,
			DateTime.UtcNow.AddDays(-1),
			DateTime.UtcNow.AddDays(1),
			publish: true);
		var futureRoute = await CreateRouteDefinitionAsync(
			AdmissionCrmRouteType.SiblingPriority,
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(2),
			publish: true);
		var draftRoute = await CreateRouteDefinitionAsync(
			AdmissionCrmRouteType.Lateral,
			DateTime.UtcNow.AddDays(-1),
			DateTime.UtcNow.AddDays(1),
			publish: false);

		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			channelSettings: channelSettings);

		try
		{
			using var viewClient = CreateClientWithCrmPermissions(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
			var optionsResponse = await viewClient.GetAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/quick-inquiry-options");
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

			var createResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Fresh admission pathway snapshot",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Route",
					PrimaryContactNo = "9876543202",
					IntendedRouteDefinitionId = activeRoute.Id
				},
				JsonOptions);
			createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
			var created = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			created.ShouldNotBeNull();
			created.IntendedRouteType.ShouldBe(AdmissionCrmRouteType.Fresh);
			created.IntendedRouteDefinitionId.ShouldBe(activeRoute.Id);
			created.IntendedRouteDefinitionVersion.ShouldBe(activeRoute.Version);
			created.IntendedRouteNameSnapshot.ShouldBe(activeRoute.Name);

			var futureCreateResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Future route must not be assignable",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Future",
					PrimaryContactNo = "9876543203",
					IntendedRouteDefinitionId = futureRoute.Id
				},
				JsonOptions);
			futureCreateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await futureCreateResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:PublishedRouteDefinitionRequired");

			var mismatchResponse = await Client.PostAsJsonAsync(
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
				JsonOptions);
			mismatchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await mismatchResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:IntendedRouteMismatch");

			var arbitraryEnumResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "An enum alone must not create a pathway snapshot",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					FirstName = "Enum",
					PrimaryContactNo = "9876543207",
					IntendedRouteType = AdmissionCrmRouteType.Lateral
				},
				JsonOptions);
			arbitraryEnumResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await arbitraryEnumResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:PublishedRouteDefinitionRequired");

			var missingFirstNameResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "Staff-assisted capture requires applicant identity",
					Channel = AdmissionInquiryChannel.QuickCallEntry,
					PrimaryContactNo = "9876543208",
					IntendedRouteDefinitionId = activeRoute.Id
				},
				JsonOptions);
			missingFirstNameResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await missingFirstNameResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:FirstNameRequired");

			var externalChannelBypassResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
				new CreateAdmissionCrmLeadDto
				{
					AdmissionIntent = "An authenticated staff endpoint cannot impersonate a public form",
					Channel = AdmissionInquiryChannel.SchoolWebsiteForm,
					PrimaryContactNo = "9876543209",
					IntendedRouteDefinitionId = activeRoute.Id
				},
				JsonOptions);
			externalChannelBypassResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await externalChannelBypassResponse.Content.ReadAsStringAsync())
				.ShouldContain("AdmissionCrm:FirstNameRequired");
		}
		finally
		{
			await RetireRouteDefinitionAsync(activeRoute.Id);
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				channelSettings: originalCycle.ChannelSettings);
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
		var contactNo = "9898012345";
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
		var contactNo = "9898098765";
		var target = await CreateCrmLeadWithContactAsync(
			"Primary duplicate merge inquiry",
			contactNo,
			AdmissionInquiryChannel.WalkInReception);
		var duplicate = await CreateCrmLeadWithContactAsync(
			"Duplicate merge inquiry",
			contactNo,
			AdmissionInquiryChannel.QuickCallEntry);

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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads?priority={(int)AdmissionCrmLeadPriority.Hot}&maxResultCount=200");
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/message-templates?channel={(int)AdmissionCrmMessageChannel.WhatsApp}&filterText=Entrance");
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/opt-outs?leadId={lead.Id}&channel={(int)AdmissionCrmMessageChannel.WhatsApp}&isActive=true&contactSearch={lead.PrimaryContactNo}");
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/opt-outs?channel={(int)AdmissionCrmMessageChannel.WhatsApp}&isActive=false&contactSearch={lead.PrimaryContactNo}");
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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
		var invalidCallbackResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Status = "delivered",
				StatusDetails = "Provider delivered the message.",
				ProviderMessageId = "provider-message-1",
				CallbackSecret = "wrong-secret"
			},
			JsonOptions);
		invalidCallbackResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var invalidCallbackBody = await invalidCallbackResponse.Content.ReadAsStringAsync();
		invalidCallbackBody.ShouldContain("AdmissionCrm:DeliveryCallbackSecretInvalid");

		var callbackResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Status = "delivered",
				StatusDetails = "Provider delivered the message.",
				ProviderMessageId = "provider-message-1",
				CallbackSecret = callbackSecret
			},
			JsonOptions);
		callbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await callbackResponse.Content.ReadAsStringAsync());
		var deliveredLog = await callbackResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		deliveredLog.ShouldNotBeNull();
		deliveredLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.DeliveredStatus);
		deliveredLog.ProviderMessageId.ShouldBe("provider-message-1");
		deliveredLog.AttemptCount.ShouldBe(queuedLog.AttemptCount);
		deliveredLog.DeliveredAt.ShouldNotBeNull();
		deliveredLog.StatusDetails.ShouldContain("provider-message-1");

		var lateSentCallbackResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{queuedLog.Id}/callback",
			new ApplyAdmissionCrmDeliveryCallbackDto
			{
				Status = "sent",
				StatusDetails = "Provider sent callback arrived late.",
				ProviderMessageId = "provider-message-late",
				CallbackSecret = callbackSecret
			},
			JsonOptions);
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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
						ProviderMessageIdPath = "message.id",
						CallbackSecretPath = "auth.secret"
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

		var mappedCallbackResponse = await anonymousClient.PostAsJsonAsync(
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
			JsonOptions);
		mappedCallbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await mappedCallbackResponse.Content.ReadAsStringAsync());
		var mappedFailedLog = await mappedCallbackResponse.Content.ReadFromJsonAsync<AdmissionCrmMessageDeliveryLogDto>(JsonOptions);
		mappedFailedLog.ShouldNotBeNull();
		mappedFailedLog.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.FailedStatus);
		mappedFailedLog.ProviderMessageId.ShouldBe("provider-message-mapped");
		mappedFailedLog.StatusDetails.ShouldContain("Mapped provider reported failure.");
		mappedFailedLog.StatusDetails.ShouldContain("provider-message-mapped");

		var providerMessageCallbackResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/communication/{Factory.SeededAcademicYearId}/{(int)AdmissionCrmMessageChannel.WhatsApp}/delivery-callback",
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
			JsonOptions);
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.WhatsApp}",
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

		var submitResponse = await anonymousClient.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/forms/{template.Id}/submit",
			new PublicAdmissionInquirySubmitDto
			{
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
			},
			JsonOptions);

		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var result = await submitResponse.Content.ReadFromJsonAsync<PublicAdmissionInquirySubmitResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.LeadId.ShouldNotBe(Guid.Empty);
		result.StatusToken.ShouldNotBeNullOrWhiteSpace();
		result.StatusPath.ShouldContain(result.StatusToken);
		result.Stage.ShouldBe(AdmissionInquiryStage.NewInquiry);

		await AssertPublicSubmissionPersistedAsync(result.LeadId);
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
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(originalCycle, originalCycle.ExamReadinessFields);
		}
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/providers/{(int)AdmissionCrmMessageChannel.Sms}",
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
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/communication/delivery-logs?leadId={submit.LeadId}&channel={(int)AdmissionCrmMessageChannel.Sms}");
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

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Assign_Recalculate_Archive_And_Reopen_Crm_Lead()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);

		var assignResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = ownerUserId },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignResponse.Content.ReadAsStringAsync());
		var assigned = await assignResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		assigned.ShouldNotBeNull();
		assigned.OwnerUserId.ShouldBe(ownerUserId);

		var scoreResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/score/recalculate",
			content: null);
		scoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scoreResponse.Content.ReadAsStringAsync());
		var scored = await scoreResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		scored.ShouldNotBeNull();
		scored.LeadScore.ShouldBeGreaterThan(0);

		var archiveResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/archive",
			new ArchiveAdmissionCrmLeadDto { Reason = "Family is not ready this cycle." },
			JsonOptions);
		archiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await archiveResponse.Content.ReadAsStringAsync());
		var archived = await archiveResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		archived.ShouldNotBeNull();
		archived.Stage.ShouldBe(AdmissionInquiryStage.Archived);
		archived.ArchiveReason.ShouldBe("Family is not ready this cycle.");

		var reopenResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/reopen",
			new ReopenAdmissionCrmLeadDto { Reason = "Parent called back for follow-up." },
			JsonOptions);
		reopenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reopenResponse.Content.ReadAsStringAsync());
		var reopened = await reopenResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		reopened.ShouldNotBeNull();
		reopened.Stage.ShouldBe(AdmissionInquiryStage.FollowUp);
		reopened.ReopenReason.ShouldBe("Parent called back for follow-up.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_List_And_Complete_Follow_Up_Task()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 5");

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks",
			new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = "Call parent for document readiness",
				DueAt = DateTime.Now.AddDays(1),
				Notes = "Ask for previous school report card."
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var task = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmFollowUpTaskDto>(JsonOptions);
		task.ShouldNotBeNull();
		task.LeadId.ShouldBe(lead.Id);
		task.Status.ShouldBe(AdmissionCrmTaskStatus.Pending);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		tasks.Items.Select(x => x.Id).ShouldContain(task.Id);

		var completeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/tasks/{task.Id}/complete",
			new CompleteAdmissionCrmFollowUpTaskDto { CompletionNotes = "Parent confirmed availability." },
			JsonOptions);
		completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await completeResponse.Content.ReadAsStringAsync());
		var completed = await completeResponse.Content.ReadFromJsonAsync<AdmissionCrmFollowUpTaskDto>(JsonOptions);
		completed.ShouldNotBeNull();
		completed.Status.ShouldBe(AdmissionCrmTaskStatus.Completed);
		completed.CompletionNotes.ShouldBe("Parent confirmed availability.");
		completed.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Use_Stage_Sla_Target_For_Sla_Reminder_Task()
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
			var updateStageResponse = await PutPipelineStageWithImpactAsync(
				Factory.SeededAcademicYearId,
				newInquiryStage.Id,
				new UpdateAdmissionCrmPipelineStageDto
				{
					StageName = newInquiryStage.StageName,
					DisplayOrder = newInquiryStage.DisplayOrder,
					IsEnabled = true,
					ColorHex = newInquiryStage.ColorHex,
					SlaTargetHours = 8,
					OwnerResponsibility = newInquiryStage.OwnerResponsibility,
					AllowedNextStages = [.. newInquiryStage.AllowedNextStages]
				});
			updateStageResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateStageResponse.Content.ReadAsStringAsync());

			var lead = await CreateCrmLeadAsync("Stage SLA inquiry for Grade 5");
			await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-2));

			var runResponse = await Client.PostAsync(
				$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
				content: null);
			runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
			var result = await runResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.StageOverrideCount.ShouldBeGreaterThanOrEqualTo(1);

			var listResponse = await Client.GetAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
			listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
			var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
			tasks.ShouldNotBeNull();
			var slaTask = tasks.Items.Single(x => x.Title == "SLA follow-up required");
			slaTask.Notes.ShouldNotBeNull();
			slaTask.Notes!.ShouldContain("after 8 hours without lead activity in NewInquiry");

			var analytics = await GetCrmAnalyticsSummaryAsync();
			analytics.SlaAnalytics.FirstResponseSlaHours.ShouldBe(8);
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
	public async Task Should_Create_Sla_Reminder_Task_And_Skip_Duplicate_Pending_Reminder()
	{
		var lead = await CreateCrmLeadAsync("SLA inquiry for Grade 5");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-5));

		var runResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
		var result = await runResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.CreatedTaskCount.ShouldBeGreaterThanOrEqualTo(1);
		result.SlaThresholdHours.ShouldBe(24);
		result.SlaCutoff.ShouldBeLessThan(DateTimeOffset.UtcNow);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		var slaTask = tasks.Items.Single(x => x.Title == "SLA follow-up required");
		slaTask.Status.ShouldBe(AdmissionCrmTaskStatus.Pending);
		slaTask.Notes.ShouldNotBeNull();
		slaTask.Notes!.ShouldContain("Generated by admissions CRM SLA automation");

		var secondRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		secondRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondRunResponse.Content.ReadAsStringAsync());
		var secondResult = await secondRunResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		secondResult.ShouldNotBeNull();
		secondResult.CreatedTaskCount.ShouldBe(0);
		secondResult.SkippedExistingTaskCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Create_Stale_Owner_Notification_Task_When_Lead_Becomes_Stale()
	{
		var lead = await CreateCrmLeadAsync("Stale inquiry for Grade 5");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-31));

		var runResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/stale-archive/run",
			content: null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
		var result = await runResponse.Content.ReadFromJsonAsync<AdmissionCrmStaleArchiveRunResultDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.MarkedStaleCount.ShouldBeGreaterThanOrEqualTo(1);
		result.CreatedOwnerNotificationTaskCount.ShouldBeGreaterThanOrEqualTo(1);

		var getLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		getLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getLeadResponse.Content.ReadAsStringAsync());
		var staleLead = await getLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staleLead.ShouldNotBeNull();
		staleLead.Stage.ShouldBe(AdmissionInquiryStage.Stale);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var tasks = await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		var staleTasks = tasks.Items.Where(x => x.Title == "Stale lead follow-up required").ToList();
		staleTasks.Count.ShouldBe(1);
		staleTasks[0].Status.ShouldBe(AdmissionCrmTaskStatus.Pending);
		staleTasks[0].Notes.ShouldNotBeNull();
		staleTasks[0].Notes!.ShouldContain("Generated by admissions CRM stale automation");

		var secondRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/stale-archive/run",
			content: null);
		secondRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondRunResponse.Content.ReadAsStringAsync());

		var secondListResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=10");
		secondListResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondListResponse.Content.ReadAsStringAsync());
		var secondTasks = await secondListResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		secondTasks.ShouldNotBeNull();
		secondTasks.Items.Count(x => x.Title == "Stale lead follow-up required").ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Run_Stale_And_Sla_Rules_From_Background_Automation_Path()
	{
		var lead = await CreateCrmLeadAsync("Background automation inquiry for Grade 5");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-31));

		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leadAppService = scope.ServiceProvider.GetRequiredService<IAdmissionCrmLeadAppService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var staleResult = await leadAppService.RunStaleArchiveRulesFromAutomationAsync(
				Factory.SeededAcademicYearId);
			var slaResult = await leadAppService.RunSlaReminderRulesFromAutomationAsync(
				Factory.SeededAcademicYearId);
			await uow.CompleteAsync();

			staleResult.MarkedStaleCount.ShouldBeGreaterThanOrEqualTo(1);
			staleResult.CreatedOwnerNotificationTaskCount.ShouldBeGreaterThanOrEqualTo(1);
			slaResult.CreatedTaskCount.ShouldBeGreaterThanOrEqualTo(1);
		}

		var getLeadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		getLeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getLeadResponse.Content.ReadAsStringAsync());
		var staleLead = await getLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		staleLead.ShouldNotBeNull();
		staleLead.Stage.ShouldBe(AdmissionInquiryStage.Stale);

		var analytics = await GetCrmAnalyticsSummaryAsync();
		analytics.AutomationAnalytics.RunCount.ShouldBeGreaterThanOrEqualTo(2);
		analytics.AutomationAnalytics.RecentRuns.Any(x =>
			x.RunType == AdmissionCrmAutomationRunType.StaleArchive &&
			x.MarkedStaleCount >= 1 &&
			x.CreatedTaskCount >= 1).ShouldBeTrue();
		analytics.AutomationAnalytics.RecentRuns.Any(x =>
			x.RunType == AdmissionCrmAutomationRunType.SlaReminder &&
			x.CreatedTaskCount >= 1).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Pause_And_Resume_Working_Time_Sla_With_Audit_History()
	{
		var lead = await CreateCrmLeadAsync("SLA hold inquiry for Grade 6");
		await AgeCrmLeadActivityAsync(lead.Id, DateTimeOffset.UtcNow.AddDays(-31));

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/sla-holds",
			new CreateAdmissionCrmSlaHoldDto
			{
				HoldReason = AdmissionCrmSlaHoldReason.ExternalAuthority,
				Reason = "Awaiting a governed certificate from the issuing authority."
			}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var hold = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaHoldDto>(JsonOptions);
		hold.ShouldNotBeNull();
		hold.IsActive.ShouldBeTrue();

		var pausedRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		pausedRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pausedRunResponse.Content.ReadAsStringAsync());
		var pausedRun = await pausedRunResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		pausedRun.ShouldNotBeNull();
		pausedRun.PausedLeadCount.ShouldBeGreaterThanOrEqualTo(1);

		var pausedTasksResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks?maxResultCount=20");
		var pausedTasks = await pausedTasksResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		pausedTasks.ShouldNotBeNull();
		pausedTasks.Items.ShouldNotContain(x => x.Title.Contains("SLA", StringComparison.OrdinalIgnoreCase));

		var resumeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/sla-holds/{hold.Id}/resume",
			new ResumeAdmissionCrmSlaHoldDto { Reason = "Authority evidence received and verified." }, JsonOptions);
		resumeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resumeResponse.Content.ReadAsStringAsync());

		var resumedRunResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/automation/sla-reminders/run",
			content: null);
		resumedRunResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resumedRunResponse.Content.ReadAsStringAsync());
		var resumedRun = await resumedRunResponse.Content.ReadFromJsonAsync<AdmissionCrmSlaReminderRunResultDto>(JsonOptions);
		resumedRun.ShouldNotBeNull();
		resumedRun.CreatedTaskCount.ShouldBeGreaterThanOrEqualTo(1);

		var historyResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/sla-holds");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = await historyResponse.Content.ReadFromJsonAsync<List<AdmissionCrmSlaHoldDto>>(JsonOptions);
		history.ShouldNotBeNull();
		history.ShouldContain(x => x.Id == hold.Id && !x.IsActive && x.ResumeReason != null);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Record_Crm_Timeline_For_Lead_Task_And_Exam_Actions()
	{
		var lead = await CreateCrmLeadAsync("Timeline inquiry for Grade 4");
		var createTaskResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/tasks",
			new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = "Timeline parent follow-up",
				DueAt = DateTime.Now.AddDays(1)
			},
			JsonOptions);
		createTaskResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createTaskResponse.Content.ReadAsStringAsync());
		var task = await createTaskResponse.Content.ReadFromJsonAsync<AdmissionCrmFollowUpTaskDto>(JsonOptions);
		task.ShouldNotBeNull();

		var completeTaskResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/tasks/{task.Id}/complete",
			new CompleteAdmissionCrmFollowUpTaskDto { CompletionNotes = "Timeline follow-up completed." },
			JsonOptions);
		completeTaskResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await completeTaskResponse.Content.ReadAsStringAsync());

		var slot = await CreateAdmissionCrmExamSlotAsync("Timeline", studentLimit: 2);
		await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		var eventTypes = timeline.Items.Select(x => x.EventType).ToList();
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.LeadCreated);
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.TaskCreated);
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.TaskCompleted);
		eventTypes.ShouldContain(AdmissionCrmTimelineEventType.ExamScheduled);

		var leadCreatedEvent = timeline.Items.Single(x => x.EventType == AdmissionCrmTimelineEventType.LeadCreated);
		leadCreatedEvent.Details.ShouldNotBeNull();
		leadCreatedEvent.Details.ShouldContain("Channel: Walk-in");
		leadCreatedEvent.Details.ShouldNotContain(nameof(AdmissionInquiryChannel.WalkInReception));

		var scheduleEvent = timeline.Items.Single(x => x.EventType == AdmissionCrmTimelineEventType.ExamScheduled);
		scheduleEvent.Title.ShouldBe("Entrance exam scheduled");
		scheduleEvent.RelatedEntityId.ShouldNotBeNull();
		scheduleEvent.ActorUserId.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Schedule_Crm_Entrance_Exam_From_Exam_Ready_Lead()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var slot = await CreateAdmissionCrmExamSlotAsync("Exam ready", studentLimit: 2);

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var exam = await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		exam.LeadId.ShouldBe(lead.Id);
		exam.EntranceExamSlotId.ShouldBe(slot.Id);
		exam.ExamRoomNo.ShouldBe(slot.RoomNo);
		exam.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.NotMarked);
		exam.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pending);
		exam.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Pending);

		var searchResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/exams?filterText={Uri.EscapeDataString(exam.ApplicantName)}&maxResultCount=50");
		searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await searchResponse.Content.ReadAsStringAsync());
		var searchResult = await searchResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmEntranceExamDto>>(JsonOptions);
		searchResult.ShouldNotBeNull();
		searchResult.Items.ShouldContain(x => x.Id == exam.Id);

		var leadResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var scheduledLead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		scheduledLead.ShouldNotBeNull();
		scheduledLead.Stage.ShouldBe(AdmissionInquiryStage.ExamScheduled);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preserve_Previous_Slot_Timeline_When_Entrance_Exam_Is_Rescheduled()
	{
		var lead = await CreateCrmLeadAsync("Entrance exam reschedule inquiry");
		var originalSlot = await CreateAdmissionCrmExamSlotAsync("Original slot", studentLimit: 2);
		var replacementSlot = await CreateAdmissionCrmExamSlotAsync("Replacement slot", studentLimit: 2);

		var originalExam = await ScheduleCrmEntranceExamAsync(lead.Id, originalSlot.Id);
		var rescheduledExam = await ScheduleCrmEntranceExamAsync(lead.Id, replacementSlot.Id);

		rescheduledExam.Id.ShouldBe(originalExam.Id);
		rescheduledExam.EntranceExamSlotId.ShouldBe(replacementSlot.Id);
		rescheduledExam.ExamRoomNo.ShouldBe(replacementSlot.RoomNo);

		var timeline = await GetCrmTimelineAsync(lead.Id);
		var scheduleEvents = timeline.Items
			.Where(x => x.EventType == AdmissionCrmTimelineEventType.ExamScheduled)
			.ToList();
		originalSlot.SlotName.ShouldNotBeNull();
		replacementSlot.SlotName.ShouldNotBeNull();
		scheduleEvents.Count.ShouldBe(2);
		scheduleEvents.ShouldContain(x =>
			x.Title == "Entrance exam scheduled" &&
			x.Details != null &&
			x.Details.Contains(originalSlot.SlotName, StringComparison.Ordinal));
		scheduleEvents.ShouldContain(x =>
			x.Title == "Entrance exam rescheduled" &&
			x.Details != null &&
			x.Details.Contains(replacementSlot.SlotName, StringComparison.Ordinal));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Crm_Registration_Form_Before_Exam_When_Configured()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			registrationFormRequiredBeforeExam: true);

		try
		{
			var lead = await CreateCrmLeadAsync("Registration form gated inquiry for Grade 4");
			var slot = await CreateAdmissionCrmExamSlotAsync("Registration gate", studentLimit: 1);

			var blockedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
				new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
				JsonOptions);

			blockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			var blockedBody = await blockedResponse.Content.ReadAsStringAsync();
			blockedBody.ShouldContain("AdmissionCrm:RegistrationFormRequiredBeforeExam");

			var formNumber = "CRM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
			var issueResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/registration-form",
				new IssueAdmissionCrmRegistrationFormDto
				{
					FormNumber = formNumber,
					Amount = 250
				},
				JsonOptions);

			issueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await issueResponse.Content.ReadAsStringAsync());
			var issuedLead = await issueResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
			issuedLead.ShouldNotBeNull();
			issuedLead.RegistrationFormNumber.ShouldBe(formNumber);
			issuedLead.RegistrationFormAmount.ShouldBe(250D);
			issuedLead.RegistrationFormIssuedAt.ShouldNotBeNull();

			var timeline = await GetCrmTimelineAsync(lead.Id);
			var issueEvent = timeline.Items.Single(x =>
				x.EventType == AdmissionCrmTimelineEventType.RegistrationFormIssued);
			issueEvent.Title.ShouldBe("Registration form issued");
			issueEvent.Details!.ShouldContain(formNumber);

			var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);
			exam.LeadId.ShouldBe(lead.Id);
			exam.EntranceExamSlotId.ShouldBe(slot.Id);
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(
				originalCycle,
				originalCycle.ExamReadinessFields,
				registrationFormRequiredBeforeExam: originalCycle.RegistrationFormRequiredBeforeExam);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Run_Exam_Lifecycle_Journeys_When_Crm_Exam_Changes()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		var scheduledTaskTitle = "Exam scheduled journey " + Guid.NewGuid().ToString("N")[..8];
		var stageTaskTitle = "Exam stage journey " + Guid.NewGuid().ToString("N")[..8];
		var completedTaskTitle = "Exam completed journey " + Guid.NewGuid().ToString("N")[..8];
		var eligibilityTaskTitle = "Eligibility journey " + Guid.NewGuid().ToString("N")[..8];
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields,
			journeyRules:
			[
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Exam scheduled event journey",
					IsEnabled = true,
					Priority = 10,
					Trigger = AdmissionCrmJourneyTrigger.ExamScheduled,
					CreateTask = true,
					TaskTitle = scheduledTaskTitle,
					TaskDueOffsetHours = 1
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Exam scheduled stage journey",
					IsEnabled = true,
					Priority = 20,
					Trigger = AdmissionCrmJourneyTrigger.StageReached,
					Stage = AdmissionInquiryStage.ExamScheduled,
					CreateTask = true,
					TaskTitle = stageTaskTitle,
					TaskDueOffsetHours = 1
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Exam completed event journey",
					IsEnabled = true,
					Priority = 30,
					Trigger = AdmissionCrmJourneyTrigger.ExamCompleted,
					CreateTask = true,
					TaskTitle = completedTaskTitle,
					TaskDueOffsetHours = 1
				},
				new AdmissionCrmJourneyRuleDto
				{
					RuleName = "Eligibility decision event journey",
					IsEnabled = true,
					Priority = 40,
					Trigger = AdmissionCrmJourneyTrigger.EligibilityDecided,
					CreateTask = true,
					TaskTitle = eligibilityTaskTitle,
					TaskDueOffsetHours = 1
				}
			]);

		try
		{
			var lead = await CreateCrmLeadAsync("Admission journey exam inquiry for Grade 4");
			var slot = await CreateAdmissionCrmExamSlotAsync("Journey lifecycle", studentLimit: 1);
			var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

			var scheduledTasks = await GetCrmTasksAsync(lead.Id);
			scheduledTasks.Items.Select(x => x.Title).ShouldContain(scheduledTaskTitle);
			scheduledTasks.Items.Select(x => x.Title).ShouldContain(stageTaskTitle);

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
			replay.CreatedTaskCount.ShouldBe(0);
			replay.SkippedExistingTaskCount.ShouldBe(1);

			var resultResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
				new UploadAdmissionCrmEntranceExamResultDto
				{
					TotalMarks = 100,
					PassingMarks = 40,
					ObtainedMarks = 78,
					MeritRank = 1
				},
				JsonOptions);
			resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
			var result = await resultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pass);

			var resultTasks = await GetCrmTasksAsync(lead.Id);
			resultTasks.Items.Select(x => x.Title).ShouldContain(completedTaskTitle);

			var decisionResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
				new UpdateAdmissionCrmEligibilityDecisionDto
				{
					Decision = AdmissionCrmEligibilityDecision.Waitlisted,
					Reason = "Eligible but waitlisted by journey test.",
					MeritRank = 1
				},
				JsonOptions);
			decisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await decisionResponse.Content.ReadAsStringAsync());

			var finalTasks = await GetCrmTasksAsync(lead.Id);
			finalTasks.Items.Select(x => x.Title).ShouldContain(eligibilityTaskTitle);
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
	public async Task Should_Use_Cycle_Configured_Exam_Readiness_Fields()
	{
		var originalCycle = await GetCrmCycleConfigurationAsync();
		await UpdateCrmCycleConfigurationAsync(
			originalCycle,
			originalCycle.ExamReadinessFields | AdmissionCrmExamReadinessField.ParentGuardianName);

		try
		{
			var lead = await CreateCrmLeadForClassAsync(
				"Strict readiness inquiry",
				"Strict readiness " + Guid.NewGuid().ToString("N")[..8],
				omitParentGuardianName: true);
			var slot = await CreateAdmissionCrmExamSlotAsync("Strict readiness", studentLimit: 1);

			var blockedResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/exam/schedule",
				new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
				JsonOptions);

			blockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
			var blockedBody = await blockedResponse.Content.ReadAsStringAsync();
			blockedBody.ShouldContain("AdmissionCrm:ExamReadinessIncomplete");
			blockedBody.ShouldContain("Parent or guardian name");
		}
		finally
		{
			await UpdateCrmCycleConfigurationAsync(originalCycle, originalCycle.ExamReadinessFields);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Recalculate_Class_Wise_Merit_Ranks_When_Result_Uploaded()
	{
		var desiredClassName = "Merit Class " + Guid.NewGuid().ToString("N")[..8];
		var firstLead = await CreateCrmLeadForClassAsync("Merit ranking inquiry one", desiredClassName);
		var secondLead = await CreateCrmLeadForClassAsync("Merit ranking inquiry two", desiredClassName);
		var slot = await CreateAdmissionCrmExamSlotAsync("Merit ranking", studentLimit: 2);
		var firstExam = await ScheduleCrmEntranceExamAsync(firstLead.Id, slot.Id);
		var secondExam = await ScheduleCrmEntranceExamAsync(secondLead.Id, slot.Id);

		var firstResultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{firstExam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 82
			},
			JsonOptions);
		firstResultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResultResponse.Content.ReadAsStringAsync());
		var firstResult = await firstResultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		firstResult.ShouldNotBeNull();
		firstResult.MeritRank.ShouldBe(1);

		var secondResultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{secondExam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 91
			},
			JsonOptions);
		secondResultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondResultResponse.Content.ReadAsStringAsync());
		var secondResult = await secondResultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		secondResult.ShouldNotBeNull();
		secondResult.MeritRank.ShouldBe(1);

		var updatedFirstExam = await GetCrmEntranceExamByLeadAsync(firstLead.Id);
		updatedFirstExam.MeritRank.ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Prevent_Crm_Exam_Overbooking_Unless_Override_Recorded()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Capacity", studentLimit: 1);
		var firstLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var secondLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");

		var firstResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());

		var blockedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slot.Id },
			JsonOptions);
		blockedResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var blockedBody = await blockedResponse.Content.ReadAsStringAsync();
		blockedBody.ShouldContain("AdmissionCrm:ExamSlotCapacityFull");

		var overrideResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto
			{
				EntranceExamSlotId = slot.Id,
				OverrideCapacity = true,
				CapacityOverrideReason = "Principal approved extra chair for sibling applicant."
			},
			JsonOptions);
		overrideResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideResponse.Content.ReadAsStringAsync());
		var exam = await overrideResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		exam.CapacityOverrideUsed.ShouldBeTrue();
		exam.CapacityOverrideReason.ShouldBe("Principal approved extra chair for sibling applicant.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Override_Permissions_For_Crm_Exam_Capacity_And_Eligibility()
	{
		var slot = await CreateAdmissionCrmExamSlotAsync("Permission capacity", studentLimit: 1);
		var firstLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var secondLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		await ScheduleCrmEntranceExamAsync(firstLead.Id, slot.Id);

		using var scheduleOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam);
		var capacityOverrideResponse = await scheduleOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/exam/schedule",
			new ScheduleAdmissionCrmEntranceExamDto
			{
				EntranceExamSlotId = slot.Id,
				OverrideCapacity = true,
				CapacityOverrideReason = "Permission gate capacity override attempt."
			},
			JsonOptions);

		capacityOverrideResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await capacityOverrideResponse.Content.ReadAsStringAsync());

		var resultLead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var resultSlot = await CreateAdmissionCrmExamSlotAsync("Permission eligibility", studentLimit: 1);
		var exam = await ScheduleCrmEntranceExamAsync(resultLead.Id, resultSlot.Id);
		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 35
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());

		using var resultOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		var eligibilityOverrideResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Permission gate eligibility override attempt.",
				OverrideDecision = true,
				OverrideReason = "Principal override should require explicit permission."
			},
			JsonOptions);

		eligibilityOverrideResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await eligibilityOverrideResponse.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Govern_Individual_Crm_Exam_Result_Corrections()
	{
		var lead = await CreateCrmLeadAsync("Individual result correction governance");
		var slot = await CreateAdmissionCrmExamSlotAsync(
			"Individual result correction governance",
			studentLimit: 1);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);
		var initialResult = new UploadAdmissionCrmEntranceExamResultDto
		{
			TotalMarks = 100,
			PassingMarks = 40,
			ObtainedMarks = 68
		};
		var initialResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			initialResult,
			JsonOptions);
		initialResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await initialResponse.Content.ReadAsStringAsync());

		using var resultOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		var identicalRetryResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			initialResult,
			JsonOptions);
		identicalRetryResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await identicalRetryResponse.Content.ReadAsStringAsync());

		var missingReasonResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72
			},
			JsonOptions);
		missingReasonResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingReasonResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ExamResultCorrectionReasonRequired");

		var missingOverridePermissionResponse = await resultOnlyClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				CorrectionReason = "Marks rechecked against the signed answer sheet."
			},
			JsonOptions);
		missingOverridePermissionResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await missingOverridePermissionResponse.Content.ReadAsStringAsync());

		using var overrideClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult);
		var correctionResponse = await overrideClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				CorrectionReason = "Marks rechecked against the signed answer sheet."
			},
			JsonOptions);
		correctionResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await correctionResponse.Content.ReadAsStringAsync());
		var corrected = await correctionResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		corrected.ShouldNotBeNull();
		corrected.ObtainedMarks.ShouldBe(72);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Record_Crm_Exam_Attendance_Result_And_Waitlist_Decision()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var slot = await CreateAdmissionCrmExamSlotAsync("Result", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var attendanceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/attendance",
			new MarkAdmissionCrmExamAttendanceDto
			{
				AttendanceStatus = AdmissionCrmExamAttendanceStatus.Present
			},
			JsonOptions);
		attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await attendanceResponse.Content.ReadAsStringAsync());
		var attended = await attendanceResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		attended.ShouldNotBeNull();
		attended.AttendanceStatus.ShouldBe(AdmissionCrmExamAttendanceStatus.Present);

		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 72,
				MeritRank = 3
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
		var result = await resultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.ExamStatus.ShouldBe(EntranceExamStatusEnum.Pass);
		result.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		result.Percentage.ShouldBe(72);
		result.MeritRank.ShouldBe(3);

		var eligibleHandoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility-handoff");
		eligibleHandoffResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await eligibleHandoffResponse.Content.ReadAsStringAsync());
		var eligibleHandoff = await eligibleHandoffResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEligibilityHandoffDto>(JsonOptions);
		eligibleHandoff.ShouldNotBeNull();
		eligibleHandoff.LeadId.ShouldBe(lead.Id);
		eligibleHandoff.ExamRecordId.ShouldBe(exam.Id);
		eligibleHandoff.CanStartAdmission.ShouldBeTrue();
		eligibleHandoff.RequiresManualReview.ShouldBeFalse();
		eligibleHandoff.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		eligibleHandoff.ObtainedMarks.ShouldBe(72);
		eligibleHandoff.MeritRank.ShouldBe(3);
		eligibleHandoff.PrimaryContactNo.ShouldBe(lead.PrimaryContactNo);

		var decisionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Waitlisted,
				Reason = "Eligible but waitlisted for class capacity.",
				MeritRank = 3
			},
			JsonOptions);
		decisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await decisionResponse.Content.ReadAsStringAsync());
		var decision = await decisionResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		decision.ShouldNotBeNull();
		decision.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Waitlisted);
		decision.EligibilityDecisionReason.ShouldBe("Eligible but waitlisted for class capacity.");

		var waitlistHandoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility-handoff");
		waitlistHandoffResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await waitlistHandoffResponse.Content.ReadAsStringAsync());
		var waitlistHandoff = await waitlistHandoffResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEligibilityHandoffDto>(JsonOptions);
		waitlistHandoff.ShouldNotBeNull();
		waitlistHandoff.CanStartAdmission.ShouldBeFalse();
		waitlistHandoff.RequiresManualReview.ShouldBeTrue();
		waitlistHandoff.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Waitlisted);

		var leadResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}");
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var waitlistedLead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		waitlistedLead.ShouldNotBeNull();
		waitlistedLead.Stage.ShouldBe(AdmissionInquiryStage.Waitlisted);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Show_Missing_Admission_Information_In_Crm_Eligibility_Handoff()
	{
		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Admission inquiry with incomplete admission handoff details",
				FirstName = "Incomplete",
				PrimaryContactNo = "989898" + Random.Shared.Next(1000, 9999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				DesiredClassName = "Grade 4"
			},
			JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		lead.FirstName.ShouldBe("Incomplete");
		lead.ParentGuardianName.ShouldBeNull();

		var slot = await CreateAdmissionCrmExamSlotAsync("Handoff Missing", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 84,
				MeritRank = 2
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());

		var handoffResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility-handoff");
		handoffResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await handoffResponse.Content.ReadAsStringAsync());
		var handoff = await handoffResponse.Content
			.ReadFromJsonAsync<AdmissionCrmEligibilityHandoffDto>(JsonOptions);
		handoff.ShouldNotBeNull();
		handoff.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		handoff.CanStartAdmission.ShouldBeFalse();
		handoff.RequiresManualReview.ShouldBeTrue();
		handoff.MissingAdmissionFields.ShouldContain("Applicant name");
		handoff.MissingAdmissionFields.ShouldContain("Parent or guardian name");
		handoff.MissingAdmissionFields.ShouldNotContain("Contact");
		handoff.MissingAdmissionFields.ShouldNotContain("Admission intent");
		handoff.MissingAdmissionFields.ShouldNotContain("Desired class");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Require_Override_Reason_For_Failed_Crm_Exam_Eligibility_Upgrade()
	{
		var lead = await CreateCrmLeadAsync("Admission inquiry for Grade 4");
		var slot = await CreateAdmissionCrmExamSlotAsync("Override", studentLimit: 2);
		var exam = await ScheduleCrmEntranceExamAsync(lead.Id, slot.Id);

		var resultResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/result",
			new UploadAdmissionCrmEntranceExamResultDto
			{
				TotalMarks = 100,
				PassingMarks = 40,
				ObtainedMarks = 35,
				MeritRank = 8
			},
			JsonOptions);
		resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
		var failed = await resultResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		failed.ShouldNotBeNull();
		failed.ExamStatus.ShouldBe(EntranceExamStatusEnum.Fail);
		failed.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Rejected);

		var missingOverrideResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Management approved interview-based admission.",
				MeritRank = 8
			},
			JsonOptions);
		missingOverrideResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var missingOverrideBody = await missingOverrideResponse.Content.ReadAsStringAsync();
		missingOverrideBody.ShouldContain("AdmissionCrm:EligibilityOverrideRequired");

		var missingReasonResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Management approved interview-based admission.",
				OverrideDecision = true,
				MeritRank = 8
			},
			JsonOptions);
		missingReasonResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
		var missingReasonBody = await missingReasonResponse.Content.ReadAsStringAsync();
		missingReasonBody.ShouldContain("AdmissionCrm:EligibilityOverrideReasonRequired");

		var overrideResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/exams/{exam.Id}/eligibility",
			new UpdateAdmissionCrmEligibilityDecisionDto
			{
				Decision = AdmissionCrmEligibilityDecision.Eligible,
				Reason = "Management approved interview-based admission.",
				OverrideDecision = true,
				OverrideReason = "Principal approved after interview and sibling priority.",
				MeritRank = 8
			},
			JsonOptions);
		overrideResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideResponse.Content.ReadAsStringAsync());
		var overridden = await overrideResponse.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		overridden.ShouldNotBeNull();
		overridden.EligibilityDecision.ShouldBe(AdmissionCrmEligibilityDecision.Eligible);
		overridden.EligibilityOverrideUsed.ShouldBeTrue();
		overridden.EligibilityOverrideReason.ShouldBe("Principal approved after interview and sibling priority.");
	}

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
			campaignName);
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
			$"&channel={(int)AdmissionInquiryChannel.QuickCallEntry}" +
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

	private async Task<AdmissionCrmFormTemplateDto> CreatePublicInquiryTemplateAsync(
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

	private async Task<AdmissionCrmLeadDto> CreateCrmLeadAsync(string admissionIntent)
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

	private HttpClient CreateClientWithCrmPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}

	private HttpClient CreateClientForCrmRole(string roleName)
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

	private async Task<AdmissionCrmRouteDefinition> CreateRouteDefinitionAsync(
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
			var route = new AdmissionCrmRouteDefinition(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededAcademicYearId,
				$"QI-{suffix}",
				$"Quick inquiry {routeType} {suffix}",
				routeType,
				version: 1,
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

	private async Task RetireRouteDefinitionAsync(Guid id)
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

	private static async Task AssertForbiddenAsync(Task<HttpResponseMessage> request)
	{
		var response = await request;
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	private async Task<AdmissionCrmLeadDto> CreateCrmLeadForClassAsync(
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

	private async Task<AdmissionCrmLeadDto> CreateCrmLeadWithContactAsync(
		string admissionIntent,
		string primaryContactNo,
		AdmissionInquiryChannel channel,
		string desiredClassName = "Grade 4",
		string? campaignName = null,
		string? parentGuardianName = null,
		string? campusBranchName = null,
		string? preferredCommunicationLanguage = null)
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
				DesiredClassName = desiredClassName
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var lead = await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		return lead;
	}

	private async Task<AdmissionCrmMessageTemplateDto> CreateApprovedCrmMessageTemplateAsync(
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

	private async Task<PagedResultDto<AdmissionCrmMessageDeliveryLogDto>> GetCrmDeliveryLogsAsync(
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

	private async Task<AdmissionCrmMessageTemplateDto> CreateDraftCrmMessageTemplateAsync()
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

	private async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetCrmTimelineAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/timeline?maxResultCount=20");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var timeline = await response.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmTimelineEventDto>>(JsonOptions);
		timeline.ShouldNotBeNull();
		return timeline;
	}

	private async Task<PagedResultDto<AdmissionCrmFollowUpTaskDto>> GetCrmTasksAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/tasks?maxResultCount=20");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var tasks = await response.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmFollowUpTaskDto>>(JsonOptions);
		tasks.ShouldNotBeNull();
		return tasks;
	}

	private async Task<AdmissionCrmAnalyticsSummaryDto> GetCrmAnalyticsSummaryAsync()
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/analytics/summary");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var summary = await response.Content.ReadFromJsonAsync<AdmissionCrmAnalyticsSummaryDto>(JsonOptions);
		summary.ShouldNotBeNull();
		return summary;
	}

	private async Task AgeCrmLeadActivityAsync(Guid leadId, DateTimeOffset lastActivityTime)
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

	private async Task MarkCrmLeadStaleAsync(Guid leadId)
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

	private async Task AgeCrmLeadCreationAsync(Guid leadId, DateTime creationTime)
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

	private async Task<AdmissionCrmCycleConfigurationDto> GetCrmCycleConfigurationAsync()
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/cycle");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var cycle = await response.Content.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		cycle.ShouldNotBeNull();
		return cycle;
	}

	private async Task SetCrmAutomationReferencesAsync(
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

	private async Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultCycleAsync(
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

	private async Task<AdmissionCrmInquirySourceDto> CreateInquirySourceAsync(string displayName)
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

	private async Task ResetSchoolDefaultCycleAsync()
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

	private async Task<Guid> CreateAcademicYearAsync()
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

	private async Task<AdmissionCrmCycleConfigurationDto> UpdateCrmCycleConfigurationAsync(
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

	private async Task<HttpResponseMessage> PutCycleConfigurationWithImpactAsync(
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

	private async Task<HttpResponseMessage> PutPipelineStageWithImpactAsync(
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

	private async Task<AdmissionCrmEntranceExamDto> GetCrmEntranceExamByLeadAsync(Guid leadId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/exam");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var exam = await response.Content.ReadFromJsonAsync<AdmissionCrmEntranceExamDto>(JsonOptions);
		exam.ShouldNotBeNull();
		return exam;
	}

	private async Task<AdmissionCrmEntranceExamDto> ScheduleCrmEntranceExamAsync(
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

	private async Task<AdmissionCrmExamSlotDto> CreateAdmissionCrmExamSlotAsync(
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

	private async Task AssertPublicSubmissionPersistedAsync(Guid leadId)
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

	private async Task AssertPublicSubmissionContainsAsync(Guid leadId, string fieldKey)
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

	private async Task<string> GetEncryptedOtpCodeForLeadAsync(Guid leadId)
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

	private async Task<int> GetStatusOtpFailedAttemptCountForLeadAsync(Guid leadId)
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
