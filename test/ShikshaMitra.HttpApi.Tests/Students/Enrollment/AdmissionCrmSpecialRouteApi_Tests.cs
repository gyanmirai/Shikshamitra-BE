using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
public class AdmissionCrmSpecialRouteApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmSpecialRouteApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Govern_Rte_Assignment_Eligibility_And_Immutable_Route_History()
	{
		var definitionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/admission-routes",
			new CreateAdmissionCrmRouteDefinitionDto
			{
				Code = "RTE-" + Guid.NewGuid().ToString("N")[..6],
				Name = "Gujarat RTE allotment",
				RouteType = AdmissionCrmRouteType.RteEws,
				Configuration = new AdmissionCrmRouteConfigurationDto
				{
					RequiresPolicyEvaluation = true,
					RequiresDistinctApproval = true,
					RequiredEvidenceCodes = ["ALLOTMENT", "AGE_PROOF"]
				},
				EffectiveFrom = DateTime.UtcNow.AddDays(-1),
				EffectiveUntil = DateTime.UtcNow.AddYears(1)
			}, JsonOptions);
		definitionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await definitionResponse.Content.ReadAsStringAsync());
		var definition = (await definitionResponse.Content.ReadFromJsonAsync<AdmissionCrmRouteDefinitionDto>(JsonOptions))!;
		await PublishDefinitionAsDifferentMakerAsync(definition.Id);

		var lead = await CreateLeadAsync();
		var invalidResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route",
			new AssignAdmissionCrmSpecialRouteDto
			{
				RouteDefinitionId = definition.Id,
				RouteData = new AdmissionCrmSpecialRouteDataDto { AuthorityName = "Gujarat RTE" },
				Reason = "Government allotment received."
			}, JsonOptions);
		invalidResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await invalidResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:RteAllotmentDetailsRequired");

		var assignmentInput = new AssignAdmissionCrmSpecialRouteDto
			{
				RouteDefinitionId = definition.Id,
				RouteData = new AdmissionCrmSpecialRouteDataDto
				{
					AuthorityName = "Gujarat RTE",
					AllotmentNumber = "RTE-ALLOT-100",
					AllotmentRound = "Round 1",
					AdmissionDeadline = DateTime.UtcNow.AddDays(10),
					AllottedClass = "Grade 1"
				},
				EvidenceReferences = ["ALLOTMENT:RTE-ALLOT-100", "AGE_PROOF:BIRTH_CERTIFICATE"],
				Reason = "Official allotment matched to applicant."
			};
		var previewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route/preview", assignmentInput, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var impact = (await previewResponse.Content.ReadFromJsonAsync<AdmissionCrmRouteImpactPreviewDto>(JsonOptions))!;
		impact.TargetRoute.RouteType.ShouldBe(AdmissionCrmRouteType.RteEws);
		impact.Impacts.ShouldContain(x => x.Area == "Policy");

		var assignmentResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route", assignmentInput, JsonOptions);
		assignmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignmentResponse.Content.ReadAsStringAsync());
		var assignment = (await assignmentResponse.Content.ReadFromJsonAsync<AdmissionCrmRouteAssignmentDto>(JsonOptions))!;

		var selfApproval = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/admission-route-assignments/{assignment.Id}/evaluate",
			new EvaluateAdmissionCrmSpecialRouteDto
			{
				Status = AdmissionCrmRouteEligibilityStatus.Eligible,
				Reason = "Self approval attempt."
			}, JsonOptions);
		selfApproval.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await selfApproval.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:DistinctRouteEligibilityApproverRequired");
		await EvaluateAsDifferentUserAsync(assignment.Id);

		var historyResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = (await historyResponse.Content.ReadFromJsonAsync<AdmissionCrmRouteAssignmentDto[]>(JsonOptions))!;
		history.ShouldHaveSingleItem();
		history.Single().Status.ShouldBe(AdmissionCrmRouteEligibilityStatus.Eligible);
		history.Single().RouteData.AllotmentNumber.ShouldBe("RTE-ALLOT-100");

		var appealResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/admission-route-assignments/{assignment.Id}/appeals",
			new RequestAdmissionCrmRouteAppealDto
			{
				RequestedStatus = AdmissionCrmRouteEligibilityStatus.ExceptionApproved,
				Reason = "Authority correction requires exception review.", EvidenceReferences = ["AUTH-CORRECTION-1"]
			}, JsonOptions);
		appealResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await appealResponse.Content.ReadAsStringAsync());
		var appeal = (await appealResponse.Content.ReadFromJsonAsync<AdmissionCrmRouteAppealDto>(JsonOptions))!;
		var selfAppealDecision = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/admission-route-appeals/{appeal.Id}/decision",
			new DecideAdmissionCrmRouteAppealDto { Approved = true, Reason = "Self approval attempt." }, JsonOptions);
		selfAppealDecision.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await selfAppealDecision.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:DistinctRouteAppealApproverRequired");

		var duplicateLead = await CreateLeadAsync();
		var importResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/rte-allotments/import",
			new ImportAdmissionCrmRteAllotmentsDto
			{
				Rows = [new ImportAdmissionCrmRteAllotmentRowDto
				{
					LeadId = duplicateLead.Id, RouteDefinitionId = definition.Id,
					RouteData = assignmentInput.RouteData, EvidenceReferences = assignmentInput.EvidenceReferences
				}]
			}, JsonOptions);
		importResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await importResponse.Content.ReadAsStringAsync());
		var import = (await importResponse.Content.ReadFromJsonAsync<AdmissionCrmRteAllotmentImportResultDto>(JsonOptions))!;
		import.Failed.ShouldBe(1);
		import.Items.Single().ErrorCode.ShouldBe("AdmissionCrm:RteAllotmentAlreadyImported");
	}

	private async Task PublishDefinitionAsDifferentMakerAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entity = await repository.GetAsync(id);
			entity.Submit(Guid.NewGuid());
			entity.Publish(Guid.NewGuid());
			await repository.UpdateAsync(entity);
			await uow.CompleteAsync();
		}
	}

	private async Task EvaluateAsDifferentUserAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmRouteAssignment, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entity = await repository.GetAsync(id);
			entity.Evaluate(AdmissionCrmRouteEligibilityStatus.Eligible,
				"Authority evidence verified by a distinct reviewer.", Guid.NewGuid(), DateTime.UtcNow);
			await repository.UpdateAsync(entity);
			await uow.CompleteAsync();
		}
	}

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync()
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "RTE Grade 1 admission",
				DesiredClassName = "Grade 1",
				DateOfBirth = DateTimeOffset.UtcNow.AddYears(-6),
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Rte" + Guid.NewGuid().ToString("N")[..6]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}
}
