using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementTemplateWorkflow_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ProfessionalManagementTemplateWorkflow _workflow;
	private readonly ProfessionalManagementDashboardReader _dashboardReader;
	private readonly IRepository<PerformanceTemplate, Guid> _templateRepository;
	private readonly IRepository<PerformanceTemplateSection, Guid> _sectionRepository;
	private readonly IRepository<PerformanceTemplateCriterion, Guid> _criterionRepository;
	private readonly IRepository<ProfessionalReviewCycle, Guid> _cycleRepository;
	private readonly IRepository<PerformanceReviewAssignment, Guid> _assignmentRepository;
	private readonly IRepository<PerformanceEvidence, Guid> _evidenceRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public ProfessionalManagementTemplateWorkflow_Tests()
	{
		_workflow = GetRequiredService<ProfessionalManagementTemplateWorkflow>();
		_dashboardReader = GetRequiredService<ProfessionalManagementDashboardReader>();
		_templateRepository = GetRequiredService<IRepository<PerformanceTemplate, Guid>>();
		_sectionRepository = GetRequiredService<IRepository<PerformanceTemplateSection, Guid>>();
		_criterionRepository = GetRequiredService<IRepository<PerformanceTemplateCriterion, Guid>>();
		_cycleRepository = GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>();
		_assignmentRepository = GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
		_evidenceRepository = GetRequiredService<IRepository<PerformanceEvidence, Guid>>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public void GetDefaultTemplates_Should_Provide_Board_Specific_Sqaa_Mapped_Presets()
	{
		var defaults = _workflow.GetDefaultTemplates().Items;

		defaults.Any(x => x.BoardType == PerformanceBoardType.CBSE && x.Name.Contains("CBSE")).ShouldBeTrue();
		defaults.Any(x => x.BoardType == PerformanceBoardType.GSEB && x.Name.Contains("GSEB")).ShouldBeTrue();
		defaults.Any(x => !string.IsNullOrWhiteSpace(x.GujaratiName)).ShouldBeTrue();
		defaults.All(x => x.Sections.Sum(section => section.Weight) == 100m).ShouldBeTrue();
		defaults.SelectMany(x => x.Sections).All(section => section.Criteria.Sum(criterion => criterion.Weight) == 100m).ShouldBeTrue();
		defaults
			.SelectMany(x => x.Sections)
			.SelectMany(section => section.Criteria.Select(criterion => criterion.SqaaDomain))
			.Distinct()
			.Count()
			.ShouldBeGreaterThan(2);
		defaults
			.SelectMany(x => x.Sections)
			.SelectMany(x => x.Criteria)
			.Any(x => x.EvidenceRequired)
			.ShouldBeTrue();
	}

	[Fact]
	public async Task GetSqaaEvidenceAsync_Should_Group_Coverage_By_Domain()
	{
		var tenantId = _guidGenerator.Create();
		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var template = new PerformanceTemplate(
					_guidGenerator.Create(),
					tenantId,
					"CBSE Evidence Template",
					PerformanceStaffCategory.Teacher,
					null,
					boardType: PerformanceBoardType.CBSE);
				await _templateRepository.InsertAsync(template, autoSave: true);

				var section = new PerformanceTemplateSection(
					_guidGenerator.Create(),
					tenantId,
					template.Id,
					"Assessment",
					100,
					1,
					sqaaDomain: PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				await _sectionRepository.InsertAsync(section, autoSave: true);

				var criterion = new PerformanceTemplateCriterion(
					_guidGenerator.Create(),
					tenantId,
					template.Id,
					section.Id,
					"Assessment evidence",
					null,
					100,
					true,
					1,
					sqaaDomain: PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				await _criterionRepository.InsertAsync(criterion, autoSave: true);

				var genericTemplate = new PerformanceTemplate(
					_guidGenerator.Create(),
					tenantId,
					"Shared Indian Evidence Template",
					PerformanceStaffCategory.Teacher,
					null,
					boardType: PerformanceBoardType.GenericIndian);
				await _templateRepository.InsertAsync(genericTemplate, autoSave: true);

				var genericSection = new PerformanceTemplateSection(
					_guidGenerator.Create(),
					tenantId,
					genericTemplate.Id,
					"Shared assessment",
					100,
					1,
					sqaaDomain: PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				await _sectionRepository.InsertAsync(genericSection, autoSave: true);

				await _criterionRepository.InsertAsync(
					new PerformanceTemplateCriterion(
						_guidGenerator.Create(),
						tenantId,
						genericTemplate.Id,
						genericSection.Id,
						"Shared non-required evidence",
						null,
						100,
						false,
						1,
						sqaaDomain: PerformanceSqaaDomain.CurriculumPedagogyAssessment),
					autoSave: true);

				var cycle = new ProfessionalReviewCycle(
					_guidGenerator.Create(),
					tenantId,
					"CBSE Cycle",
					new DateTime(2026, 1, 1),
					new DateTime(2026, 12, 31),
					ProfessionalReviewCycleStatus.InProgress);
				await _cycleRepository.InsertAsync(cycle, autoSave: true);

				var assignment = new PerformanceReviewAssignment(
					_guidGenerator.Create(),
					tenantId,
					cycle.Id,
					template.Id,
					_guidGenerator.Create());
				await _assignmentRepository.InsertAsync(assignment, autoSave: true);

				var assignmentWithoutEvidence = new PerformanceReviewAssignment(
					_guidGenerator.Create(),
					tenantId,
					cycle.Id,
					template.Id,
					_guidGenerator.Create());
				await _assignmentRepository.InsertAsync(assignmentWithoutEvidence, autoSave: true);

				await _evidenceRepository.InsertAsync(
					new PerformanceEvidence(
						_guidGenerator.Create(),
						tenantId,
						assignment.Id,
						criterion.Id,
						"Assessment register",
						null,
						null,
						null),
					autoSave: true);

				var rows = await _dashboardReader.GetSqaaEvidenceAsync(
					cycle.Id,
					PerformanceBoardType.CBSE,
					PerformanceStaffCategory.Teacher,
					PerformanceSqaaDomain.CurriculumPedagogyAssessment);

				var row = rows.Single();
				row.Domain.ShouldBe(PerformanceSqaaDomain.CurriculumPedagogyAssessment);
				row.TemplateCount.ShouldBe(2);
				row.AssignedReviewCount.ShouldBe(2);
				row.MappedCriteria.ShouldBe(2);
				row.EvidenceRequiredCriteria.ShouldBe(1);
				row.EvidenceSubmitted.ShouldBe(1);
				row.MissingEvidence.ShouldBe(1);
			});
		}
	}
}
