using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class RubricTemplateAssignment_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");

	private readonly IRubricAssessmentAppService rubricService;
	private readonly IRepository<AssessmentRubric, Guid> rubricRepository;
	private readonly IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository;
	private readonly IRepository<EducationMediumStage, Guid> stageRepository;
	private readonly HomeworkTypeManager homeworkTypeManager;
	private readonly ICurrentTenant currentTenant;

	public RubricTemplateAssignment_Tests()
	{
		rubricService = GetRequiredService<IRubricAssessmentAppService>();
		rubricRepository = GetRequiredService<IRepository<AssessmentRubric, Guid>>();
		homeworkRepository = GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
		stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		homeworkTypeManager = GetRequiredService<HomeworkTypeManager>();
		currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task CopyToTask_Should_Be_Idempotent_And_Replace_The_Previous_Template()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var mediumId = (await stageRepository.GetAsync(stageId)).EducationMediumId;
				var homeworkType = await homeworkTypeManager.CreateAsync(
					mediumId,
					$"Rubric homework {Guid.NewGuid():N}"[..30],
					defaultSubmissionType: SubmissionType.OnlineText);
				var standardId = Guid.NewGuid();
				var subjectId = Guid.NewGuid();
				var homework = await homeworkRepository.InsertAsync(
					new HomeworkAssignmentEntity(
						Guid.NewGuid(),
						standardId,
						subjectId,
						"Template homework",
						"Homework with a selected rubric template.",
						homeworkType.Id,
						SubmissionType.OnlineText,
						tenantId: TestTenantId,
						educationMediumId: mediumId),
					autoSave: true);
				var firstTemplate = await InsertTemplateAsync("Rubric One", mediumId);
				var secondTemplate = await InsertTemplateAsync("Rubric Two", mediumId);
				var input = new CreateRubricFromTemplateDto
				{
					StandardId = standardId,
					SubjectId = subjectId,
					HomeworkId = homework.Id,
					Title = "Template homework rubric"
				};

				var firstCopy = await rubricService.CreateFromTemplateAsync(firstTemplate.Id, input);
				var repeatedCopy = await rubricService.CreateFromTemplateAsync(firstTemplate.Id, input);

				repeatedCopy.Id.ShouldBe(firstCopy.Id);

				var replacement = await rubricService.CreateFromTemplateAsync(secondTemplate.Id, input);
				var taskRubrics = await rubricRepository.GetListAsync(rubric =>
					rubric.HomeworkId == homework.Id && !rubric.IsTemplate);

				taskRubrics.Count.ShouldBe(2);
				taskRubrics.Single(rubric => rubric.Id == firstCopy.Id).Status.ShouldBe(RubricStatus.Archived);
				var activeRubric = taskRubrics.Single(rubric => rubric.Status == RubricStatus.Published);
				activeRubric.Id.ShouldBe(replacement.Id);
				activeRubric.SourceTemplateId.ShouldBe(secondTemplate.Id);
			}
		});
	}

	private async Task<AssessmentRubric> InsertTemplateAsync(string title, Guid mediumId)
	{
		var rubric = new AssessmentRubric(
			Guid.NewGuid(),
			TestTenantId,
			null,
			null,
			mediumId,
			null,
			null,
			null,
			null,
			null,
			true,
			WorkType.Homework,
			RubricScoringModel.PointsBased,
			RubricVisibility.Institution,
			title,
			null,
			null,
			null,
			null,
			null);
		rubric.ReplaceCriteria([
			new AssessmentRubricCriterion(
				Guid.NewGuid(),
				TestTenantId,
				rubric.Id,
				"Accuracy",
				null,
				null,
				null,
				true,
				10,
				1)
		]);
		rubric.Publish();
		return await rubricRepository.InsertAsync(rubric, autoSave: true);
	}
}
