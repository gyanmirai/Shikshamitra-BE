using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
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
	public async Task Should_Preview_Activate_And_Return_Idempotent_First_Day_Ready_Student()
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
		var lead = await CreateLeadAsync(email, placement.StandardName);
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

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync(string email, string standardName)
	{
		var response = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Final activation", DesiredClassName = standardName, DateOfBirth = DateTimeOffset.UtcNow.AddYears(-7),
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999), Email = email,
				Channel = AdmissionInquiryChannel.QuickCallEntry, FirstName = "Activate" + Guid.NewGuid().ToString("N")[..6], LastName = "Student"
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}
}
