using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmActivationApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmActivationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Evaluated_Route_Assignment_For_Governed_Intake_Pathway()
	{
		var lateralRoute = await CreatePublishedRouteDefinitionAsync(AdmissionCrmRouteType.Lateral);

		var placementResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/placement-options");
		placementResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await placementResponse.Content.ReadAsStringAsync());
		var placement = (await placementResponse.Content
			.ReadFromJsonAsync<AdmissionCrmPlacementOptionDto[]>(JsonOptions))!.First();

		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Lateral admission requiring governed review",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Lateral",
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				DateOfBirth = DateTimeOffset.UtcNow.AddYears(-8),
				TargetStandardId = placement.StandardId,
				DesiredClassName = placement.StandardName,
				IntendedRouteDefinitionId = lateralRoute.RouteDefinitionId
			},
			JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = (await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
		await SeedExistingStudentInDivisionAsync(placement);

		var previewInput = new PreviewAdmissionCrmActivationDto
		{
			EducationBoardId = placement.EducationBoardId,
			AcademicMediumId = placement.AcademicMediumId,
			AcademicMediumStageId = placement.AcademicMediumStageId,
			StandardId = placement.StandardId,
			DivisionId = placement.DivisionId,
			GrNumberSeriesId = Guid.NewGuid(),
			RequireStudentAccount = false,
			RequireGuardianAccount = false
		};
		using var previewTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var blockedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation/preview",
			previewInput,
			JsonOptions,
			previewTimeout.Token);
		blockedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await blockedResponse.Content.ReadAsStringAsync());
		var blocked = (await blockedResponse.Content
			.ReadFromJsonAsync<AdmissionCrmActivationPreviewDto>(JsonOptions))!;
		blocked.DivisionOccupancy.ShouldBe(0);
		var blockedRoute = blocked.Items.Single(item => item.Key == "admission_route");
		blockedRoute.Status.ShouldBe(AdmissionCrmActivationStepStatus.Failed);
		blockedRoute.Detail.ShouldContain("requires an evaluated route assignment");

		var unrelatedRoute = await CreatePublishedRouteDefinitionAsync(AdmissionCrmRouteType.Transfer);
		await RecordUnrelatedEligibleRouteChangeAsync(lead, unrelatedRoute);
		var unrelatedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation/preview",
			previewInput,
			JsonOptions);
		unrelatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await unrelatedResponse.Content.ReadAsStringAsync());
		var unrelated = (await unrelatedResponse.Content
			.ReadFromJsonAsync<AdmissionCrmActivationPreviewDto>(JsonOptions))!;
		var unrelatedRouteItem = unrelated.Items.Single(item => item.Key == "admission_route");
		unrelatedRouteItem.Status.ShouldBe(AdmissionCrmActivationStepStatus.Failed);
		unrelatedRouteItem.Detail.ShouldContain("does not correspond to the selected pathway");

		await RecordEligibleRouteAssignmentAsync(lead, lateralRoute);

		var evaluatedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation/preview",
			previewInput,
			JsonOptions);
		evaluatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await evaluatedResponse.Content.ReadAsStringAsync());
		var evaluated = (await evaluatedResponse.Content
			.ReadFromJsonAsync<AdmissionCrmActivationPreviewDto>(JsonOptions))!;
		var evaluatedRoute = evaluated.Items.Single(item => item.Key == "admission_route");
		evaluatedRoute.Status.ShouldBe(AdmissionCrmActivationStepStatus.Completed);
		evaluatedRoute.Detail.ShouldContain("evaluated eligible route assignment");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Allow_Year_Wide_Placement_For_Legacy_Lead_Without_Target_Standard()
	{
		var placementResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/placement-options");
		placementResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await placementResponse.Content.ReadAsStringAsync());
		var placement = (await placementResponse.Content
			.ReadFromJsonAsync<AdmissionCrmPlacementOptionDto[]>(JsonOptions))!.First();
		var lead = await CreateLeadAsync(
			$"legacy-activation-{Guid.NewGuid():N}@example.test",
			placement.StandardName,
			standardId: null);

		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation/preview",
			new PreviewAdmissionCrmActivationDto
			{
				EducationBoardId = placement.EducationBoardId,
				AcademicMediumId = placement.AcademicMediumId,
				AcademicMediumStageId = placement.AcademicMediumStageId,
				StandardId = placement.StandardId,
				DivisionId = placement.DivisionId,
				GrNumberSeriesId = Guid.NewGuid()
			},
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmActivationPreviewDto>(JsonOptions))!;
		var placementCheck = preview.Items.Single(item => item.Key == "placement");
		placementCheck.Status.ShouldBe(AdmissionCrmActivationStepStatus.Completed);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Scope_Placement_Options_And_Reject_Sibling_Standard_For_Request_And_Execute()
	{
		var placementResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/placement-options");
		placementResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await placementResponse.Content.ReadAsStringAsync());
		var allowedPlacement = (await placementResponse.Content
			.ReadFromJsonAsync<AdmissionCrmPlacementOptionDto[]>(JsonOptions))!.First();
		var siblingPlacement = await CreateSiblingPlacementAsync(allowedPlacement);

		using var scopedClient = await CreateStandardScopedClientAsync(
			allowedPlacement.StandardId,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RequestActivation,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation);
		var scopedOptionsResponse = await scopedClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/placement-options");
		scopedOptionsResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await scopedOptionsResponse.Content.ReadAsStringAsync());
		var scopedOptions = (await scopedOptionsResponse.Content
			.ReadFromJsonAsync<AdmissionCrmPlacementOptionDto[]>(JsonOptions))!;
		scopedOptions.ShouldNotBeEmpty();
		scopedOptions.ShouldAllBe(option => option.StandardId == allowedPlacement.StandardId);
		scopedOptions.ShouldNotContain(option =>
			option.StandardId == siblingPlacement.StandardId ||
			option.DivisionId == siblingPlacement.DivisionId);

		var lead = await CreateLeadAsync(
			$"activation-scope-{Guid.NewGuid():N}@example.test",
			allowedPlacement.StandardName,
			allowedPlacement.StandardId);
		var siblingInput = new RequestAdmissionCrmActivationDto
		{
			EducationBoardId = siblingPlacement.EducationBoardId,
			AcademicMediumId = siblingPlacement.AcademicMediumId,
			AcademicMediumStageId = siblingPlacement.AcademicMediumStageId,
			StandardId = siblingPlacement.StandardId,
			DivisionId = siblingPlacement.DivisionId,
			GrNumberSeriesId = Guid.NewGuid(),
			Gender = GenderEnum.Female,
			RequireStudentAccount = false,
			RequireGuardianAccount = false
		};

		var mismatchPreviewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation/preview",
			siblingInput,
			JsonOptions);
		mismatchPreviewResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await mismatchPreviewResponse.Content.ReadAsStringAsync());
		var mismatchPreview = (await mismatchPreviewResponse.Content
			.ReadFromJsonAsync<AdmissionCrmActivationPreviewDto>(JsonOptions))!;
		var placementCheck = mismatchPreview.Items.Single(item => item.Key == "placement");
		placementCheck.Status.ShouldBe(AdmissionCrmActivationStepStatus.Failed);
		placementCheck.Detail.ShouldContain("must match the applicant's target standard");

		var mismatchRequestResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation",
			siblingInput,
			JsonOptions);
		var mismatchRequestBody = await mismatchRequestResponse.Content.ReadAsStringAsync();
		mismatchRequestResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, mismatchRequestBody);
		mismatchRequestBody.ShouldContain("AdmissionCrm:ActivationReadinessFailed");
		mismatchRequestBody.ShouldContain("placement");
		await AssertNoActivationAsync(lead.Id);

		var scopedRequestResponse = await scopedClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation",
			siblingInput,
			JsonOptions);
		await ShouldBeScopeDeniedAsync(scopedRequestResponse);
		await AssertNoActivationAsync(lead.Id);

		var tamperedActivationId = await InsertTamperedActivationAsync(lead.Id, siblingInput);
		var scopedExecuteResponse = await scopedClient.PostAsync(
			$"/api/student-enrollment/admissions-crm/activation/{tamperedActivationId}/execute",
			null);
		await ShouldBeScopeDeniedAsync(scopedExecuteResponse);
		await AssertActivationUnchangedAsync(tamperedActivationId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Roll_Back_Failed_Activation_And_Return_Idempotent_First_Day_Ready_Student()
	{
		var placementResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/placement-options");
		placementResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await placementResponse.Content.ReadAsStringAsync());
		var placements = await placementResponse.Content.ReadFromJsonAsync<AdmissionCrmPlacementOptionDto[]>(JsonOptions);
		var placement = placements.ShouldNotBeNull().First();

		var seriesResponse = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/number-series",
			new CreateAdmissionCrmNumberSeriesDto
			{
				Code = "GR-ACT-" + Guid.NewGuid().ToString("N")[..6], Name = "Activation test GR",
				IdentifierType = AdmissionCrmIdentifierType.GrNumber, EducationBoardId = placement.EducationBoardId,
				AcademicMediumId = placement.AcademicMediumId, StandardId = placement.StandardId,
				Prefix = "TGR-", Padding = 6, StartingSequence = Random.Shared.Next(100000, 800000),
				EffectiveFrom = DateTime.UtcNow.AddDays(-1), EffectiveUntil = DateTime.UtcNow.AddYears(1)
			}, JsonOptions);
		seriesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await seriesResponse.Content.ReadAsStringAsync());
		var series = (await seriesResponse.Content.ReadFromJsonAsync<AdmissionCrmNumberSeriesDto>(JsonOptions))!;
		await PublishSeriesAsDifferentMakerAsync(series.Id);

		// Reuse the seeded identity so this API test validates account linking without invoking an external email provider.
		var email = TestAuthHandler.TestEmail;
		var lead = await CreateLeadAsync(email, placement.StandardName, placement.StandardId);
		var decisionResponse = await Client.PutAsJsonAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/final-admission/decision",
			new RecordAdmissionCrmAdmissionDecisionDto
			{
				Status = AdmissionCrmAdmissionDecisionStatus.Approved,
				Reason = "Applicant cleared final admission review.",
				OverrideUsed = true,
				OverrideReason = "The configured direct-admission route does not require a formal seat offer."
			}, JsonOptions);
		decisionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await decisionResponse.Content.ReadAsStringAsync());

		var identityResponse = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/household-identity",
			new CreateAdmissionCrmApplicantIdentityProfileDto
			{
				HouseholdDisplayName = "Activation household", Applicant = new AdmissionCrmPersonInputDto
				{
					FirstName = lead.FirstName!, Email = email, Phone = lead.PrimaryContactNo,
					DateOfBirth = lead.DateOfBirth?.UtcDateTime, Provenance = AdmissionCrmPersonProvenance.StaffSupplied
				}
			}, JsonOptions);
		identityResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await identityResponse.Content.ReadAsStringAsync());
		var overrideResponse = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/household-identity/override-readiness",
			new OverrideAdmissionCrmIdentityReadinessDto { Reason = "Principal approved identity readiness for controlled activation test." }, JsonOptions);
		overrideResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overrideResponse.Content.ReadAsStringAsync());

		var activationInput = new RequestAdmissionCrmActivationDto
		{
			EducationBoardId = placement.EducationBoardId, AcademicMediumId = placement.AcademicMediumId,
			AcademicMediumStageId = placement.AcademicMediumStageId, StandardId = placement.StandardId,
			DivisionId = placement.DivisionId, GrNumberSeriesId = series.Id, Gender = GenderEnum.Female,
			RequireStudentAccount = false, RequireGuardianAccount = false,
			ServiceHandoffs = [new AdmissionCrmServiceHandoffRequestDto
			{
				Type = AdmissionCrmServiceHandoffType.Library, DestinationModule = "Library",
				ConsentConfirmed = true,
				Payload = new System.Collections.Generic.Dictionary<string, string?> { ["interest"] = "Library access" }
			}]
		};
		var previewResponse = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation/preview", activationInput, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<AdmissionCrmActivationPreviewDto>(JsonOptions))!;
		preview.IsReady.ShouldBeTrue(string.Join("; ", preview.Items.Where(x => x.Status == AdmissionCrmActivationStepStatus.Failed).Select(x => x.Detail)));

		var requestResponse = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation", activationInput, JsonOptions);
		requestResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await requestResponse.Content.ReadAsStringAsync());
		var activation = (await requestResponse.Content.ReadFromJsonAsync<AdmissionCrmActivationDto>(JsonOptions))!;
		var manualSequence = Random.Shared.Next(810000, 890000);
		var manualResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/activation/{activation.Id}/manual-identifier",
			new AssignManualAdmissionCrmIdentifierDto
			{
				NumberSeriesId = series.Id, Sequence = manualSequence,
				Value = $"MAN-GR-{manualSequence}", Reason = "Verified migration from the predecessor admission register."
			}, JsonOptions);
		manualResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await manualResponse.Content.ReadAsStringAsync());

		var policyResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/financial/fee-policies",
			new CreateAdmissionCrmFeePolicyDto
			{
				Code = "ACT-" + Guid.NewGuid().ToString("N")[..8],
				Name = "Activation fee gate",
				Applicability = new AdmissionCrmFeePolicyApplicabilityDto
				{
					DesiredClass = placement.StandardName
				},
				Components =
				[
					new AdmissionCrmFeeComponentDto
					{
						Key = "activation-fee",
						Name = "Activation fee",
						Amount = 500,
						ParentDescription = "Required before student activation."
					}
				],
				GateType = AdmissionCrmPaymentGateType.FinanceCleared,
				GateMilestone = AdmissionCrmPaymentGateMilestone.StudentActivation,
				RequiredAmount = 500,
				EffectiveFrom = DateTime.UtcNow.AddDays(-1),
				EffectiveUntil = DateTime.UtcNow.AddDays(30)
			}, JsonOptions);
		policyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await policyResponse.Content.ReadAsStringAsync());
		var policy = (await policyResponse.Content.ReadFromJsonAsync<AdmissionCrmFeePolicyDto>(JsonOptions))!;
		await PublishFeePolicyAsync(policy.Id);
		var quotationResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/financial/quotations",
			new IssueAdmissionCrmQuotationDto
			{
				FeePolicyId = policy.Id,
				ValidUntil = DateTime.UtcNow.AddDays(2)
			}, JsonOptions);
		quotationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await quotationResponse.Content.ReadAsStringAsync());
		var quotation = (await quotationResponse.Content.ReadFromJsonAsync<AdmissionCrmAdmissionQuotationDto>(JsonOptions))!;

		await ApproveActivationAsDifferentUserAsync(activation.Id);
		using var repairOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RepairActivation);
		var repairPermissionCannotExecuteResponse = await repairOnlyClient.PostAsync(
			$"/api/student-enrollment/admissions-crm/activation/{activation.Id}/execute",
			null);
		repairPermissionCannotExecuteResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await repairPermissionCannotExecuteResponse.Content.ReadAsStringAsync());

		await MarkActivationRepairRequiredAsync(activation.Id);
		using var executeOnlyClient = CreateClientWithCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation);
		var executePermissionCannotRepairResponse = await executeOnlyClient.PostAsync(
			$"/api/student-enrollment/admissions-crm/activation/{activation.Id}/execute",
			null);
		executePermissionCannotRepairResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await executePermissionCannotRepairResponse.Content.ReadAsStringAsync());

		var blockedExecute = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/activation/{activation.Id}/execute",
			null);
		blockedExecute.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await blockedExecute.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:ActivationReadinessChanged");
		var financeResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/financial/quotations/{quotation.Id}/finance-state",
			new RecordAdmissionCrmFinanceStateDto
			{
				State = AdmissionCrmFinanceState.Confirmed,
				ConfirmedAmount = 500,
				FinanceReference = "ACTIVATION-PAID",
				Source = "Finance placeholder confirmation",
				ConfirmedAt = DateTime.UtcNow
			}, JsonOptions);
		financeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await financeResponse.Content.ReadAsStringAsync());

		using (Factory.FailNextAdmissionCrmTimelineSave(AdmissionCrmTimelineEventType.StudentActivated))
		{
			var failedExecution = await Client.PostAsync(
				$"/api/student-enrollment/admissions-crm/activation/{activation.Id}/execute",
				null);
			failedExecution.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await failedExecution.Content.ReadAsStringAsync());
		}
		var afterFailedExecution = await Client.GetFromJsonAsync<AdmissionCrmActivationDto>(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/activation", JsonOptions);
		afterFailedExecution.ShouldNotBeNull();
		afterFailedExecution.Status.ShouldBe(AdmissionCrmActivationStatus.RepairRequired);
		afterFailedExecution.StudentId.ShouldBeNull();
		afterFailedExecution.Identifiers.ShouldHaveSingleItem().Status.ShouldNotBe(AdmissionCrmIdentifierStatus.Committed);

		var executeResponse = await Client.PostAsync($"/api/student-enrollment/admissions-crm/activation/{activation.Id}/execute", null);
		executeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await executeResponse.Content.ReadAsStringAsync());
		activation = (await executeResponse.Content.ReadFromJsonAsync<AdmissionCrmActivationDto>(JsonOptions))!;
		activation.Status.ShouldBe(AdmissionCrmActivationStatus.FirstDayReady, activation.RepairReason);
		activation.StudentId.ShouldNotBeNull();
		var committedIdentifier = activation.Identifiers.ShouldHaveSingleItem();
		committedIdentifier.Status.ShouldBe(AdmissionCrmIdentifierStatus.Committed);
		committedIdentifier.IsManual.ShouldBeTrue();
		var handoff = activation.ServiceHandoffs.ShouldHaveSingleItem();
		handoff.Status.ShouldBe(AdmissionCrmServiceHandoffStatus.Pending);
		var dispatchResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/activation/service-handoffs/{handoff.Id}",
			new UpdateAdmissionCrmServiceHandoffDto
			{
				Status = AdmissionCrmServiceHandoffStatus.Dispatched, ExternalReference = "LIB-REQ-100"
			}, JsonOptions);
		dispatchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await dispatchResponse.Content.ReadAsStringAsync());
		var acknowledgeResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/activation/service-handoffs/{handoff.Id}",
			new UpdateAdmissionCrmServiceHandoffDto
			{
				Status = AdmissionCrmServiceHandoffStatus.Acknowledged, ExternalReference = "LIB-MEMBER-100"
			}, JsonOptions);
		acknowledgeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await acknowledgeResponse.Content.ReadAsStringAsync());
		var governanceResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/activation/number-series/{series.Id}/governance");
		governanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await governanceResponse.Content.ReadAsStringAsync());
		var governance = (await governanceResponse.Content.ReadFromJsonAsync<AdmissionCrmIdentifierGovernanceReportDto>(JsonOptions))!;
		governance.ManualCount.ShouldBe(1);

		var secondExecute = await Client.PostAsync($"/api/student-enrollment/admissions-crm/activation/{activation.Id}/execute", null);
		secondExecute.StatusCode.ShouldBe(HttpStatusCode.OK, await secondExecute.Content.ReadAsStringAsync());
		var repeated = (await secondExecute.Content.ReadFromJsonAsync<AdmissionCrmActivationDto>(JsonOptions))!;
		repeated.StudentId.ShouldBe(activation.StudentId);
		repeated.Identifiers.ShouldHaveSingleItem().Value.ShouldBe(activation.Identifiers[0].Value);
		repeated.AttemptCount.ShouldBe(activation.AttemptCount);

		await RequestCancellationAsDifferentUserAsync(activation.Id);
		var cancellationPreview = await Client.GetAsync($"/api/student-enrollment/admissions-crm/activation/{activation.Id}/cancellation-preview");
		cancellationPreview.StatusCode.ShouldBe(HttpStatusCode.OK, await cancellationPreview.Content.ReadAsStringAsync());
		var previewBody = (await cancellationPreview.Content.ReadFromJsonAsync<AdmissionCrmActivationCancellationPreviewDto>(JsonOptions))!;
		previewBody.CanCancel.ShouldBeTrue(string.Join("; ", previewBody.Checks.Where(x => !x.Passed).Select(x => x.Detail)));

		var cancelResponse = await Client.PostAsync($"/api/student-enrollment/admissions-crm/activation/{activation.Id}/approve-cancellation", null);
		cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cancelResponse.Content.ReadAsStringAsync());
		var cancelled = (await cancelResponse.Content.ReadFromJsonAsync<AdmissionCrmActivationDto>(JsonOptions))!;
		cancelled.Status.ShouldBe(AdmissionCrmActivationStatus.Cancelled);
		cancelled.CancelledAt.ShouldNotBeNull();
		cancelled.Identifiers.ShouldHaveSingleItem().Status.ShouldBe(AdmissionCrmIdentifierStatus.Void);
		(await GetStudentStatusAsync(cancelled.StudentId!.Value)).ShouldBe(StudentLifeCycleStatusEnum.InActive);
	}

	private async Task<AdmissionCrmPlacementOptionDto> CreateSiblingPlacementAsync(
		AdmissionCrmPlacementOptionDto reference)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var standardManager = scope.ServiceProvider.GetRequiredService<StandardManager>();
		var divisionManager = scope.ServiceProvider.GetRequiredService<DivisionManager>();
		var standardRepository = scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>();
		var divisionRepository = scope.ServiceProvider.GetRequiredService<IRepository<Division, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (tenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var suffix = Guid.NewGuid().ToString("N")[..8];
			var standard = await standardManager.CreateAsync(
				reference.AcademicMediumStageId,
				$"Sibling {suffix}",
				7,
				shiftId: null);
			await standardRepository.InsertAsync(standard, autoSave: false);
			var division = await divisionManager.CreateAsync(
				standard.Id,
				$"Scope {suffix}",
				30,
				$"S-{suffix[..4]}");
			await divisionRepository.InsertAsync(division, autoSave: false);
			await uow.CompleteAsync();

			return new AdmissionCrmPlacementOptionDto
			{
				EducationBoardId = reference.EducationBoardId,
				BoardName = reference.BoardName,
				AcademicMediumId = reference.AcademicMediumId,
				MediumName = reference.MediumName,
				AcademicMediumStageId = reference.AcademicMediumStageId,
				StageName = reference.StageName,
				StandardId = standard.Id,
				StandardName = standard.Name,
				DivisionId = division.Id,
				DivisionName = division.Name,
				DivisionStrength = division.Strength
			};
		}
	}

	private async Task<HttpClient> CreateStandardScopedClientAsync(
		Guid standardId,
		params string[] permissions)
	{
		var userId = Guid.NewGuid();
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (tenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					academicYearId: Factory.SeededAcademicYearId,
					standardId: standardId,
					description: "Admissions activation standard-only scope regression."),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString("D"));
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "teacher");
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}

	private async Task AssertNoActivationAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmActivation, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await repository.GetQueryableAsync();
			(await queryable.AnyAsync(entity => entity.LeadId == leadId)).ShouldBeFalse();
			await uow.CompleteAsync();
		}
	}

	private static async Task ShouldBeScopeDeniedAsync(HttpResponseMessage response)
	{
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBeOneOf(
			new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden },
			body);
		body.ShouldContain("UserScope:AccessDenied");
	}

	private async Task<Guid> InsertTamperedActivationAsync(
		Guid leadId,
		RequestAdmissionCrmActivationDto input)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmActivation, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (tenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var id = Guid.NewGuid();
			await repository.InsertAsync(
				new AdmissionCrmActivation(
					id,
					tenantId,
					Factory.SeededAcademicYearId,
					leadId,
					$"ACT-TAMPER-{Guid.NewGuid():N}"[..32],
					input.EducationBoardId,
					input.AcademicMediumId,
					input.AcademicMediumStageId,
					input.StandardId,
					input.DivisionId,
					input.GrNumberSeriesId,
					input.RollNumber,
					input.Gender,
					"{}",
					"[]",
					requireStudentAccount: false,
					requireGuardianAccount: false,
					requestedByUserId: Guid.NewGuid(),
					requestedAt: DateTime.UtcNow),
				autoSave: false);
			await uow.CompleteAsync();
			return id;
		}
	}

	private async Task AssertActivationUnchangedAsync(Guid activationId)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmActivation, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var activation = await repository.GetAsync(activationId);
			activation.Status.ShouldBe(AdmissionCrmActivationStatus.ApprovalPending);
			activation.AttemptCount.ShouldBe(0);
			await uow.CompleteAsync();
		}
	}

	private async Task MarkActivationRepairRequiredAsync(Guid activationId)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmActivation, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var activation = await repository.GetAsync(activationId);
			activation.RequireRepair(
				"permission-gate",
				"Controlled repair state used to verify the dedicated repair permission.");
			await repository.UpdateAsync(activation);
			await uow.CompleteAsync();
		}
	}

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

	private async Task PublishSeriesAsDifferentMakerAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmNumberSeries, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entity = await repository.GetAsync(id); entity.Submit(Guid.NewGuid()); entity.Publish(Guid.NewGuid());
			await repository.UpdateAsync(entity); await uow.CompleteAsync();
		}
	}

	private async Task SeedExistingStudentInDivisionAsync(AdmissionCrmPlacementOptionDto placement)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var existingStudent = new Student(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				"Existing",
				null,
				"Student",
				Factory.SeededAcademicYearId,
				placement.EducationBoardId,
				placement.AcademicMediumId,
				placement.AcademicMediumStageId,
				placement.StandardId,
				placement.DivisionId,
				status: StudentLifeCycleStatusEnum.InActive);
			await repository.InsertAsync(existingStudent);
			await uow.CompleteAsync();
		}
	}

	private async Task RecordEligibleRouteAssignmentAsync(
		AdmissionCrmLeadDto lead,
		AdmissionCrmQuickInquiryRouteOptionDto route)
	{
		var routeDefinitionId = route.RouteDefinitionId ??
			throw new InvalidOperationException("A published route definition is required.");
		var routeVersion = route.Version ??
			throw new InvalidOperationException("A published route version is required.");
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmRouteAssignment, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await repository.GetQueryableAsync();
			var current = await queryable.FirstOrDefaultAsync(assignment =>
				assignment.TenantId == Guid.Parse(TestAuthHandler.TestTenantId) &&
				assignment.LeadId == lead.Id &&
				assignment.IsPrimary);
			current?.Supersede();
			if (current != null)
			{
				await repository.UpdateAsync(current);
			}

			var assignedBy = Guid.NewGuid();
			var assignment = new AdmissionCrmRouteAssignment(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				lead.AcademicYearId,
				lead.Id,
				routeDefinitionId,
				routeVersion,
				route.RouteType,
				"{\"previousSchool\":\"Previous School\",\"previousBoard\":\"State Board\",\"lastClassCompleted\":\"Class 3\"}",
				"[\"PREVIOUS_SCHOOL_RECORD:PSR-1\",\"TRANSFER_CERTIFICATE:TC-1\"]",
				current?.Id,
				"Validated lateral-entry evidence.",
				assignedBy,
				DateTime.UtcNow);
			assignment.Evaluate(
				AdmissionCrmRouteEligibilityStatus.Eligible,
				"Transfer history and evidence verified.",
				Guid.NewGuid(),
				DateTime.UtcNow);
			await repository.InsertAsync(assignment);
			await uow.CompleteAsync();
		}
	}

	private async Task RecordUnrelatedEligibleRouteChangeAsync(
		AdmissionCrmLeadDto lead,
		AdmissionCrmQuickInquiryRouteOptionDto route)
	{
		var routeDefinitionId = route.RouteDefinitionId ??
			throw new InvalidOperationException("A published route definition is required.");
		var routeVersion = route.Version ??
			throw new InvalidOperationException("A published route version is required.");
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmRouteAssignment, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			AdmissionCrmRouteAssignment Create(Guid? previousAssignmentId)
			{
				var assignment = new AdmissionCrmRouteAssignment(
					Guid.NewGuid(),
					Guid.Parse(TestAuthHandler.TestTenantId),
					lead.AcademicYearId,
					lead.Id,
					routeDefinitionId,
					routeVersion,
					route.RouteType,
					"{\"previousSchool\":\"Unrelated School\",\"previousBoard\":\"Other Board\",\"lastClassCompleted\":\"Class 3\"}",
					"[\"TRANSFER_RECORD:UNRELATED-1\"]",
					previousAssignmentId,
					"Unrelated route history must not satisfy the captured pathway.",
					Guid.NewGuid(),
					DateTime.UtcNow);
				assignment.Evaluate(
					AdmissionCrmRouteEligibilityStatus.Eligible,
					"Unrelated route evaluated.",
					Guid.NewGuid(),
					DateTime.UtcNow);
				return assignment;
			}

			var previous = Create(null);
			previous.Supersede();
			var current = Create(previous.Id);
			await repository.InsertManyAsync([previous, current]);
			await uow.CompleteAsync();
		}
	}

	private async Task<AdmissionCrmQuickInquiryRouteOptionDto> CreatePublishedRouteDefinitionAsync(
		AdmissionCrmRouteType routeType)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var suffix = Guid.NewGuid().ToString("N")[..8];
			var definition = new AdmissionCrmRouteDefinition(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				$"ACT-{suffix}",
				$"Activation {routeType} {suffix}",
				routeType,
				1,
				"{\"requiresPolicyEvaluation\":true,\"requiresDistinctApproval\":true,\"requiredEvidenceCodes\":[]}",
				DateTime.UtcNow.AddDays(-1),
				DateTime.UtcNow.AddDays(1));
			definition.Submit(Guid.NewGuid());
			definition.Publish(Guid.NewGuid());
			await repository.InsertAsync(definition);
			await uow.CompleteAsync();
			return new AdmissionCrmQuickInquiryRouteOptionDto
			{
				RouteDefinitionId = definition.Id,
				Name = definition.Name,
				RouteType = definition.RouteType,
				Version = definition.Version
			};
		}
	}

	private async Task PublishFeePolicyAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmFeePolicy, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entity = await repository.GetAsync(id);
			entity.Submit(Guid.NewGuid(), DateTime.UtcNow);
			entity.Approve(Guid.NewGuid(), DateTime.UtcNow);
			entity.Publish(DateTime.UtcNow);
			await repository.UpdateAsync(entity);
			await uow.CompleteAsync();
		}
	}

	private async Task ApproveActivationAsDifferentUserAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmActivation, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entity = await repository.GetAsync(id); entity.Approve(Guid.NewGuid(), DateTime.UtcNow);
			await repository.UpdateAsync(entity); await uow.CompleteAsync();
		}
	}

	private async Task RequestCancellationAsDifferentUserAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmActivation, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entity = await repository.GetAsync(id);
			entity.RequestCancellation("Controlled same-cycle cancellation test.", Guid.NewGuid(), DateTime.UtcNow);
			await repository.UpdateAsync(entity);
			await uow.CompleteAsync();
		}
	}

	private async Task<StudentLifeCycleStatusEnum> GetStudentStatusAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
			return await dbContext.Students.AsNoTracking().IgnoreAutoIncludes()
				.Where(x => x.Id == id).Select(x => x.Status).SingleAsync();
		}
	}

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync(
		string email,
		string standardName,
		Guid? standardId)
	{
		var response = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Final activation", DesiredClassName = standardName, DateOfBirth = DateTimeOffset.UtcNow.AddYears(-7),
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999), Email = email,
				Channel = AdmissionInquiryChannel.QuickCallEntry, FirstName = "Activate" + Guid.NewGuid().ToString("N")[..6], LastName = "Student",
				TargetStandardId = standardId
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}
}
