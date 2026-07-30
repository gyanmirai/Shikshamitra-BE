using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExaminationSettingsAppService_Archive_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

	private readonly IExaminationSettingsAppService _settingsAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<GradingScale, Guid> _gradingScaleRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly ICurrentTenant _currentTenant;

	public ExaminationSettingsAppService_Archive_Tests()
	{
		_settingsAppService = GetRequiredService<IExaminationSettingsAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_gradingScaleRepository = GetRequiredService<IRepository<GradingScale, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Used_ExamType_Should_Be_Archived_Instead_Of_Deleted_And_Excluded_From_Default_List()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (examType, mediumId, _) = await CreateExamWithSettingsAsync();

				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_settingsAppService.DeleteExamTypeAsync(examType.Id, mediumId));
				exception.Code.ShouldBe(ExamErrorCodes.ExamTypeIsInUse);

				var archived = await _settingsAppService.ArchiveExamTypeAsync(examType.Id, mediumId);
				archived.Item.ShouldNotBeNull();
				archived.Item.Status.ShouldBe(SettingStatus.Archived);

				var activeList = await _settingsAppService.GetExamTypesAsync(new GetExamTypesInput
				{
					AcademicMediumId = mediumId,
					MaxResultCount = 20
				});
				activeList.Items.Select(item => item.Id).ShouldNotContain(examType.Id);

				var allList = await _settingsAppService.GetExamTypesAsync(new GetExamTypesInput
				{
					AcademicMediumId = mediumId,
					IncludeArchived = true,
					MaxResultCount = 20
				});
				allList.Items.Single(item => item.Id == examType.Id).Status.ShouldBe(SettingStatus.Archived);
			}
		});
	}

	[Fact]
	public async Task Used_GradingScale_Should_Be_Archived_Instead_Of_Deleted_And_Excluded_From_Default_List()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (_, mediumId, gradingScale) = await CreateExamWithSettingsAsync();

				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_settingsAppService.DeleteGradingScaleAsync(gradingScale.Id, mediumId));
				exception.Code.ShouldBe(ExamErrorCodes.GradingScaleIsInUse);

				var archived = await _settingsAppService.ArchiveGradingScaleAsync(gradingScale.Id, mediumId);
				archived.Data.Status.ShouldBe(SettingStatus.Archived);
				archived.Data.IsDefault.ShouldBeFalse();

				var activeList = await _settingsAppService.GetAllGradingScalesAsync(mediumId);
				activeList.Data.Select(item => item.Id).ShouldNotContain(gradingScale.Id);

				var allList = await _settingsAppService.GetAllGradingScalesAsync(mediumId, includeArchived: true);
				allList.Data.Single(item => item.Id == gradingScale.Id).Status.ShouldBe(SettingStatus.Archived);
			}
		});
	}

	private async Task<(ExamType examType, Guid mediumId, GradingScale gradingScale)> CreateExamWithSettingsAsync()
	{
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var unique = Guid.NewGuid().ToString("N");

		var standard = await _standardManager.CreateAsync(
			stageId,
			$"Settings Std {unique}"[..30],
			7,
			shiftId: null);
		await _standardRepository.InsertAsync(standard, autoSave: true);

		var division = await _divisionManager.CreateAsync(
			standard.Id,
			$"A-{unique}"[..30],
			35);
		await _divisionRepository.InsertAsync(division, autoSave: true);

		var subject = new Subject(
			Guid.NewGuid(),
			TestTenantId,
			$"Science {unique}"[..30],
			"#00A76F",
			stage.EducationMediumId);
		await _subjectRepository.InsertAsync(subject, autoSave: true);

		var examType = await _examTypeManager.CreateAsync(
			$"Archive Type {unique}"[..30],
			description: null,
			stage.EducationMediumId);

		var gradingScale = new GradingScale(
			Guid.NewGuid(),
			TestTenantId,
			$"Archive Scale {unique}"[..30],
			"100",
			stage.EducationMediumId,
			33,
			description: null,
			isDefault: true,
			SettingStatus.Active);
		gradingScale.AddGradeRange(Guid.NewGuid(), 0, 32, "F", 0, null, GradeResultStatus.Fail);
		gradingScale.AddGradeRange(Guid.NewGuid(), 33, 100, "A", 4, null, GradeResultStatus.Pass);
		await _gradingScaleRepository.InsertAsync(gradingScale, autoSave: true);

		var exam = _examManager.Create(
			examType.Id,
			$"Archive Exam {unique}"[..40],
			standard.Id,
			new List<Guid> { division.Id },
			new List<Guid> { subject.Id },
			gradingScale.Id);
		await _examRepository.InsertAsync(exam, autoSave: true);

		return (examType, stage.EducationMediumId, gradingScale);
	}
}
