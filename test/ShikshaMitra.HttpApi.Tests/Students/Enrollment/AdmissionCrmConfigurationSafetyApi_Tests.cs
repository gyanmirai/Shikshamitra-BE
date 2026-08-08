using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmConfigurationSafetyApi_Tests : ShikshaMitraApiTestBase
{
	private static int _academicYearSequence;

	public AdmissionCrmConfigurationSafetyApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Version_Admission_Year_And_Project_Applicant_Progress()
	{
		var academicYearId = (await CreateAcademicYearPairAsync()).SourceYearId;
		var hierarchy = await CreateAcademicHierarchyAsync(academicYearId);

		var optionsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/{academicYearId}/standard-options");
		optionsResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await optionsResponse.Content.ReadAsStringAsync());
		var options = await optionsResponse.Content
			.ReadFromJsonAsync<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>(JsonOptions);
		options.ShouldNotBeNull();
		options.ShouldContain(option =>
			option.StandardId == hierarchy.StandardId &&
			option.BoardName == EducationBoardType.CBSE.ToString());

		var draftResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/{academicYearId}/draft",
			content: null);
		draftResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await draftResponse.Content.ReadAsStringAsync());
		var draft = await draftResponse.Content
			.ReadFromJsonAsync<AdmissionCrmAdmissionYearSetupDto>(JsonOptions);
		draft.ShouldNotBeNull();
		draft.Status.ShouldBe(AdmissionCrmAdmissionYearSetupStatus.Draft);

		var today = DateTime.UtcNow.Date;
		var updateResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/drafts/{draft.Id}",
			new UpdateAdmissionCrmAdmissionYearSetupDto
			{
				ConcurrencyStamp = draft.ConcurrencyStamp,
				InquiryOpenDate = today.AddDays(-1),
				InquiryCloseDate = today.AddDays(30),
				RegistrationCloseDate = today.AddDays(60),
				ActivationTargetDate = today.AddDays(90),
				EntranceExamRequired = true,
				FinanceVerificationRequired = false,
				PlacementTiming = AdmissionCrmPlacementTiming.StandardBeforeActivation,
				TargetStandardIds = [hierarchy.StandardId],
				RouteTypes = [AdmissionCrmRouteType.Fresh]
			},
			JsonOptions);
		updateResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await updateResponse.Content.ReadAsStringAsync());
		var publishInput = new PublishAdmissionCrmAdmissionYearSetupDto
		{
			ConcurrencyStamp = (await updateResponse.Content
				.ReadFromJsonAsync<AdmissionCrmAdmissionYearSetupDto>(JsonOptions))!
				.ConcurrencyStamp
		};

		var missingSourceResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/{draft.Id}/publish",
			JsonContent.Create(publishInput, options: JsonOptions));
		missingSourceResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingSourceResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ActiveInquirySourceRequired");

		var sourceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/catalog/inquiry-sources",
			new AdmissionCrmInquirySourceUpsertDto
			{
				Code = "QUICK",
				DisplayName = "Quick inquiry",
				AllowedChannels = [AdmissionInquiryChannel.QuickCallEntry]
			},
			JsonOptions);
		sourceResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await sourceResponse.Content.ReadAsStringAsync());
		var inquirySource = await sourceResponse.Content
			.ReadFromJsonAsync<AdmissionCrmInquirySourceDto>(JsonOptions);
		inquirySource.ShouldNotBeNull();

		var missingTypeResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/{draft.Id}/publish",
			JsonContent.Create(publishInput, options: JsonOptions));
		missingTypeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingTypeResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ActiveInquiryTypeRequired");

		var typeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/catalog/inquiry-types",
			new AdmissionCrmInquiryTypeUpsertDto
			{
				Code = "STANDARD",
				DisplayName = "Standard inquiry"
			},
			JsonOptions);
		typeResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await typeResponse.Content.ReadAsStringAsync());
		var inquiryType = await typeResponse.Content
			.ReadFromJsonAsync<AdmissionCrmInquiryTypeDto>(JsonOptions);
		inquiryType.ShouldNotBeNull();

		var publishResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/setup/admission-years/{draft.Id}/publish",
			JsonContent.Create(publishInput, options: JsonOptions));
		publishResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await publishResponse.Content.ReadAsStringAsync());
		var published = await publishResponse.Content
			.ReadFromJsonAsync<AdmissionCrmAdmissionYearSetupDto>(JsonOptions);
		published.ShouldNotBeNull();
		published.Status.ShouldBe(AdmissionCrmAdmissionYearSetupStatus.Published);
		published.Version.ShouldBe(1);

		var contactSuffix = Random.Shared.Next(100000, 999999);
		var createLeadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				InquirySourceId = inquirySource.Id,
				InquiryTypeId = inquiryType.Id,
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Admission",
				LastName = "Year",
				ParentGuardianName = "Setup Parent",
				PrimaryContactNo = $"98{contactSuffix:000000}01",
				Email = $"admission.year.{contactSuffix}@example.com",
				AdmissionIntent = "Admission Year projection test",
				IntendedRouteType = AdmissionCrmRouteType.Fresh,
				TargetStandardId = hierarchy.StandardId,
				AcademicBoardId = hierarchy.BoardId,
				AcademicMediumId = hierarchy.MediumId,
				AcademicMediumStageId = hierarchy.MediumStageId
			},
			JsonOptions);
		createLeadResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await createLeadResponse.Content.ReadAsStringAsync());
		var lead = await createLeadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();
		lead.AdmissionYearSetupId.ShouldBe(published.Id);
		lead.AdmissionYearSetupVersion.ShouldBe(1);
		lead.IntendedRouteType.ShouldBe(AdmissionCrmRouteType.Fresh);
		lead.IntendedRouteDefinitionId.ShouldBeNull();
		lead.IntendedRouteDefinitionVersion.ShouldBeNull();
		lead.IntendedRouteNameSnapshot.ShouldBeNull();

		var progressResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/workspace/applicants/{lead.Id}/progress");
		progressResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await progressResponse.Content.ReadAsStringAsync());
		var progress = await progressResponse.Content
			.ReadFromJsonAsync<AdmissionCrmApplicantProgressDto>(JsonOptions);
		progress.ShouldNotBeNull();
		progress.AdmissionYearSetupVersion.ShouldBe(1);
		progress.Steps.Count.ShouldBe(6);
		progress.Steps.Single(step => step.Step == AdmissionCrmApplicantStepKey.Exam)
			.Status.ShouldBe(AdmissionCrmApplicantStepStatus.NotStarted);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Authoritative_References_And_Impact_Fingerprint()
	{
		var (sourceYearId, _) = await CreateAcademicYearPairAsync();
		await SeedConfigurationAsync(sourceYearId);

		var referencesResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{sourceYearId}/configuration-safety/references?kind=Round&code=REGULAR");
		referencesResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await referencesResponse.Content.ReadAsStringAsync());
		var references = await referencesResponse.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationReferenceResultDto>(JsonOptions);
		references.ShouldNotBeNull();
		references.Code.ShouldBe("REGULAR");
		references.Families.Single(x => x.Family == "Playbook rounds").ReferenceCount.ShouldBeGreaterThan(0);
		references.Families.Single(x => x.Family == "Seat pools").ReferenceCount.ShouldBeGreaterThan(0);

		var impactResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{sourceYearId}/configuration-safety/impact-preview",
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				Kind = AdmissionCrmConfigurationReferenceKind.Round,
				Code = "REGULAR",
				ProposedCycleConfiguration = new UpdateAdmissionCrmCycleConfigurationDto
				{
					CycleName = "Source admissions",
					EntranceExamRequired = false
				}
			},
			JsonOptions);
		impactResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await impactResponse.Content.ReadAsStringAsync());
		var impact = await impactResponse.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationImpactPreviewDto>(JsonOptions);
		impact.ShouldNotBeNull();
		impact.ChangedFields.ShouldContain("EntranceExamRequired");
		impact.ConfigurationReferenceCount.ShouldBeGreaterThan(0);
		impact.Fingerprint.Length.ShouldBe(64);
		impact.Warnings.ShouldContain(x => x.Contains("registration gates", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preview_Confirm_And_Idempotently_Clone_As_Drafts()
	{
		var (sourceYearId, targetYearId) = await CreateAcademicYearPairAsync();
		var seeded = await SeedConfigurationAsync(sourceYearId);
		var preview = await PreviewCloneAsync(sourceYearId, targetYearId);
		preview.CanConfirm.ShouldBeTrue();
		preview.Families.Single(x => x.Family == "Admission playbooks").CloneableCount.ShouldBe(1);
		preview.Families.Single(x => x.Family == "Route definitions").CloneableCount.ShouldBe(1);
		preview.Families.Single(x => x.Family == "Global policy packs").CloneableCount.ShouldBe(0);
		preview.Families.Single(x => x.Family == "Identifier number series").CloneableCount.ShouldBe(1);

		var confirmInput = new ConfirmAdmissionCrmConfigurationCloneDto
		{
			SourceAcademicYearId = sourceYearId,
			TargetAcademicYearId = targetYearId,
			PreviewFingerprint = preview.Fingerprint
		};
		var confirmResponse = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
			confirmInput,
			JsonOptions);
		confirmResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await confirmResponse.Content.ReadAsStringAsync());
		var firstResult = await confirmResponse.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationCloneResultDto>(JsonOptions);
		firstResult.ShouldNotBeNull();
		firstResult.AlreadyApplied.ShouldBeFalse();

		await AssertClonedDraftsAsync(targetYearId, seeded);

		var idempotentResponse = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
			confirmInput,
			JsonOptions);
		idempotentResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await idempotentResponse.Content.ReadAsStringAsync());
		var idempotentResult = await idempotentResponse.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationCloneResultDto>(JsonOptions);
		idempotentResult.ShouldNotBeNull();
		idempotentResult.AlreadyApplied.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Stale_Fingerprint_And_Nonempty_Target_Configuration()
	{
		var (sourceYearId, targetYearId) = await CreateAcademicYearPairAsync();
		await SeedConfigurationAsync(sourceYearId);
		var preview = await PreviewCloneAsync(sourceYearId, targetYearId);

		await InsertTargetInquirySourceAsync(targetYearId);
		var staleResponse = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
			new ConfirmAdmissionCrmConfigurationCloneDto
			{
				SourceAcademicYearId = sourceYearId,
				TargetAcademicYearId = targetYearId,
				PreviewFingerprint = preview.Fingerprint
			},
			JsonOptions);
		staleResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await staleResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ConfigurationCloneFingerprintMismatch");

		var nonemptyPreview = await PreviewCloneAsync(sourceYearId, targetYearId);
		nonemptyPreview.CanConfirm.ShouldBeFalse();
		nonemptyPreview.Conflicts.ShouldContain(x =>
			x.Contains("configuration record", StringComparison.OrdinalIgnoreCase));
		var nonemptyResponse = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
			new ConfirmAdmissionCrmConfigurationCloneDto
			{
				SourceAcademicYearId = sourceYearId,
				TargetAcademicYearId = targetYearId,
				PreviewFingerprint = nonemptyPreview.Fingerprint
			},
			JsonOptions);
		nonemptyResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await nonemptyResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ConfigurationCloneTargetNotEmpty");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Target_With_Applicant_Activity()
	{
		var (sourceYearId, targetYearId) = await CreateAcademicYearPairAsync();
		await SeedConfigurationAsync(sourceYearId);
		await InsertTargetLeadAsync(targetYearId);
		var preview = await PreviewCloneAsync(sourceYearId, targetYearId);
		preview.CanConfirm.ShouldBeFalse();
		preview.TargetApplicantActivityCount.ShouldBe(1);
		preview.Conflicts.ShouldContain(x =>
			x.Contains("applicant record", StringComparison.OrdinalIgnoreCase));

		var response = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
			new ConfirmAdmissionCrmConfigurationCloneDto
			{
				SourceAcademicYearId = sourceYearId,
				TargetAcademicYearId = targetYearId,
				PreviewFingerprint = preview.Fingerprint
			},
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ConfigurationCloneTargetNotEmpty");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Not_Read_Academic_Years_From_Another_Tenant()
	{
		var currentTenantYear = (await CreateAcademicYearPairAsync()).SourceYearId;
		await SeedConfigurationAsync(currentTenantYear);
		var otherTenantId = Guid.NewGuid();
		var otherTenantYear = await CreateAcademicYearAsync(otherTenantId);

		var response = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/preview",
			new PreviewAdmissionCrmConfigurationCloneDto
			{
				SourceAcademicYearId = currentTenantYear,
				TargetAcademicYearId = otherTenantYear
			},
			JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse();
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldNotContain(otherTenantId.ToString("D"));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Stale_Cycle_And_Stage_Impact_Fingerprints()
	{
		var academicYearId = (await CreateAcademicYearPairAsync()).SourceYearId;
		var cycleResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle");
		cycleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cycleResponse.Content.ReadAsStringAsync());
		var cycle = await cycleResponse.Content
			.ReadFromJsonAsync<AdmissionCrmCycleConfigurationDto>(JsonOptions);
		cycle.ShouldNotBeNull();

		var firstCycleUpdate = BuildCycleUpdate(cycle, "First reviewed change.");
		var firstCyclePreview = await PreviewImpactAsync(
			academicYearId,
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				ProposedCycleConfiguration = firstCycleUpdate
			});
		var secondCycleUpdate = BuildCycleUpdate(cycle, "Second reviewed change.");
		var secondCyclePreview = await PreviewImpactAsync(
			academicYearId,
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				ProposedCycleConfiguration = secondCycleUpdate
			});
		secondCycleUpdate.ImpactPreviewFingerprint = secondCyclePreview.Fingerprint;
		var acceptedCycleResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle",
			secondCycleUpdate,
			JsonOptions);
		acceptedCycleResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await acceptedCycleResponse.Content.ReadAsStringAsync());

		firstCycleUpdate.ImpactPreviewFingerprint = firstCyclePreview.Fingerprint;
		var staleCycleResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle",
			firstCycleUpdate,
			JsonOptions);
		staleCycleResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await staleCycleResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ConfigurationImpactFingerprintMismatch");

		var stagesResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration/pipeline-stages");
		stagesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stagesResponse.Content.ReadAsStringAsync());
		var stages = await stagesResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmPipelineStageDto>>(JsonOptions);
		stages.ShouldNotBeNull();
		var stage = stages.Items.First();
		var firstStageUpdate = BuildStageUpdate(stage, "First reviewed stage");
		var firstStagePreview = await PreviewImpactAsync(
			academicYearId,
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				PipelineStageId = stage.Id,
				ProposedPipelineStage = firstStageUpdate
			});
		var secondStageUpdate = BuildStageUpdate(stage, "Second reviewed stage");
		var secondStagePreview = await PreviewImpactAsync(
			academicYearId,
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				PipelineStageId = stage.Id,
				ProposedPipelineStage = secondStageUpdate
			});
		secondStageUpdate.ImpactPreviewFingerprint = secondStagePreview.Fingerprint;
		var acceptedStageResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/pipeline-stages/{stage.Id}",
			secondStageUpdate,
			JsonOptions);
		acceptedStageResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await acceptedStageResponse.Content.ReadAsStringAsync());

		firstStageUpdate.ImpactPreviewFingerprint = firstStagePreview.Fingerprint;
		var staleStageResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/pipeline-stages/{stage.Id}",
			firstStageUpdate,
			JsonOptions);
		staleStageResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await staleStageResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ConfigurationImpactFingerprintMismatch");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Block_Unmapped_Hierarchy_And_Remap_Embedded_And_Direct_Ids()
	{
		var (sourceYearId, targetYearId) = await CreateAcademicYearPairAsync();
		var sourceHierarchy = await CreateAcademicHierarchyAsync(sourceYearId);
		await SeedConfigurationAsync(sourceYearId, sourceHierarchy.StandardId);
		await InsertHierarchyBoundNumberSeriesAsync(sourceYearId, sourceHierarchy);

		var blockedPreview = await PreviewCloneAsync(sourceYearId, targetYearId);
		blockedPreview.CanConfirm.ShouldBeFalse();
		blockedPreview.Conflicts.ShouldContain(x =>
			x.Contains(sourceHierarchy.StandardId.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
			x.Contains("target academic year", StringComparison.OrdinalIgnoreCase));

		var targetHierarchy = await CreateAcademicHierarchyAsync(targetYearId);
		var readyPreview = await PreviewCloneAsync(sourceYearId, targetYearId);
		readyPreview.CanConfirm.ShouldBeTrue();
		readyPreview.Warnings.ShouldContain(x =>
			x.Contains("hierarchy identifier", StringComparison.OrdinalIgnoreCase));

		var confirmResponse = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
			new ConfirmAdmissionCrmConfigurationCloneDto
			{
				SourceAcademicYearId = sourceYearId,
				TargetAcademicYearId = targetYearId,
				PreviewFingerprint = readyPreview.Fingerprint
			},
			JsonOptions);
		confirmResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await confirmResponse.Content.ReadAsStringAsync());
		await AssertHierarchyReferencesRemappedAsync(
			targetYearId,
			sourceHierarchy,
			targetHierarchy);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Serialize_Concurrent_Clone_Confirmations()
	{
		var (sourceYearId, targetYearId) = await CreateAcademicYearPairAsync();
		await SeedConfigurationAsync(sourceYearId);
		var preview = await PreviewCloneAsync(sourceYearId, targetYearId);
		var input = new ConfirmAdmissionCrmConfigurationCloneDto
		{
			SourceAcademicYearId = sourceYearId,
			TargetAcademicYearId = targetYearId,
			PreviewFingerprint = preview.Fingerprint
		};
		using var secondClient = Factory.CreateClient();
		var requests = new[]
		{
			Client.PostAsJsonAsync(
				"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
				input,
				JsonOptions),
			secondClient.PostAsJsonAsync(
				"/api/student-enrollment/admissions-crm/configuration-safety/clone/confirm",
				input,
				JsonOptions)
		};

		var responses = await Task.WhenAll(requests);
		responses.ShouldAllBe(x => x.StatusCode == HttpStatusCode.OK);
		var results = await Task.WhenAll(responses.Select(async response =>
			await response.Content.ReadFromJsonAsync<AdmissionCrmConfigurationCloneResultDto>(JsonOptions)
			?? throw new InvalidOperationException("Concurrent clone confirmation returned no body.")));
		results.Count(x => x.AlreadyApplied).ShouldBe(1);
		results.Count(x => !x.AlreadyApplied).ShouldBe(1);
		await AssertTargetHasSingleCycleAsync(targetYearId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Year_Wide_Configuration_Mutations_For_Standard_Only_User()
	{
		var client = await CreateStandardScopedClientAsync();
		var stagesResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/pipeline-stages");
		stagesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stagesResponse.Content.ReadAsStringAsync());
		var stages = await stagesResponse.Content
			.ReadFromJsonAsync<PagedResultDto<AdmissionCrmPipelineStageDto>>(JsonOptions);
		stages.ShouldNotBeNull();
		var stage = stages.Items.First();

		var schoolDefaultResponse = await client.PutAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration/cycle-default",
			new UpdateAdmissionCrmCycleConfigurationDto
			{
				CycleName = "Standard-scoped school default mutation"
			},
			JsonOptions);
		schoolDefaultResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await schoolDefaultResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:TenantWideScopeRequired");

		var cycleResponse = await client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/cycle",
			new UpdateAdmissionCrmCycleConfigurationDto
			{
				CycleName = "Standard-scoped mutation",
				ImpactPreviewFingerprint = new string('A', 64)
			},
			JsonOptions);
		cycleResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await cycleResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");

		var stageResponse = await client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/configuration/pipeline-stages/{stage.Id}",
			BuildStageUpdate(stage, "Standard-scoped stage"),
			JsonOptions);
		stageResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await stageResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");

		var sourceResponse = await client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/catalog/inquiry-sources",
			new AdmissionCrmInquirySourceUpsertDto
			{
				Code = "STANDARD_ONLY",
				DisplayName = "Standard-only source",
				AllowedChannels = [AdmissionInquiryChannel.WalkInReception]
			},
			JsonOptions);
		sourceResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await sourceResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");

		var registrationSeriesResponse = await client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/configuration/form-number-series",
			new AdmissionCrmFormNumberSeriesUpsertDto
			{
				Name = "Standard-only registration series",
				Prefix = "REG-"
			},
			JsonOptions);
		registrationSeriesResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await registrationSeriesResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Filter_Operational_Standard_Options_To_UserScope()
	{
		var outOfScopeStandardId = await CreateAdditionalStandardAsync();
		using var client = await CreateStandardScopedClientAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add);

		var response = await client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/operational-standard-options");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var options = await response.Content
			.ReadFromJsonAsync<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>(JsonOptions);
		options.ShouldNotBeNull();
		options.ShouldContain(option => option.StandardId == Factory.SeededStandardId);
		options.ShouldNotContain(option => option.StandardId == outOfScopeStandardId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Permission_And_Scope_Safe_Focused_Admission_Report()
	{
		var otherStandardId = await CreateAdditionalStandardAsync();
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var scopedSource = $"Scoped report {suffix}";
		var otherSource = $"Other report {suffix}";
		await InsertReportLeadAsync(Factory.SeededStandardId, scopedSource);
		await InsertReportLeadAsync(otherStandardId, otherSource);

		using var forbiddenClient = Factory.CreateClient();
		forbiddenClient.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"__none__");
		var forbiddenResponse = await forbiddenClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/reports/admissions");
		forbiddenResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		using var scopedClient = await CreateStandardScopedClientAsync();
		var scopedResponse = await scopedClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/reports/admissions");
		scopedResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await scopedResponse.Content.ReadAsStringAsync());
		var scopedReport = await scopedResponse.Content
			.ReadFromJsonAsync<AdmissionCrmAdmissionReportDto>(JsonOptions);
		scopedReport.ShouldNotBeNull();
		scopedReport.IsScopeRestricted.ShouldBeTrue();
		scopedReport.Sources.ShouldContain(source => source.SourceName == scopedSource);
		scopedReport.Sources.ShouldNotContain(source => source.SourceName == otherSource);
		scopedReport.Standards.ShouldAllBe(row =>
			row.TargetStandardId == Factory.SeededStandardId);

		var filteredResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/reports/admissions?targetStandardId={otherStandardId:D}");
		filteredResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await filteredResponse.Content.ReadAsStringAsync());
		var filteredReport = await filteredResponse.Content
			.ReadFromJsonAsync<AdmissionCrmAdmissionReportDto>(JsonOptions);
		filteredReport.ShouldNotBeNull();
		filteredReport.Sources.ShouldContain(source => source.SourceName == otherSource);

		var oversizedRangeResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/reports/admissions?fromDate=2025-01-01&toDate=2026-01-03");
		oversizedRangeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await oversizedRangeResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ReportDateRangeTooLarge");
	}

	private async Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewImpactAsync(
		Guid academicYearId,
		PreviewAdmissionCrmConfigurationImpactDto input)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{academicYearId}/configuration-safety/impact-preview",
			input,
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content
			.ReadFromJsonAsync<AdmissionCrmConfigurationImpactPreviewDto>(JsonOptions)
			?? throw new InvalidOperationException("Impact preview response was empty.");
	}

	private static UpdateAdmissionCrmCycleConfigurationDto BuildCycleUpdate(
		AdmissionCrmCycleConfigurationDto cycle,
		string notes) =>
		new()
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
			JourneyRules = cycle.JourneyRules.ToList(),
			ApplicantChecklist = cycle.ApplicantChecklist.ToList(),
			ChannelSettings = cycle.ChannelSettings.ToList(),
			Notes = notes
		};

	private static UpdateAdmissionCrmPipelineStageDto BuildStageUpdate(
		AdmissionCrmPipelineStageDto stage,
		string stageName) =>
		new()
		{
			StageName = stageName,
			DisplayOrder = stage.DisplayOrder,
			IsEnabled = stage.IsEnabled,
			ColorHex = stage.ColorHex,
			SlaTargetHours = stage.SlaTargetHours,
			OwnerResponsibility = stage.OwnerResponsibility,
			AllowedNextStages = stage.AllowedNextStages.ToList()
		};

	private async Task<AcademicHierarchy> CreateAcademicHierarchyAsync(Guid academicYearId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var boardRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationBoard, Guid>>();
		var mediumRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();
		var stageRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		var standardRepository = scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>();
		var divisionRepository = scope.ServiceProvider.GetRequiredService<IRepository<Division, Guid>>();
		var boardManager = scope.ServiceProvider.GetRequiredService<EducationBoardManager>();
		var mediumManager = scope.ServiceProvider.GetRequiredService<EducationMediumManager>();
		var stageManager = scope.ServiceProvider.GetRequiredService<EducationMediumStageManager>();
		var standardManager = scope.ServiceProvider.GetRequiredService<StandardManager>();
		var divisionManager = scope.ServiceProvider.GetRequiredService<DivisionManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var board = await boardManager.CreateAsync(
				academicYearId,
				EducationBoardType.CBSE,
				new DateTime(2200, 4, 1),
				new DateTime(2201, 3, 31));
			await boardRepository.InsertAsync(board, autoSave: false);
			var medium = await mediumManager.CreateAsync(board.Id, "English");
			await mediumRepository.InsertAsync(medium, autoSave: false);
			var stage = await stageManager.CreateAsync(medium.Id, "Primary");
			await stageRepository.InsertAsync(stage, autoSave: false);
			var standard = await standardManager.CreateAsync(stage.Id, "Grade 1", 6, shiftId: null);
			await standardRepository.InsertAsync(standard, autoSave: false);
			var division = await divisionManager.CreateAsync(standard.Id, "A", 30, "A-101");
			await divisionRepository.InsertAsync(division, autoSave: false);
			await uow.CompleteAsync();
			return new AcademicHierarchy(
				board.Id,
				medium.Id,
				stage.Id,
				standard.Id,
				division.Id);
		}
	}

	private async Task InsertHierarchyBoundNumberSeriesAsync(
		Guid academicYearId,
		AcademicHierarchy hierarchy)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmNumberSeries, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await repository.InsertAsync(
				new AdmissionCrmNumberSeries(
					Guid.NewGuid(),
					tenantId,
					academicYearId,
					"ADM-HIERARCHY",
					"Hierarchy admission IDs",
					AdmissionCrmIdentifierType.AdmissionNumber,
					hierarchy.BoardId,
					hierarchy.MediumId,
					hierarchy.StandardId,
					"H-",
					string.Empty,
					6,
					1,
					null,
					new DateTime(2200, 4, 1),
					new DateTime(2201, 3, 31)),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task AssertHierarchyReferencesRemappedAsync(
		Guid targetAcademicYearId,
		AcademicHierarchy source,
		AcademicHierarchy target)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var playbookRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmPlaybook, Guid>>();
		var numberSeriesRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmNumberSeries, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var playbooks = await playbookRepository.GetQueryableAsync();
			var clonedPlaybook = await playbooks.SingleAsync(x =>
				x.AcademicYearId == targetAcademicYearId &&
				x.Name == "Primary playbook");
			clonedPlaybook.ApplicabilityJson.ShouldContain(target.StandardId.ToString("D"));
			clonedPlaybook.ApplicabilityJson.ShouldNotContain(source.StandardId.ToString("D"));

			var numberSeries = await numberSeriesRepository.GetQueryableAsync();
			var clonedSeries = await numberSeries.SingleAsync(x =>
				x.AcademicYearId == targetAcademicYearId &&
				x.Code == "ADM-HIERARCHY");
			clonedSeries.EducationBoardId.ShouldBe(target.BoardId);
			clonedSeries.AcademicMediumId.ShouldBe(target.MediumId);
			clonedSeries.StandardId.ShouldBe(target.StandardId);
			await uow.CompleteAsync();
		}
	}

	private async Task AssertTargetHasSingleCycleAsync(Guid targetAcademicYearId)
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
			(await queryable.CountAsync(x => x.AcademicYearId == targetAcademicYearId)).ShouldBe(1);
			await uow.CompleteAsync();
		}
	}

	private async Task<HttpClient> CreateStandardScopedClientAsync(params string[] permissions)
	{
		var userId = Guid.NewGuid();
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					academicYearId: Factory.SeededAcademicYearId,
					standardId: Factory.SeededStandardId,
					description: "Admissions configuration standard-only scope regression."),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString("D"));
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "teacher");
		if (permissions.Length > 0)
		{
			client.DefaultRequestHeaders.Add(
				AlwaysGrantPermissionChecker.TestPermissionsHeader,
				string.Join(",", permissions));
		}
		return client;
	}

	private async Task<Guid> CreateAdditionalStandardAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>();
		var manager = scope.ServiceProvider.GetRequiredService<StandardManager>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var seededStandard = await repository.GetAsync(Factory.SeededStandardId);
			var standard = await manager.CreateAsync(
				seededStandard.EducationMediumStageId,
				$"Report {Guid.NewGuid():N}",
				7,
				shiftId: null);
			await repository.InsertAsync(standard, autoSave: false);
			await uow.CompleteAsync();
			return standard.Id;
		}
	}

	private async Task InsertReportLeadAsync(Guid targetStandardId, string source)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await repository.InsertAsync(
				new AdmissionInquiryLead(
					Guid.NewGuid(),
					tenantId,
					Factory.SeededAcademicYearId,
					null,
					source,
					AdmissionInquiryChannel.WalkInReception,
					null,
					null,
					"en",
					"Report",
					null,
					"Applicant",
					null,
					"Report Parent",
					$"9{Random.Shared.NextInt64(100000000, 999999999)}",
					null,
					"Focused admission report scope test",
					targetStandardId,
					null,
					null,
					DateTimeOffset.UtcNow),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<AdmissionCrmConfigurationClonePreviewDto> PreviewCloneAsync(
		Guid sourceAcademicYearId,
		Guid targetAcademicYearId)
	{
		var response = await Client.PostAsJsonAsync(
			"/api/student-enrollment/admissions-crm/configuration-safety/clone/preview",
			new PreviewAdmissionCrmConfigurationCloneDto
			{
				SourceAcademicYearId = sourceAcademicYearId,
				TargetAcademicYearId = targetAcademicYearId
			},
			JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<AdmissionCrmConfigurationClonePreviewDto>(JsonOptions)
			?? throw new InvalidOperationException("Clone preview response was empty.");
	}

	private async Task<(Guid SourceYearId, Guid TargetYearId)> CreateAcademicYearPairAsync()
	{
		var sequence = Interlocked.Increment(ref _academicYearSequence);
		var sourceYear = 2200 + sequence * 3;
		var source = await CreateAcademicYearAsync(
			Guid.Parse(TestAuthHandler.TestTenantId),
			sourceYear);
		var target = await CreateAcademicYearAsync(
			Guid.Parse(TestAuthHandler.TestTenantId),
			sourceYear + 1);
		return (source, target);
	}

	private async Task<Guid> CreateAcademicYearAsync(Guid tenantId, int? startYear = null)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
		var year = startYear ?? 2500 + Interlocked.Increment(ref _academicYearSequence) * 2;

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYear = await academicYearManager.CreateAsync(
				$"CRM Safety {tenantId:N} {year}",
				new DateTime(year, 4, 1),
				new DateTime(year + 1, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);
			await uow.CompleteAsync();
			return academicYear.Id;
		}
	}

	private async Task<SeededConfiguration> SeedConfigurationAsync(
		Guid academicYearId,
		Guid? embeddedStandardId = null)
	{
		using var scope = Factory.Services.CreateScope();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var cycleRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmCycleConfiguration, Guid>>();
			var stageRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmPipelineStage, Guid>>();
			var sourceRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmInquirySource, Guid>>();
			var formRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionInquiryFormTemplate, Guid>>();
			var messageRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmMessageTemplate, Guid>>();
			var playbookRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmPlaybook, Guid>>();
			var registrationRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmRegistrationTemplate, Guid>>();
			var formSeriesRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmFormNumberSeries, Guid>>();
			var checklistRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmDocumentChecklistTemplate, Guid>>();
			var seatPoolRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmSeatPool, Guid>>();
			var feePolicyRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmFeePolicy, Guid>>();
			var routeRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
			var policyPackRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmPolicyPack, Guid>>();
			var policyAdoptionRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmPolicyAdoption, Guid>>();
			var numberSeriesRepository = scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmNumberSeries, Guid>>();

			var inquirySourceId = Guid.NewGuid();
			var formId = Guid.NewGuid();
			var messageId = Guid.NewGuid();
			var playbookId = Guid.NewGuid();
			var routeId = Guid.NewGuid();
			var now = DateTime.UtcNow;
			var source = new AdmissionCrmInquirySource(
				inquirySourceId,
				tenantId,
				academicYearId,
				"WEB",
				"Website",
				"Public website inquiries.",
				[AdmissionInquiryChannel.SchoolWebsiteForm],
				10,
				isRoutingEligible: true);
			await sourceRepository.InsertAsync(source, autoSave: false);

			var form = new AdmissionInquiryFormTemplate(
				formId,
				tenantId,
				academicYearId,
				"Parent inquiry",
				"Parent-facing inquiry form.",
				AdmissionInquiryChannel.SchoolWebsiteForm,
				isPublic: false,
				consentText: null,
				privacyNoticeUrl: null,
				"[{\"key\":\"roundCode\",\"label\":\"Round\",\"fieldType\":0,\"required\":true,\"defaultValue\":\"REGULAR\"}]",
				"{}");
			await formRepository.InsertAsync(form, autoSave: false);

			var message = new AdmissionCrmMessageTemplate(
				messageId,
				tenantId,
				academicYearId,
				"Round confirmation",
				AdmissionCrmMessageChannel.Email,
				"Round confirmation",
				"Admission round",
				$"REGULAR route {routeId:D}",
				"[\"studentName\"]");
			message.UpdateStatus(AdmissionCrmMessageTemplateStatus.Approved, isActive: true);
			await messageRepository.InsertAsync(message, autoSave: false);

			var cycle = new AdmissionCrmCycleConfiguration(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"Source admissions");
			cycle.Update(
				"Source admissions",
				isActive: true,
				registrationFormRequiredBeforeExam: false,
				entranceExamRequired: true,
				staleAfterDays: 30,
				archiveAfterDays: 60,
				defaultLeadSlaHours: 24,
				activationCancellationWindowDays: 7,
				defaultExamPassingPercentage: 40,
				AdmissionCrmMeritRankingMode.MarksThenInquiryDate,
				AdmissionCrmExamReadinessField.Contact,
				"{}",
				$"{{\"rules\":[{{\"sourceId\":\"{inquirySourceId:D}\"}}]}}",
				$"[{{\"trigger\":\"NewInquiry\",\"messageTemplateId\":\"{messageId:D}\"}}]",
				"[]",
				$"[{{\"channel\":\"SchoolWebsiteForm\",\"formTemplateId\":\"{formId:D}\"}}]",
				"{\"timeZoneId\":\"Asia/Kolkata\",\"workingDays\":[1,2,3,4,5,6],\"workingDayStartHour\":8,\"workingDayEndHour\":17,\"holidays\":[],\"escalationAfterHours\":24}",
				"Source-year settings.");
			cycle.LockJourney(Guid.NewGuid(), now, "Published source journey.");
			await cycleRepository.InsertAsync(cycle, autoSave: false);

			var stage = AdmissionCrmPipelineStage.CreateCustom(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"REVIEW",
				AdmissionInquiryStage.FollowUp,
				"Review",
				25,
				"#336699",
				12,
				"Admissions lead",
				"[2]");
			await stageRepository.InsertAsync(stage, autoSave: false);

			var playbook = new AdmissionCrmPlaybook(
				playbookId,
				tenantId,
				academicYearId,
				"Primary playbook",
				1,
				isFallback: true,
				AdmissionCrmRegistrationPath.RegistrationAfterExam,
				embeddedStandardId.HasValue
					? $"{{\"targetStandardId\":\"{embeddedStandardId.Value:D}\"}}"
					: "{}",
				"[{\"code\":\"REVIEW\"}]",
				"[{\"code\":\"REGULAR\"}]",
				"[{\"code\":\"GRADE1-REGULAR\",\"roundCode\":\"REGULAR\"}]",
				"[{\"code\":\"GRADE1-ASSESSMENT\",\"roundCode\":\"REGULAR\"}]",
				"[]",
				"[{\"code\":\"AUTO-REVIEW\",\"stageCode\":\"REVIEW\"}]",
				"Published source playbook.");
			playbook.Publish(now);
			await playbookRepository.InsertAsync(playbook, autoSave: false);

			var registrationTemplate = new AdmissionCrmRegistrationTemplate(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"Registration form",
				1,
				isFallback: true,
				"{\"roundCode\":\"REGULAR\"}",
				"[{\"key\":\"student\",\"fields\":[{\"key\":\"round\",\"defaultValue\":\"REGULAR\"}]}]",
				14,
				allowPartialSave: true,
				feeRequired: false,
				feeAmount: 0,
				AdmissionInquiryStage.Eligible,
				"Published source registration template.");
			registrationTemplate.Publish(now);
			await registrationRepository.InsertAsync(registrationTemplate, autoSave: false);

			await formSeriesRepository.InsertAsync(
				new AdmissionCrmFormNumberSeries(
					Guid.NewGuid(),
					tenantId,
					academicYearId,
					"Registration numbers",
					"REG-",
					string.Empty,
					5,
					42,
					"{\"roundCode\":\"REGULAR\"}",
					isActive: true),
				autoSave: false);

			var checklist = new AdmissionCrmDocumentChecklistTemplate(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"Admission documents",
				1,
				isFallback: true,
				"{\"roundCode\":\"REGULAR\"}",
				"[{\"code\":\"TC\",\"required\":true}]",
				1,
				"Published source checklist.");
			checklist.Publish(now);
			await checklistRepository.InsertAsync(checklist, autoSave: false);

			var seatPool = new AdmissionCrmSeatPool(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				playbookId,
				"GRADE1-REGULAR",
				"Grade 1 regular",
				"REGULAR",
				"Main",
				"Grade 1",
				"CBSE",
				"English",
				"General",
				25,
				30);
			seatPool.ReserveSeat(capacityOverride: false, overrideReason: null);
			await seatPoolRepository.InsertAsync(seatPool, autoSave: false);

			var feePolicy = new AdmissionCrmFeePolicy(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"REG-FEE",
				"Registration fee",
				1,
				"{\"roundCode\":\"REGULAR\"}",
				"[{\"code\":\"APPLICATION\",\"amount\":500}]",
				"INR",
				AdmissionCrmPaymentGateType.RequiredAmountConfirmed,
				AdmissionCrmPaymentGateMilestone.OfferAcceptance,
				500,
				0,
				24,
				now,
				now.AddMonths(10));
			feePolicy.Submit(Guid.NewGuid(), now);
			feePolicy.Approve(Guid.NewGuid(), now);
			feePolicy.Publish(now);
			await feePolicyRepository.InsertAsync(feePolicy, autoSave: false);

			var route = new AdmissionCrmRouteDefinition(
				routeId,
				tenantId,
				academicYearId,
				"FRESH",
				"Fresh admission",
				AdmissionCrmRouteType.Fresh,
				1,
				"{\"roundCode\":\"REGULAR\",\"stageCode\":\"REVIEW\"}",
				now,
				now.AddMonths(10));
			route.Submit(Guid.NewGuid());
			route.Publish(Guid.NewGuid());
			await routeRepository.InsertAsync(route, autoSave: false);

			var policyPack = new AdmissionCrmPolicyPack(
				Guid.NewGuid(),
				tenantId,
				"POL-" + Guid.NewGuid().ToString("N")[..8],
				"Admission baseline",
				"Gujarat",
				"CBSE",
				1,
				"[{\"authority\":\"CBSE\"}]",
				"[{\"code\":\"REGULAR\"}]",
				now,
				now.AddYears(2),
				now.AddMonths(6));
			policyPack.Submit(Guid.NewGuid(), now);
			policyPack.Approve(Guid.NewGuid(), now);
			policyPack.Publish(now);
			await policyPackRepository.InsertAsync(policyPack, autoSave: false);
			await policyAdoptionRepository.InsertAsync(
				new AdmissionCrmPolicyAdoption(
					Guid.NewGuid(),
					tenantId,
					academicYearId,
					policyPack.Id,
					policyPack.Version,
					"{\"roundCode\":\"REGULAR\"}",
					"[]",
					"{\"affectedApplicants\":0}",
					null,
					now,
					now.AddMonths(10),
					Guid.NewGuid(),
					now),
				autoSave: false);

			var independentNumberSeries = new AdmissionCrmNumberSeries(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				"ADM-GLOBAL",
				"Admission IDs",
				AdmissionCrmIdentifierType.AdmissionNumber,
				null,
				null,
				null,
				"ADM-",
				string.Empty,
				6,
				100,
				null,
				now,
				now.AddMonths(10));
			independentNumberSeries.Submit(Guid.NewGuid());
			independentNumberSeries.Publish(Guid.NewGuid());
			await numberSeriesRepository.InsertAsync(independentNumberSeries, autoSave: false);

			await uow.CompleteAsync();
			return new SeededConfiguration(messageId);
		}
	}

	private async Task AssertClonedDraftsAsync(Guid targetAcademicYearId, SeededConfiguration seeded)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var cycles = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmCycleConfiguration, Guid>>()
				.GetQueryableAsync();
			var messages = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmMessageTemplate, Guid>>()
				.GetQueryableAsync();
			var playbooks = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmPlaybook, Guid>>()
				.GetQueryableAsync();
			var registrations = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmRegistrationTemplate, Guid>>()
				.GetQueryableAsync();
			var checklists = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmDocumentChecklistTemplate, Guid>>()
				.GetQueryableAsync();
			var routes = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>()
				.GetQueryableAsync();
			var feePolicies = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmFeePolicy, Guid>>()
				.GetQueryableAsync();
			var formSeries = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmFormNumberSeries, Guid>>()
				.GetQueryableAsync();
			var seatPools = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmSeatPool, Guid>>()
				.GetQueryableAsync();
			var numberSeries = await scope.ServiceProvider
				.GetRequiredService<IRepository<AdmissionCrmNumberSeries, Guid>>()
				.GetQueryableAsync();

			var cycle = await cycles.SingleAsync(x => x.AcademicYearId == targetAcademicYearId);
			var message = await messages.SingleAsync(x => x.AcademicYearId == targetAcademicYearId);
			cycle.JourneyLocked.ShouldBeFalse();
			cycle.JourneyRulesJson.ShouldContain(message.Id.ToString("D"));
			cycle.JourneyRulesJson.ShouldNotContain(seeded.SourceMessageTemplateId.ToString("D"));
			message.Status.ShouldBe(AdmissionCrmMessageTemplateStatus.Draft);
			(await playbooks.SingleAsync(x => x.AcademicYearId == targetAcademicYearId))
				.Status.ShouldBe(AdmissionCrmPlaybookStatus.Draft);
			(await registrations.SingleAsync(x => x.AcademicYearId == targetAcademicYearId))
				.Status.ShouldBe(AdmissionCrmRegistrationTemplateStatus.Draft);
			(await checklists.SingleAsync(x => x.AcademicYearId == targetAcademicYearId))
				.Status.ShouldBe(AdmissionCrmDocumentChecklistTemplateStatus.Draft);
			(await routes.SingleAsync(x => x.AcademicYearId == targetAcademicYearId))
				.Status.ShouldBe(AdmissionCrmRouteDefinitionStatus.Draft);
			(await feePolicies.SingleAsync(x => x.AcademicYearId == targetAcademicYearId))
				.Status.ShouldBe(AdmissionCrmFeePolicyStatus.Draft);
			(await formSeries.SingleAsync(x => x.AcademicYearId == targetAcademicYearId))
				.NextNumber.ShouldBe(1);
			var seatPool = await seatPools.SingleAsync(x => x.AcademicYearId == targetAcademicYearId);
			seatPool.ReservedSeats.ShouldBe(0);
			seatPool.AcceptedSeats.ShouldBe(0);
			seatPool.ActivatedSeats.ShouldBe(0);
			var clonedNumberSeries = await numberSeries
				.Where(x => x.AcademicYearId == targetAcademicYearId)
				.ToListAsync();
			clonedNumberSeries.Count.ShouldBe(1);
			clonedNumberSeries[0].Status.ShouldBe(AdmissionCrmNumberSeriesStatus.Draft);
			clonedNumberSeries[0].NextSequence.ShouldBe(clonedNumberSeries[0].StartingSequence);
			await uow.CompleteAsync();
		}
	}

	private async Task InsertTargetInquirySourceAsync(Guid targetAcademicYearId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmInquirySource, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await repository.InsertAsync(
				new AdmissionCrmInquirySource(
					Guid.NewGuid(),
					tenantId,
					targetAcademicYearId,
					"TARGET",
					"Target source",
					null,
					[AdmissionInquiryChannel.WalkInReception],
					1,
					true),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task InsertTargetLeadAsync(Guid targetAcademicYearId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await repository.InsertAsync(
				new AdmissionInquiryLead(
					Guid.NewGuid(),
					tenantId,
					targetAcademicYearId,
					null,
					null,
					AdmissionInquiryChannel.WalkInReception,
					null,
					null,
					"en",
					"Target",
					null,
					"Applicant",
					null,
					"Parent",
					"9999999999",
					null,
					"Admission",
					null,
					"Grade 1",
					null,
					DateTimeOffset.UtcNow),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private sealed record AcademicHierarchy(
		Guid BoardId,
		Guid MediumId,
		Guid MediumStageId,
		Guid StandardId,
		Guid DivisionId);

	private sealed record SeededConfiguration(Guid SourceMessageTemplateId);
}
