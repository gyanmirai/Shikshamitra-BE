using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
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

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamAppService_ScheduleStatus_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IExamAppService _examAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly ICurrentTenant _currentTenant;

	public ExamAppService_ScheduleStatus_Tests()
	{
		_examAppService = GetRequiredService<IExamAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task ChangeStatusAsync_Should_Reject_Completion_Before_Final_Subject_End_Time()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, _) = await CreateScheduledExamAsync(ExamStatus.Published);

				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_examAppService.ChangeStatusAsync(exam.Id, ExamStatus.Completed));

				exception.Code.ShouldBe("ShikshaMitra.Exam:CompletionBeforeScheduleEnd");
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Report_Future_Completed_Exam_As_Published()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Completed);

				var published = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Published,
					MaxResultCount = 10
				});

				var completed = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Completed,
					MaxResultCount = 10
				});

				published.Items.Select(item => item.ExamId).ShouldContain(exam.Id);
				published.Items.Single(item => item.ExamId == exam.Id).Status.ShouldBe(ExamStatus.Published);
				completed.Items.Select(item => item.ExamId).ShouldNotContain(exam.Id);
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Start_And_End_Date_For_Scheduled_Pending_Exam()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);

				var result = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Pending,
					MaxResultCount = 10
				});

				var item = result.Items.Single(item => item.ExamId == exam.Id);
				item.SubjectCount.ShouldBe(2);
				item.StartDate.ShouldBe(DateTime.Now.Date.AddDays(1));
				item.EndDate.ShouldBe(DateTime.Now.Date.AddDays(6).AddHours(15));
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Correct_Subject_Count_For_Assigned_Subjects()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);

				var result = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Pending,
					MaxResultCount = 10
				});

				var item = result.Items.Single(item => item.ExamId == exam.Id);
				item.SubjectCount.ShouldBe(exam.ExamSubjects.Count);
				item.SubjectCount.ShouldBe(2);
			}
		});
	}

	private async Task<(Exam exam, Guid mediumId)> CreateScheduledExamAsync(ExamStatus status)
	{
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var unique = Guid.NewGuid().ToString("N");

		var standard = await _standardManager.CreateAsync(
			stageId,
			$"Exam Std {unique}"[..30],
			7,
			shiftId: null);
		await _standardRepository.InsertAsync(standard, autoSave: true);

		var division = await _divisionManager.CreateAsync(
			standard.Id,
			$"A-{unique}"[..30],
			35);
		await _divisionRepository.InsertAsync(division, autoSave: true);

		var firstSubject = await CreateSubjectAsync(stage.EducationMediumId, $"Science {unique}"[..30]);
		var finalSubject = await CreateSubjectAsync(stage.EducationMediumId, $"Social Science {unique}"[..30]);

		var examType = await _examTypeManager.CreateAsync(
			$"Mid Term {unique}"[..30],
			description: null,
			stage.EducationMediumId);

		var exam = _examManager.Create(
			examType.Id,
			$"Janmashtami Vacation {unique}"[..40],
			standard.Id,
			new List<Guid> { division.Id },
			new List<Guid> { firstSubject.Id, finalSubject.Id });

		var firstExamDate = DateTime.Now.Date.AddDays(1);
		var finalExamDate = DateTime.Now.Date.AddDays(6);
		exam.ExamSubjects.Single(subject => subject.SubjectId == firstSubject.Id)
			.UpdateSchedule(
				firstExamDate,
				firstExamDate.AddHours(14),
				firstExamDate.AddHours(15),
				100,
				33,
				notes: null);
		exam.ExamSubjects.Single(subject => subject.SubjectId == finalSubject.Id)
			.UpdateSchedule(
				finalExamDate,
				finalExamDate.AddHours(14),
				finalExamDate.AddHours(15),
				100,
				33,
				notes: null);

		exam.ChangeStatus(status);
		await _examRepository.InsertAsync(exam, autoSave: true);

		return (exam, stage.EducationMediumId);
	}

	private async Task<Subject> CreateSubjectAsync(Guid mediumId, string name)
	{
		var subject = new Subject(
			Guid.NewGuid(),
			TestTenantId,
			name,
			"#00A76F",
			mediumId);
		await _subjectRepository.InsertAsync(subject, autoSave: true);

		return subject;
	}
}
