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
public class AdmissionCrmPolicyGovernanceApi_Tests : ShikshaMitraApiTestBase
{
	private const string SourceUrl = "https://www.cbse.gov.in/cbsenew/admission.html";

	public AdmissionCrmPolicyGovernanceApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Install_Official_Baselines_Idempotently_As_Governed_Drafts()
	{
		var firstResponse = await Client.PostAsync(
			"/api/student-enrollment/admissions-crm/policy-packs/install-official-baselines", null);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
		var first = (await firstResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyBaselineInstallResultDto>(JsonOptions))!;
		first.CreatedDrafts.Select(x => x.Code).ShouldContain("CBSE-ADMISSION-CORE");
		first.CreatedDrafts.Select(x => x.Code).ShouldContain("GUJARAT-RTE-2025");
		first.CreatedDrafts.Select(x => x.Code).ShouldContain("GUJARAT-BOARD-READINESS");
		first.CreatedDrafts.Select(x => x.Code).ShouldContain("APAAR-IDENTITY-READINESS");
		first.CreatedDrafts.ShouldAllBe(x => x.Status == AdmissionCrmPolicyPackStatus.Draft);

		var secondResponse = await Client.PostAsync(
			"/api/student-enrollment/admissions-crm/policy-packs/install-official-baselines", null);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await secondResponse.Content.ReadAsStringAsync());
		var second = (await secondResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyBaselineInstallResultDto>(JsonOptions))!;
		second.CreatedDrafts.ShouldBeEmpty();
		second.AlreadyCurrentCodes.Count.ShouldBe(4);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Publish_Adopt_Evaluate_And_Govern_Exception_Request()
	{
		var createResponse = await Client.PostAsJsonAsync("/api/student-enrollment/admissions-crm/policy-packs",
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "CBSE-GJ-" + Guid.NewGuid().ToString("N")[..6],
				Name = "CBSE Gujarat admission core",
				Jurisdiction = "Gujarat",
				Board = "CBSE",
				EffectiveFrom = DateTime.UtcNow.AddDays(-1),
				EffectiveUntil = DateTime.UtcNow.AddYears(1),
				ReviewDueAt = DateTime.UtcNow.AddMonths(6),
				Sources = [new AdmissionCrmPolicySourceDto
				{
					Authority = "CBSE", Title = "Admission of Students to a School", Url = SourceUrl,
					PublishedOrEffectiveAt = DateTime.UtcNow.AddYears(-1), VerifiedAt = DateTime.UtcNow,
					ApplicabilityNote = "CBSE rules with Gujarat state age eligibility."
				}],
				Rules =
				[
					new AdmissionCrmPolicyRuleDto
					{
						Code = "GJ-AGE-G1", Name = "Grade 1 age eligibility",
						Type = AdmissionCrmPolicyRuleType.AgeEligibility,
						Severity = AdmissionCrmPolicyRuleSeverity.Blocking,
						Description = "Cycle-controlled Gujarat age rule.", DesiredClass = "Grade 1",
						AgeCutoffDate = new DateTime(DateTime.UtcNow.Year, 6, 1, 0, 0, 0, DateTimeKind.Utc),
						MinimumAgeYears = 5, SourceAuthority = "Gujarat Education Department", SourceUrl = SourceUrl
					},
					new AdmissionCrmPolicyRuleDto
					{
						Code = "CBSE-TC", Name = "Transfer evidence",
						Type = AdmissionCrmPolicyRuleType.DocumentEvidence,
						Severity = AdmissionCrmPolicyRuleSeverity.ApprovalRequired,
						Description = "Transfer evidence must be verified.", RequiredEvidenceCode = "TC",
						ManualVerificationRequired = true, SourceAuthority = "CBSE", SourceUrl = SourceUrl
					}
				]
			}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var pack = (await createResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyPackDto>(JsonOptions))!;
		await SubmitAsDifferentMakerAsync(pack.Id);

		var approveResponse = await Client.PostAsync($"/api/student-enrollment/admissions-crm/policy-packs/{pack.Id}/approve", null);
		approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await approveResponse.Content.ReadAsStringAsync());
		var publishResponse = await Client.PostAsync($"/api/student-enrollment/admissions-crm/policy-packs/{pack.Id}/publish", null);
		publishResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await publishResponse.Content.ReadAsStringAsync());

		var adoptResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/policy-adoptions/{pack.Id}",
			new AdoptAdmissionCrmPolicyPackDto
			{
				Applicability = new AdmissionCrmPolicyApplicabilityDto { Board = "CBSE", DesiredClass = "Grade 1" },
				EffectiveFrom = DateTime.UtcNow.AddDays(-1), EffectiveUntil = DateTime.UtcNow.AddYears(1)
			}, JsonOptions);
		adoptResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await adoptResponse.Content.ReadAsStringAsync());
		var adoption = (await adoptResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyAdoptionDto>(JsonOptions))!;

		var lead = await CreateLeadAsync();
		var ageResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/policy-evaluations",
			new EvaluateAdmissionCrmPolicyRuleDto { PolicyAdoptionId = adoption.Id, RuleCode = "GJ-AGE-G1" }, JsonOptions);
		ageResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await ageResponse.Content.ReadAsStringAsync());
		var ageEvaluation = (await ageResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyEvaluationDto>(JsonOptions))!;
		ageEvaluation.Status.ShouldBe(AdmissionCrmPolicyEvaluationStatus.Satisfied);
		ageEvaluation.Explanation.ShouldContain("Applicant age is");

		var evidenceResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/policy-evaluations",
			new EvaluateAdmissionCrmPolicyRuleDto
			{
				PolicyAdoptionId = adoption.Id, RuleCode = "CBSE-TC",
				ManualStatus = AdmissionCrmPolicyEvaluationStatus.NotSatisfied,
				EvidenceReferences = ["TC-PENDING"], Explanation = "Countersigned transfer certificate is pending."
			}, JsonOptions);
		evidenceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await evidenceResponse.Content.ReadAsStringAsync());
		var evidenceEvaluation = (await evidenceResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyEvaluationDto>(JsonOptions))!;

		var exceptionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/policy-evaluations/{evidenceEvaluation.Id}/exceptions",
			new RequestAdmissionCrmPolicyExceptionDto
			{
				Reason = "Authority confirmation is expected within the permitted review window.",
				EvidenceReference = "AUTH-CASE-100", ExpiresAt = DateTime.UtcNow.AddDays(7)
			}, JsonOptions);
		exceptionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await exceptionResponse.Content.ReadAsStringAsync());
		var exception = (await exceptionResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyExceptionDto>(JsonOptions))!;
		var sameMakerDecision = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/policy-exceptions/{exception.Id}/decision",
			new DecideAdmissionCrmPolicyExceptionDto { Approve = true, Reason = "Self approval attempt." }, JsonOptions);
		sameMakerDecision.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await sameMakerDecision.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:DistinctPolicyExceptionApproverRequired");

		var readinessResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/leads/{lead.Id}/policy-readiness");
		readinessResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await readinessResponse.Content.ReadAsStringAsync());
		var readiness = (await readinessResponse.Content.ReadFromJsonAsync<AdmissionCrmPolicyReadinessDto>(JsonOptions))!;
		readiness.BlockingFailureCount.ShouldBe(1);
		readiness.Exceptions.Single().Status.ShouldBe(AdmissionCrmPolicyExceptionStatus.ApprovalPending);
	}

	private async Task SubmitAsDifferentMakerAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmPolicyPack, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var pack = await repository.GetAsync(id);
			pack.Submit(Guid.NewGuid(), DateTime.UtcNow);
			await repository.UpdateAsync(pack);
			await uow.CompleteAsync();
		}
	}

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync()
	{
		var response = await Client.PostAsJsonAsync($"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Grade 1 admission", DesiredClassName = "Grade 1",
				DateOfBirth = DateTimeOffset.UtcNow.AddYears(-6), PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry, FirstName = "Policy" + Guid.NewGuid().ToString("N")[..6]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}
}
