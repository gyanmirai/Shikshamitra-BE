using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
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

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveAssignmentAppService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IStudentElectiveAssignmentAppService _appService;
	private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<StudentElectiveSelection, Guid> _selectionRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly ICurrentTenant _currentTenant;

	public StudentElectiveAssignmentAppService_Tests()
	{
		_appService = GetRequiredService<IStudentElectiveAssignmentAppService>();
		_electiveGroupRepository = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_selectionRepository = GetRequiredService<IRepository<StudentElectiveSelection, Guid>>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_mediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_boardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_studentRepository = GetRequiredService<IRepository<Student, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task New_Admission_Should_Remain_Editable_Without_Unlocking_Existing_Students()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _boardRepository.GetAsync(medium.EducationBoardId);
				var standard = await _standardRepository.InsertAsync(new Standard(Guid.NewGuid(), TestTenantId, "Elective class", 8, stageId, null), autoSave: true);
				var division = await _divisionRepository.InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "E-A"), autoSave: true);
				var subject = await _subjectRepository.InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Music", "#00A76F", medium.Id), autoSave: true);
				var otherSubject = await _subjectRepository.InsertAsync(new Subject(Guid.NewGuid(), TestTenantId, "Art", "#00A76F", medium.Id), autoSave: true);
				var group = new ElectiveGroup(Guid.NewGuid(), TestTenantId, "Arts", "#00A76F", medium.Id);
				group.AddSubject(subject.Id);
				group.AddSubject(otherSubject.Id);
				await _electiveGroupRepository.InsertAsync(group, autoSave: true);
				async Task<Student> AdmitAsync(string name, int roll) => await _studentRepository.InsertAsync(new Student(
					Guid.NewGuid(), TestTenantId, name, null, "Student", board.AcademicYearId, board.Id,
					medium.Id, stageId, standard.Id, division.Id, rollNo: roll), autoSave: true);
				var existing = await AdmitAsync("Existing", 1);
				var selection = await _selectionRepository.InsertAsync(new StudentElectiveSelection(
					Guid.NewGuid(), TestTenantId, existing.Id, group.Id, subject.Id, medium.Id), autoSave: true);
				var lockInput = new LockStudentElectiveAssignmentsDto
				{
					AcademicMediumId = medium.Id, ClassId = standard.Id, DivisionId = division.Id, ElectiveGroupId = group.Id
				};
				await _appService.LockStudentElectiveAssignmentsAsync(lockInput);
				(await _appService.GetStudentElectiveAssignmentsAsync(medium.Id, standard.Id, division.Id, group.Id)).IsLocked.ShouldBeTrue();
				var admitted = await AdmitAsync("New", 2);
				var roster = await _appService.GetStudentElectiveAssignmentsAsync(medium.Id, standard.Id, division.Id, group.Id);
				roster.IsLocked.ShouldBeFalse();
				roster.Records.Single(row => row.StudentId == existing.Id).IsLocked.ShouldBeTrue();
				roster.Records.Single(row => row.StudentId == admitted.Id).IsLocked.ShouldBeFalse();
				roster.Records.Single(row => row.StudentId == admitted.Id).SelectedSubjectId.ShouldBeNull();
				var searched = await _appService.GetStudentElectiveAssignmentsAsync(medium.Id, standard.Id, division.Id, group.Id, "Existing");
				searched.IsLocked.ShouldBeFalse();
				await Should.ThrowAsync<Volo.Abp.UserFriendlyException>(() => _appService.LockStudentElectiveAssignmentsAsync(lockInput));
				await _appService.SaveStudentElectiveAssignmentsAsync(new SaveStudentElectiveAssignmentsDto
				{
					AcademicMediumId = medium.Id, ElectiveGroupId = group.Id,
					Assignments = new List<StudentElectiveUpdateDto> { new() { StudentId = admitted.Id, SelectedSubjectId = otherSubject.Id } }
				});
				(await _appService.GetStudentElectiveAssignmentsAsync(medium.Id, standard.Id, division.Id, group.Id)).IsLocked.ShouldBeFalse();
				await _appService.LockStudentElectiveAssignmentsAsync(lockInput);
				(await _appService.GetStudentElectiveAssignmentsAsync(medium.Id, standard.Id, division.Id, group.Id)).IsLocked.ShouldBeTrue();
				(await _selectionRepository.GetAsync(selection.Id)).SelectedSubjectId.ShouldBe(subject.Id);
				(await _selectionRepository.GetAsync(selection.Id)).IsLocked.ShouldBeTrue();
				foreach (var attemptedSubject in new Guid?[] { otherSubject.Id, null })
				{
					await Should.ThrowAsync<Volo.Abp.UserFriendlyException>(() => _appService.SaveStudentElectiveAssignmentsAsync(
						new SaveStudentElectiveAssignmentsDto
						{
							AcademicMediumId = medium.Id, ElectiveGroupId = group.Id,
							Assignments = new List<StudentElectiveUpdateDto> { new() { StudentId = existing.Id, SelectedSubjectId = attemptedSubject } }
						}));
				}
				(await _selectionRepository.GetAsync(selection.Id)).SelectedSubjectId.ShouldBe(subject.Id);
			}
		});
	}
	[Fact]
	public async Task Get_Assignments_Should_Return_Numeric_Roll_Order_With_Unassigned_Last()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _boardRepository.GetAsync(medium.EducationBoardId);
				var standard = await _standardRepository.InsertAsync(
					new Standard(Guid.NewGuid(), TestTenantId, "Class 8", 8, stageId, shiftId: null),
					autoSave: true);
				var division = await _divisionRepository.InsertAsync(
					new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "8-A"),
					autoSave: true);
				var group = await _electiveGroupRepository.InsertAsync(new ElectiveGroup(
					Guid.NewGuid(), TestTenantId, "Elven Grp One", "#7C3AED", medium.Id),
					autoSave: true);

				foreach (var rollNo in new int?[] { 2, null, 10, 1, 3 })
				{
					await _studentRepository.InsertAsync(new Student(
						Guid.NewGuid(), TestTenantId, "Student", null, rollNo?.ToString() ?? "Unassigned",
						board.AcademicYearId, board.Id, medium.Id, stageId, standard.Id, division.Id,
						rollNo: rollNo), autoSave: true);
				}

				var response = await _appService.GetStudentElectiveAssignmentsAsync(
					medium.Id, standard.Id, division.Id, group.Id);

				response.TotalRecords.ShouldBe(5);
				response.Records.Select(record => record.RollNo).ShouldBe(new[] { "1", "2", "3", "10", "N/A" });
			}
		});
	}

	[Fact]
	public async Task Should_Delete_An_Existing_Selection_When_The_Subject_Is_Null()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var academicMediumId = stage.EducationMediumId;
				var studentId = Guid.NewGuid();
				var subject = new Subject(
					Guid.NewGuid(),
					TestTenantId,
					"Computer Science",
					"#2563EB",
					academicMediumId);
				var electiveGroup = new ElectiveGroup(
					Guid.NewGuid(),
					TestTenantId,
					"Technology",
					"#7C3AED",
					academicMediumId);
				electiveGroup.AddSubject(subject.Id);
				var selection = new StudentElectiveSelection(
					Guid.NewGuid(),
					TestTenantId,
					studentId,
					electiveGroup.Id,
					subject.Id,
					academicMediumId);

				await _subjectRepository.InsertAsync(subject, autoSave: true);
				await _electiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);
				await _selectionRepository.InsertAsync(selection, autoSave: true);

				await _appService.SaveStudentElectiveAssignmentsAsync(
					new SaveStudentElectiveAssignmentsDto
					{
						AcademicMediumId = academicMediumId,
						ElectiveGroupId = electiveGroup.Id,
						Assignments = new List<StudentElectiveUpdateDto>
						{
							new() { StudentId = studentId, SelectedSubjectId = null }
						}
					});

				(await _selectionRepository.AnyAsync(item => item.Id == selection.Id)).ShouldBeFalse();
			}
		});
	}
}
