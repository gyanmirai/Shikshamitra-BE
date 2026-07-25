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
		var missingEvidenceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route",
			new AssignAdmissionCrmSpecialRouteDto
			{
				RouteDefinitionId = definition.Id,
				RouteData = assignmentInput.RouteData,
				EvidenceReferences = ["ALLOTMENT:RTE-ALLOT-100"],
				Reason = "Incomplete evidence must not create an assignment."
			}, JsonOptions);
		missingEvidenceResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingEvidenceResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RequiredRouteEvidenceMissing");

		var nullEvidenceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route",
			new
			{
				routeDefinitionId = definition.Id,
				routeData = assignmentInput.RouteData,
				evidenceReferences = (string[]?)null,
				reason = "A null evidence collection must produce a controlled validation error."
			}, JsonOptions);
		nullEvidenceResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var nullEvidenceBody = await nullEvidenceResponse.Content.ReadAsStringAsync();
		nullEvidenceBody.ShouldContain("Validation failed");
		nullEvidenceBody.ShouldNotContain("Internal Server Error");

		var malformedEvidenceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route/preview",
			new AssignAdmissionCrmSpecialRouteDto
			{
				RouteDefinitionId = definition.Id,
				RouteData = assignmentInput.RouteData,
				EvidenceReferences = ["ALLOTMENT", "AGE_PROOF:BIRTH_CERTIFICATE"],
				Reason = "Opaque evidence without a configured code separator is invalid."
			}, JsonOptions);
		malformedEvidenceResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await malformedEvidenceResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:RouteEvidenceReferenceInvalid");

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

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Not_Treat_An_Arbitrary_Student_Id_As_A_Verified_Sibling()
	{
		var definitionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/admission-routes",
			new CreateAdmissionCrmRouteDefinitionDto
			{
				Code = "SIB-" + Guid.NewGuid().ToString("N")[..6],
				Name = "Verified sibling priority",
				RouteType = AdmissionCrmRouteType.SiblingPriority,
				Configuration = new AdmissionCrmRouteConfigurationDto
				{
					RequiredEvidenceCodes = ["SIBLING_RELATIONSHIP"]
				},
				EffectiveFrom = DateTime.UtcNow.AddDays(-1),
				EffectiveUntil = DateTime.UtcNow.AddYears(1)
			}, JsonOptions);
		definitionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await definitionResponse.Content.ReadAsStringAsync());
		var definition = (await definitionResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRouteDefinitionDto>(JsonOptions))!;
		await PublishDefinitionAsDifferentMakerAsync(definition.Id);

		var unrelatedStudentId = await CreateStudentAsync("8111111111");
		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Sibling priority admission",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Applicant",
				PrimaryContactNo = "9222222222",
				IntendedRouteDefinitionId = definition.Id
			}, JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = (await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;

		var assignmentResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route",
			new AssignAdmissionCrmSpecialRouteDto
			{
				RouteDefinitionId = definition.Id,
				RouteData = new AdmissionCrmSpecialRouteDataDto
				{
					RelatedStudentId = unrelatedStudentId
				},
				EvidenceReferences = ["SIBLING_RELATIONSHIP:CLAIM-100"],
				Reason = "A student identifier alone must not establish a sibling relationship."
			}, JsonOptions);
		assignmentResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await assignmentResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:SiblingRelationshipNotVerified");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Allow_The_Captured_Route_Version_To_Receive_Its_First_Assignment_After_Retirement()
	{
		var definitionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/admission-routes",
			new CreateAdmissionCrmRouteDefinitionDto
			{
				Code = "LAT-" + Guid.NewGuid().ToString("N")[..6],
				Name = "Captured lateral admission",
				RouteType = AdmissionCrmRouteType.Lateral,
				Configuration = new AdmissionCrmRouteConfigurationDto
				{
					RequiredEvidenceCodes = ["TRANSFER_RECORD"]
				},
				EffectiveFrom = DateTime.UtcNow.AddDays(-1),
				EffectiveUntil = DateTime.UtcNow.AddYears(1)
			}, JsonOptions);
		definitionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await definitionResponse.Content.ReadAsStringAsync());
		var definition = (await definitionResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRouteDefinitionDto>(JsonOptions))!;
		await PublishDefinitionAsDifferentMakerAsync(definition.Id);

		var leadResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Captured lateral pathway",
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Captured",
				PrimaryContactNo = "9333333333",
				IntendedRouteDefinitionId = definition.Id
			}, JsonOptions);
		leadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await leadResponse.Content.ReadAsStringAsync());
		var lead = (await leadResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
		await RetireDefinitionAsync(definition.Id);

		var assignmentResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/admission-route",
			new AssignAdmissionCrmSpecialRouteDto
			{
				RouteDefinitionId = definition.Id,
				RouteData = new AdmissionCrmSpecialRouteDataDto
				{
					PreviousSchool = "Prior School",
					PreviousBoard = "State Board",
					LastClassCompleted = "Grade 4"
				},
				EvidenceReferences = ["TRANSFER_RECORD:TC-100"],
				Reason = "Complete the immutable route selected while this version was published."
			}, JsonOptions);
		assignmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignmentResponse.Content.ReadAsStringAsync());
		var assignment = (await assignmentResponse.Content
			.ReadFromJsonAsync<AdmissionCrmRouteAssignmentDto>(JsonOptions))!;
		assignment.RouteDefinitionId.ShouldBe(definition.Id);
		assignment.RouteDefinitionVersion.ShouldBe(definition.Version);
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

	private async Task RetireDefinitionAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider
			.GetRequiredService<IRepository<AdmissionCrmRouteDefinition, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var definition = await repository.GetAsync(id);
			definition.Retire();
			await repository.UpdateAsync(definition);
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> CreateStudentAsync(string primaryContactNo)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (tenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var student = Student.CreateDirectRegistration(
				Guid.NewGuid(),
				tenantId,
				"Unrelated",
				null,
				"Student",
				Factory.SeededAcademicYearId,
				Factory.SeededBoardId,
				Factory.SeededMediumId,
				Factory.SeededMediumStageId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				DateTimeOffset.UtcNow.AddYears(-10),
				GenderEnum.Female,
				primaryContactNo);
			await repository.InsertAsync(student);
			await uow.CompleteAsync();
			return student.Id;
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
