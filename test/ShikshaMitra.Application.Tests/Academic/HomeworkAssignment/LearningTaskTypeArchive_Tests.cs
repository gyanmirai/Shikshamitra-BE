using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class LearningTaskTypeArchive_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

	private readonly IAssignmentTypeAppService _assignmentTypeAppService;
	private readonly IHomeworkTypeAppService _homeworkTypeAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<AssignmentEntity, Guid> _assignmentRepository;
	private readonly IRepository<HomeworkAssignmentEntity, Guid> _homeworkRepository;
	private readonly AssignmentTypeManager _assignmentTypeManager;
	private readonly HomeworkTypeManager _homeworkTypeManager;
	private readonly ICurrentTenant _currentTenant;

	public LearningTaskTypeArchive_Tests()
	{
		_assignmentTypeAppService = GetRequiredService<IAssignmentTypeAppService>();
		_homeworkTypeAppService = GetRequiredService<IHomeworkTypeAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_assignmentRepository = GetRequiredService<IRepository<AssignmentEntity, Guid>>();
		_homeworkRepository = GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
		_assignmentTypeManager = GetRequiredService<AssignmentTypeManager>();
		_homeworkTypeManager = GetRequiredService<HomeworkTypeManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Used_AssignmentType_Should_Archive_And_Be_Excluded_From_Default_List()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var mediumId = await GetAcademicMediumIdAsync();
				var unique = Guid.NewGuid().ToString("N");
				var assignmentType = await _assignmentTypeManager.CreateAsync(
					mediumId,
					$"Case Study {unique}"[..30],
					defaultSubmissionType: SubmissionType.OnlineText,
					defaultMarks: 10,
					evaluationRequired: true,
					defaultEvaluationMethod: EvaluationMethod.Marks);
				await _assignmentTypeManager.CreateAsync(
					mediumId,
					$"Project {unique}"[..30],
					defaultSubmissionType: SubmissionType.OnlineText,
					defaultMarks: 10,
					evaluationRequired: true,
					defaultEvaluationMethod: EvaluationMethod.Marks);

				await _assignmentRepository.InsertAsync(new AssignmentEntity(
					Guid.NewGuid(),
					standardId: Guid.NewGuid(),
					subjectId: Guid.NewGuid(),
					title: "Seeded assignment",
					description: "Usage seed",
					assignmentTypeId: assignmentType.Id,
					submissionType: SubmissionType.OnlineText,
					totalMarks: 10,
					tenantId: TestTenantId,
					educationMediumId: mediumId), autoSave: true);

				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_assignmentTypeAppService.DeleteAsync(assignmentType.Id, mediumId));
				exception.Code.ShouldBe(HomeworkAssignmentErrorCodes.AssignmentTypeIsInUse);

				await _assignmentTypeAppService.ArchiveAsync(assignmentType.Id, mediumId);

				var activeList = await _assignmentTypeAppService.GetAllAsync(mediumId);
				activeList.Data.Select(type => type.Id).ShouldNotContain(assignmentType.Id);

				var allList = await _assignmentTypeAppService.GetAllAsync(mediumId, includeArchived: true);
				allList.Data.Single(type => type.Id == assignmentType.Id).Status.ShouldBe(TaskTypeStatus.Archived);
			}
		});
	}

	[Fact]
	public async Task Used_HomeworkType_Should_Archive_And_Be_Excluded_From_Default_List()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var mediumId = await GetAcademicMediumIdAsync();
				var unique = Guid.NewGuid().ToString("N");
				var homeworkType = await _homeworkTypeManager.CreateAsync(
					mediumId,
					$"Worksheet {unique}"[..30],
					defaultSubmissionType: SubmissionType.OnlineText,
					defaultMarks: 10,
					evaluationRequired: true,
					defaultEvaluationMethod: EvaluationMethod.Marks);
				await _homeworkTypeManager.CreateAsync(
					mediumId,
					$"Reading {unique}"[..30],
					defaultSubmissionType: SubmissionType.OnlineText,
					defaultMarks: 10,
					evaluationRequired: true,
					defaultEvaluationMethod: EvaluationMethod.Marks);

				await _homeworkRepository.InsertAsync(new HomeworkAssignmentEntity(
					Guid.NewGuid(),
					standardId: Guid.NewGuid(),
					subjectId: Guid.NewGuid(),
					title: "Seeded homework",
					description: "Usage seed",
					homeworkTypeId: homeworkType.Id,
					submissionType: SubmissionType.OnlineText,
					tenantId: TestTenantId,
					educationMediumId: mediumId), autoSave: true);

				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_homeworkTypeAppService.DeleteAsync(homeworkType.Id, mediumId));
				exception.Code.ShouldBe(HomeworkAssignmentErrorCodes.HomeworkTypeIsInUse);

				await _homeworkTypeAppService.ArchiveAsync(homeworkType.Id, mediumId);

				var activeList = await _homeworkTypeAppService.GetAllAsync(mediumId);
				activeList.Data.Select(type => type.Id).ShouldNotContain(homeworkType.Id);

				var allList = await _homeworkTypeAppService.GetAllAsync(mediumId, includeArchived: true);
				allList.Data.Single(type => type.Id == homeworkType.Id).Status.ShouldBe(TaskTypeStatus.Archived);
			}
		});
	}

	private async Task<Guid> GetAcademicMediumIdAsync()
	{
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		return stage.EducationMediumId;
	}
}
