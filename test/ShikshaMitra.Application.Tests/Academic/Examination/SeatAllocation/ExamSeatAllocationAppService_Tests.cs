using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public class ExamSeatAllocationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly IExamSeatAllocationAppService _seatAllocationAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<ExamSeatingPlan, Guid> _seatingPlanRepository;
	private readonly IRepository<ExamSeatAllocation, Guid> _seatAllocationRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly ICurrentTenant _currentTenant;

	public ExamSeatAllocationAppService_Tests()
	{
		_seatAllocationAppService = GetRequiredService<IExamSeatAllocationAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_educationMediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_educationBoardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_studentRepository = GetRequiredService<IRepository<Student, Guid>>();
		_seatingPlanRepository = GetRequiredService<IRepository<ExamSeatingPlan, Guid>>();
		_seatAllocationRepository = GetRequiredService<IRepository<ExamSeatAllocation, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetDivisionsForExamAsync_Should_Default_To_Exam_Standard_When_StandardId_Is_Not_Provided()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, division) = await CreateScheduledExamAsync();

				var result = await _seatAllocationAppService.GetDivisionsForExamAsync(
					exam.Id,
					new GetExamDivisionsInput
					{
						StandardId = Guid.Empty
					});

				result.TotalCount.ShouldBe(1);
				result.Items.Select(item => item.Id).ShouldContain(division.Id);
				result.Items.Single().Name.ShouldBe(division.Name);
			}
		});
	}

	[Fact]
	public async Task GetDivisionsForExamAsync_Should_Return_Divisions_Alphabetically()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var unique = Guid.NewGuid().ToString("N");

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Seat Std {unique}"[..30],
					11,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var divisionB = await _divisionManager.CreateAsync(standard.Id, $"B-{unique}"[..30], 30);
				var divisionA = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 30);
				await _divisionRepository.InsertManyAsync([divisionB, divisionA], autoSave: true);

				var subject = new Subject(
					Guid.NewGuid(),
					TestTenantId,
					$"Math {unique}"[..30],
					"#00A76F",
					stage.EducationMediumId);
				await _subjectRepository.InsertAsync(subject, autoSave: true);

				var examType = await _examTypeManager.CreateAsync(
					$"Seat Exam {unique}"[..30],
					description: null,
					stage.EducationMediumId);

				var exam = _examManager.Create(
					examType.Id,
					$"Seating Plan Exam {unique}"[..40],
					standard.Id,
					new List<Guid> { divisionB.Id, divisionA.Id },
					new List<Guid> { subject.Id });
				exam.ChangeStatus(ExamStatus.Published);
				await _examRepository.InsertAsync(exam, autoSave: true);

				var result = await _seatAllocationAppService.GetDivisionsForExamAsync(
					exam.Id,
					new GetExamDivisionsInput
					{
						StandardId = Guid.Empty
					});

				result.Items.Select(item => item.Name).ToList().ShouldBe([divisionA.Name, divisionB.Name]);
			}
		});
	}

	[Fact]
	public async Task GetSeatAllocationTableAsync_Should_Return_Division_Wise_Progress()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
				var unique = Guid.NewGuid().ToString("N");

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Seat Std {unique}"[..30],
					11,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var divisionA = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 30);
				var divisionB = await _divisionManager.CreateAsync(standard.Id, $"B-{unique}"[..30], 30);
				await _divisionRepository.InsertManyAsync([divisionB, divisionA], autoSave: true);

				var subject = new Subject(
					Guid.NewGuid(),
					TestTenantId,
					$"Math {unique}"[..30],
					"#00A76F",
					stage.EducationMediumId);
				await _subjectRepository.InsertAsync(subject, autoSave: true);

				var examType = await _examTypeManager.CreateAsync(
					$"Seat Exam {unique}"[..30],
					description: null,
					stage.EducationMediumId);

				var exam = _examManager.Create(
					examType.Id,
					$"Seating Plan Exam {unique}"[..40],
					standard.Id,
					new List<Guid> { divisionB.Id, divisionA.Id },
					new List<Guid> { subject.Id });
				exam.ChangeStatus(ExamStatus.Published);
				await _examRepository.InsertAsync(exam, autoSave: true);

				var students = Enumerable.Range(1, 3)
					.Select(index => CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionA.Id, index, unique))
					.Concat(Enumerable.Range(4, 3)
						.Select(index => CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionB.Id, index, unique)))
					.ToList();
				await _studentRepository.InsertManyAsync(students, autoSave: true);

				var plan = new ExamSeatingPlan(Guid.NewGuid(), TestTenantId, exam.Id, "All Subjects", SeatingPlanScopeType.AllSubjects);
				await _seatingPlanRepository.InsertAsync(plan, autoSave: true);
				var roomId = Guid.NewGuid();

				var allocations = students
					.Where(student => student.DivisionId == divisionA.Id || student.RollNo == 4)
					.Select((student, index) => new ExamSeatAllocation(
						Guid.NewGuid(),
						TestTenantId,
						plan.Id,
						exam.Id,
						student.Id,
						roomId,
						index + 1,
						benchId: null,
						isRoomAssign: true))
					.ToList();
				await _seatAllocationRepository.InsertManyAsync(allocations, autoSave: true);

				var result = await _seatAllocationAppService.GetSeatAllocationTableAsync(
					new GetSeatAllocationTableInput { StandardId = standard.Id });

				var row = result.Items.Single(item => item.ExamId == exam.Id);
				row.TotalStudents.ShouldBe(6);
				row.AllocatedStudents.ShouldBe(4);
				row.DivisionProgress.Count.ShouldBe(2);
				row.Divisions.ShouldBe([divisionA.Name, divisionB.Name]);
				row.DivisionProgress.Select(item => item.DivisionName).ToList().ShouldBe([divisionA.Name, divisionB.Name]);
				row.DivisionProgress.Single(item => item.DivisionId == divisionA.Id).AllocatedStudents.ShouldBe(3);
				row.DivisionProgress.Single(item => item.DivisionId == divisionA.Id).Progress.ShouldBe(100);
				row.DivisionProgress.Single(item => item.DivisionId == divisionB.Id).AllocatedStudents.ShouldBe(1);
				row.DivisionProgress.Single(item => item.DivisionId == divisionB.Id).Progress.ShouldBe(33);
			}
		});
	}

	private async Task<(Exam exam, Division division)> CreateScheduledExamAsync()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
			var stage = await _stageRepository.GetAsync(stageId);
			var unique = Guid.NewGuid().ToString("N");

			var standard = await _standardManager.CreateAsync(
				stageId,
				$"Seat Std {unique}"[..30],
				6,
				shiftId: null);
			await _standardRepository.InsertAsync(standard, autoSave: true);

			var division = await _divisionManager.CreateAsync(
				standard.Id,
				$"A-{unique}"[..30],
				30);
			await _divisionRepository.InsertAsync(division, autoSave: true);

			var subject = new Subject(
				Guid.NewGuid(),
				TestTenantId,
				$"Math {unique}"[..30],
				"#00A76F",
				stage.EducationMediumId);
			await _subjectRepository.InsertAsync(subject, autoSave: true);

			var examType = await _examTypeManager.CreateAsync(
				$"Seat Exam {unique}"[..30],
				description: null,
				stage.EducationMediumId);

			var exam = _examManager.Create(
				examType.Id,
				$"Seating Plan Exam {unique}"[..40],
				standard.Id,
				new List<Guid> { division.Id },
				new List<Guid> { subject.Id });
			exam.ChangeStatus(ExamStatus.Published);
			await _examRepository.InsertAsync(exam, autoSave: true);

			return (exam, division);
		}
	}

	private static Student CreateStudent(
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid educationMediumStageId,
		Guid standardId,
		Guid divisionId,
		int rollNo,
		string unique)
	{
		return new Student(
			Guid.NewGuid(),
			TestTenantId,
			$"Seat{rollNo}",
			null,
			$"Student{unique}"[..30],
			academicYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			grNo: rollNo,
			rollNo: rollNo,
			status: StudentLifeCycleStatusEnum.Enrolled);
	}
}
