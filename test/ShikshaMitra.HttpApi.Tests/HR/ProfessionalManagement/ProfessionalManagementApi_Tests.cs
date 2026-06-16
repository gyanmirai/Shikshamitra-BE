using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class ProfessionalManagementApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/professional-management";

	public ProfessionalManagementApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Templates_List_Without_Exception()
	{
		var created = await CreateTemplateAsync($"CBSE API Template {Guid.NewGuid():N}");
		await PublishTemplateAsync(created.Id);

		var response = await Client.GetAsync($"{BaseUrl}/templates?page=1&pageSize=50");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var body = await response.Content.ReadFromJsonAsync<PerformanceTemplateListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldContain(x =>
			x.Id == created.Id &&
			x.BoardType == PerformanceBoardType.CBSE &&
			x.Sections.Any(section => section.SqaaDomain == PerformanceSqaaDomain.CurriculumPedagogyAssessment));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Sqaaf_Evidence_Report_With_Board_And_Staff_Filters_Without_Exception()
	{
		var created = await CreateTemplateAsync($"CBSE SQAAF API Template {Guid.NewGuid():N}");
		await PublishTemplateAsync(created.Id);

		var response = await Client.GetAsync($"{BaseUrl}/reports/sqaaf-evidence?boardType=1&staffCategory=0");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var rows = await response.Content.ReadFromJsonAsync<PerformanceSqaaEvidenceDto[]>(JsonOptions);
		rows.ShouldNotBeNull();
		rows.ShouldContain(x =>
			x.Domain == PerformanceSqaaDomain.CurriculumPedagogyAssessment &&
			x.TemplateCount >= 1 &&
			x.MappedCriteria >= 1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Sqaaf_Readiness_Report_With_Domain_Summaries_Without_Exception()
	{
		var created = await CreateTemplateAsync($"CBSE SQAAF Readiness API Template {Guid.NewGuid():N}");
		await PublishTemplateAsync(created.Id);

		var response = await Client.GetAsync($"{BaseUrl}/reports/sqaaf-readiness?boardType=1&staffCategory=0");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var rows = await response.Content.ReadFromJsonAsync<PerformanceSqaaReadinessDomainDto[]>(JsonOptions);
		rows.ShouldNotBeNull();
		rows.ShouldContain(x =>
			x.Domain == PerformanceSqaaDomain.CurriculumPedagogyAssessment &&
			x.MappedCriteria >= 1 &&
			x.ReadinessPercentage >= 0 &&
			x.ReadinessPercentage <= 100);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Sqaaf_Audit_Pack_With_Missing_Evidence_Checklist_Without_Exception()
	{
		var created = await CreateTemplateAsync($"CBSE SQAAF Audit Pack API Template {Guid.NewGuid():N}");
		await PublishTemplateAsync(created.Id);

		var response = await Client.GetAsync($"{BaseUrl}/reports/sqaaf-audit-pack?boardType=1&staffCategory=0");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var body = await response.Content.ReadFromJsonAsync<PerformanceSqaaAuditPackDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Domains.ShouldContain(x => x.Domain == PerformanceSqaaDomain.CurriculumPedagogyAssessment);
		body.MissingEvidenceItems.ShouldNotBeNull();
		body.OverallReadinessPercentage.ShouldBeInRange(0, 100);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Sqaaf_Audit_Pack_Export_Without_Exception()
	{
		var created = await CreateTemplateAsync($"CBSE SQAAF Export API Template {Guid.NewGuid():N}");
		await PublishTemplateAsync(created.Id);

		var response = await Client.GetAsync($"{BaseUrl}/reports/sqaaf-audit-pack/export?boardType=1&staffCategory=0");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var body = await response.Content.ReadFromJsonAsync<PerformanceSqaaAuditPackExportDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.FileName.ShouldEndWith(".csv");
		body.ContentType.ShouldBe("text/csv");
		body.ContentBase64.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Defaults_With_Cbse_Gseb_And_Sqaaf_Metadata()
	{
		var response = await Client.GetAsync($"{BaseUrl}/templates/defaults");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var body = await response.Content.ReadFromJsonAsync<DefaultPerformanceTemplatesResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldContain(x => x.BoardType == PerformanceBoardType.CBSE && x.Name.Contains("CBSE"));
		body.Items.ShouldContain(x => x.BoardType == PerformanceBoardType.GSEB && x.Name.Contains("GSEB"));
		body.Items.SelectMany(x => x.Sections).ShouldContain(x =>
			x.SqaaDomain == PerformanceSqaaDomain.CurriculumPedagogyAssessment);
		body.Items.SelectMany(x => x.Sections).SelectMany(x => x.Criteria).ShouldContain(x => x.EvidenceRequired);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Review_Evidence_For_Workspace()
	{
		var (assignmentId, criterionId) = await SeedReviewEvidenceAsync();

		var response = await Client.GetAsync($"{BaseUrl}/reviews/{assignmentId}/evidence");
		var raw = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var rows = await response.Content.ReadFromJsonAsync<PerformanceEvidenceDto[]>(JsonOptions);
		rows.ShouldNotBeNull();
		var evidence = rows.Single(x => x.Title == "Lesson plan evidence API");
		evidence.ReviewAssignmentId.ShouldBe(assignmentId);
		evidence.CriterionId.ShouldBe(criterionId);
		evidence.FileName.ShouldBe("lesson-plan.pdf");
		evidence.Url.ShouldBe("https://example.test/lesson-plan");
		evidence.Notes.ShouldBe("Evidence seeded for API regression.");
	}

	private async Task<PerformanceTemplateDto> CreateTemplateAsync(string name)
	{
		var response = await Client.PostAsJsonAsync(BaseUrl + "/templates", BuildTemplateRequest(name), JsonOptions);
		var raw = await response.Content.ReadAsStringAsync();

		response.IsSuccessStatusCode.ShouldBeTrue(raw);
		return await response.Content.ReadFromJsonAsync<PerformanceTemplateDto>(JsonOptions)
		       ?? throw new InvalidOperationException("Failed to deserialize created performance template.");
	}

	private async Task<PerformanceTemplateDto> PublishTemplateAsync(Guid id)
	{
		var response = await Client.PostAsync($"{BaseUrl}/templates/{id}/publish", null);
		var raw = await response.Content.ReadAsStringAsync();

		response.IsSuccessStatusCode.ShouldBeTrue(raw);
		return await response.Content.ReadFromJsonAsync<PerformanceTemplateDto>(JsonOptions)
		       ?? throw new InvalidOperationException("Failed to deserialize published performance template.");
	}

	private static PerformanceTemplateRequest BuildTemplateRequest(string name) => new()
	{
		Name = name,
		GujaratiName = "સીબીએસઈ API મૂલ્યાંકન",
		BoardType = PerformanceBoardType.CBSE,
		StaffCategory = PerformanceStaffCategory.Teacher,
		RatingScaleMax = 5,
		AllowNotApplicable = true,
		Description = "API test template mapped to SQAAF.",
		Sections =
		[
			new PerformanceTemplateSectionRequest
			{
				Name = "Curriculum and assessment",
				GujaratiName = "પાઠયક્રમ અને મૂલ્યાંકન",
				SqaaDomain = PerformanceSqaaDomain.CurriculumPedagogyAssessment,
				Weight = 100,
				DisplayOrder = 1,
				Criteria =
				[
					new PerformanceTemplateCriterionRequest
					{
						Name = "Lesson plan evidence",
						GujaratiName = "પાઠ આયોજન પુરાવો",
						SqaaDomain = PerformanceSqaaDomain.CurriculumPedagogyAssessment,
						Weight = 100,
						EvidenceRequired = true,
						DisplayOrder = 1
					}
				]
			}
		]
	};

	private async Task<(Guid AssignmentId, Guid CriterionId)> SeedReviewEvidenceAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var templateRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceTemplate, Guid>>();
		var sectionRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceTemplateSection, Guid>>();
		var criterionRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceTemplateCriterion, Guid>>();
		var cycleRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
		var evidenceRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceEvidence, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var template = new PerformanceTemplate(
				Guid.NewGuid(),
				tenantId,
				$"CBSE API Evidence Template {Guid.NewGuid():N}",
				PerformanceStaffCategory.Teacher,
				null,
				boardType: PerformanceBoardType.CBSE);
			await templateRepository.InsertAsync(template, autoSave: false);

			var section = new PerformanceTemplateSection(
				Guid.NewGuid(),
				tenantId,
				template.Id,
				"Curriculum and assessment",
				100,
				1,
				null,
				PerformanceSqaaDomain.CurriculumPedagogyAssessment);
			await sectionRepository.InsertAsync(section, autoSave: false);

			var criterion = new PerformanceTemplateCriterion(
				Guid.NewGuid(),
				tenantId,
				template.Id,
				section.Id,
				"Lesson plan evidence",
				null,
				100,
				true,
				1,
				null,
				PerformanceSqaaDomain.CurriculumPedagogyAssessment);
			await criterionRepository.InsertAsync(criterion, autoSave: false);

			var cycle = new ProfessionalReviewCycle(
				Guid.NewGuid(),
				tenantId,
				"API Evidence Cycle",
				new DateTime(2026, 1, 1),
				new DateTime(2026, 12, 31),
				ProfessionalReviewCycleStatus.InProgress);
			await cycleRepository.InsertAsync(cycle, autoSave: false);

			var assignment = new PerformanceReviewAssignment(
				Guid.NewGuid(),
				tenantId,
				cycle.Id,
				template.Id,
				Guid.NewGuid());
			await assignmentRepository.InsertAsync(assignment, autoSave: false);

			await evidenceRepository.InsertAsync(
				new PerformanceEvidence(
					Guid.NewGuid(),
					tenantId,
					assignment.Id,
					criterion.Id,
					"Lesson plan evidence API",
					"lesson-plan.pdf",
					"https://example.test/lesson-plan",
					"Evidence seeded for API regression."),
				autoSave: false);

			await uow.CompleteAsync();
			return (assignment.Id, criterion.Id);
		}
	}
}
