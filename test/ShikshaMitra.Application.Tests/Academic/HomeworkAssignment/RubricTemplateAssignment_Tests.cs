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
	public async Task GetStudentAssignmentEvaluation_Should_Return_Null_When_No_Submission_Exists()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (currentTenant.Change(TestTenantId))
			{
				var evaluation = await rubricService.GetStudentAssignmentEvaluationAsync(
					Guid.NewGuid(),
					Guid.NewGuid());

				evaluation.ShouldBeNull();
			}
		});
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

	[Fact]
	public async Task Performance_Template_Should_Create_Update_And_Read_Without_Criterion_Maximum()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (currentTenant.Change(TestTenantId))
			{
				var input = new CreateUpdateRubricDto
				{
					IsTemplate = true, AppliesToWorkType = WorkType.Both,
					Title = "Shared performance rubric", ScoringModel = RubricScoringModel.PerformanceLevels,
					PerformanceLevels = [new() { Level = 1, Label = "Good", Score = 2 }, new() { Level = 2, Label = "Excellent", Score = 4 }],
					Criteria = [new() { Title = "Accuracy", IsRequired = true }, new() { Title = "Presentation", IsRequired = false }]
				};
				var created = await rubricService.CreateAsync(input);
				created.TotalMarks.ShouldBe(8);
				created.Criteria.ShouldAllBe(c => c.Points == null && c.Weight == null);
				var json = System.Text.Json.JsonSerializer.Serialize(created);
				json.ShouldNotContain("MaxMarks");
				input.PerformanceLevels[1].Score = 5;
				var updated = await rubricService.UpdateAsync(created.Id, input);
				updated.TotalMarks.ShouldBe(10);
				var read = await rubricService.GetAsync(created.Id);
				read.TotalMarks.ShouldBe(10);
				read.Criteria.Count.ShouldBe(2);
				read.Criteria.Count(c => c.IsRequired).ShouldBe(1);
				var listed = await rubricService.GetListAsync(new() { IsTemplate = true });
				listed.Single(r => r.Id == created.Id).TotalMarks.ShouldBe(10);
				await rubricService.PublishAsync(created.Id);
				(await rubricService.GetAsync(created.Id)).Status.ShouldBe(RubricStatus.Published);
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
