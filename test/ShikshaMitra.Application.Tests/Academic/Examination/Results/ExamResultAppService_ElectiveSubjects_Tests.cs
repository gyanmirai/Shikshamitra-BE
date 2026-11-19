using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
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

namespace ShikshaMitra.Academic.Examination.Results;

public partial class ExamResultAppService_ElectiveSubjects_Tests
    : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FakeAdminUserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

    private readonly IExamResultAppService _resultAppService;
    private readonly AcademicReportGenerator _reportGenerator;
    private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
    private readonly IRepository<EducationMedium, Guid> _mediumRepository;
    private readonly IRepository<EducationBoard, Guid> _boardRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly IRepository<Subject, Guid> _subjectRepository;
    private readonly IRepository<AssignedSubject, Guid> _assignedSubjectRepository;
    private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
    private readonly IRepository<StudentElectiveSelection, Guid> _selectionRepository;
    private readonly IRepository<Student, Guid> _studentRepository;
    private readonly IRepository<Exam, Guid> _examRepository;
    private readonly IRepository<ExamMark, Guid> _markRepository;
    private readonly IRepository<ExamMarksWorkflowState, Guid> _workflowStateRepository;
    private readonly IRepository<ExamResultDetail, Guid> _resultDetailRepository;
    private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
    private readonly IUserScopeService _userScopeService;
    private readonly StandardManager _standardManager;
    private readonly DivisionManager _divisionManager;
    private readonly ExamTypeManager _examTypeManager;
    private readonly ExamManager _examManager;
    private readonly ICurrentTenant _currentTenant;

    public ExamResultAppService_ElectiveSubjects_Tests()
    {
        _resultAppService = GetRequiredService<IExamResultAppService>();
        _reportGenerator = GetRequiredService<AcademicReportGenerator>();
        _stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
        _mediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
        _boardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
        _standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
        _divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
        _subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
        _assignedSubjectRepository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
        _electiveGroupRepository = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
        _selectionRepository = GetRequiredService<IRepository<StudentElectiveSelection, Guid>>();
        _studentRepository = GetRequiredService<IRepository<Student, Guid>>();
        _examRepository = GetRequiredService<IRepository<Exam, Guid>>();
        _markRepository = GetRequiredService<IRepository<ExamMark, Guid>>();
        _workflowStateRepository = GetRequiredService<IRepository<ExamMarksWorkflowState, Guid>>();
        _resultDetailRepository = GetRequiredService<IRepository<ExamResultDetail, Guid>>();
        _scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        _userScopeService = GetRequiredService<IUserScopeService>();
        _standardManager = GetRequiredService<StandardManager>();
        _divisionManager = GetRequiredService<DivisionManager>();
        _examTypeManager = GetRequiredService<ExamTypeManager>();
        _examManager = GetRequiredService<ExamManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GenerateAsync_Should_Calculate_Each_Student_From_Their_Selected_Elective_Only()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var fixture = await CreateElectiveExamAsync(
                    englishMarks: 74,
                    computerScienceMarks: 88,
                    computerScienceAbsent: false);

                var readiness = await _resultAppService.GetReadinessAsync(fixture.Exam.Id, fixture.Division.Id);

                readiness.IsReady.ShouldBeTrue();
                readiness.RequiredSubjects.ShouldBe(2);
                readiness.CompletedSubjects.ShouldBe(2);
                readiness.MissingMarks.ShouldBe(0);

                var generated = await _resultAppService.GenerateAsync(new GenerateResultRequestDto
                {
                    ExamId = fixture.Exam.Id,
                    DivisionId = fixture.Division.Id
                });
                var details = await _resultDetailRepository.GetListAsync(detail =>
                    detail.ResultHeaderId == generated.Id);

                var computerScienceResult = details.Single(detail =>
                    detail.StudentId == fixture.ComputerScienceStudent.Id);
                computerScienceResult.TotalMaxMarks.ShouldBe(100);
                computerScienceResult.TotalObtainedMarks.ShouldBe(88);
                computerScienceResult.Percentage.ShouldBe(88);
                computerScienceResult.Grade.ShouldBe("A");
                computerScienceResult.ResultStatus.ShouldBe(ExamStudentResultStatus.Pass);

                var englishResult = details.Single(detail => detail.StudentId == fixture.EnglishStudent.Id);
                englishResult.TotalMaxMarks.ShouldBe(100);
                englishResult.TotalObtainedMarks.ShouldBe(74);
                englishResult.Percentage.ShouldBe(74);
                englishResult.ResultStatus.ShouldBe(ExamStudentResultStatus.Pass);

                var studentView = await _resultAppService.GetStudentResultDetailsAsync(
                    fixture.Exam.Id,
                    fixture.ComputerScienceStudent.Id);
                studentView.TotalMarks.ShouldBe("100.00");
                studentView.ObtainedMarks.ShouldBe("88.00");
                studentView.Percentage.ShouldBe("88%");
                studentView.Grade.ShouldBe("A");
                studentView.Status.ShouldBe("PASS");
                studentView.Subjects.Count.ShouldBe(1);
                studentView.Subjects.Single().SubjectName.ShouldBe(fixture.ComputerScience.Name);
                studentView.Subjects.ShouldNotContain(subject => subject.SubjectName == fixture.English.Name);

                var report = await _reportGenerator.GenerateAsync(
                    new AcademicReportRequestDto
                    {
                        ReportType = AcademicReportTypes.ResultWithAllSubjectList,
                        Filters = new AcademicReportFilterDto
                        {
                            ExamId = fixture.Exam.Id,
                            DivisionIds = [fixture.Division.Id]
                        }
                    },
                    FakeAdminUserId,
                    TestTenantId,
                    exportAllRows: false);
                var computerScienceRow = report.Rows.Single(row =>
                    row["StudentId"] == fixture.ComputerScienceStudent.Id.ToString());
                computerScienceRow["Total"].ShouldBe("88.00");
                computerScienceRow["TotalMaxMarks"].ShouldBe("100.00");
                computerScienceRow["Percentage"].ShouldBe("88%");
                computerScienceRow["Grade"].ShouldBe("A");
                computerScienceRow["Status"].ShouldBe("Passed");
                computerScienceRow[$"subj_{fixture.English.Id}"].ShouldBe("—");
                computerScienceRow[$"subj_{fixture.English.Id}_Remarks"].ShouldBe("Not applicable");
                computerScienceRow[$"subj_{fixture.ComputerScience.Id}"].ShouldBe("88.00");
            }
        });
    }

    [Fact]
    public async Task GenerateAsync_Should_Still_Count_An_Absence_For_The_Selected_Elective()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var fixture = await CreateElectiveExamAsync(
                    englishMarks: 74,
                    computerScienceMarks: null,
                    computerScienceAbsent: true);

                var generated = await _resultAppService.GenerateAsync(new GenerateResultRequestDto
                {
                    ExamId = fixture.Exam.Id,
                    DivisionId = fixture.Division.Id
                });
                var result = await _resultDetailRepository.SingleAsync(detail =>
                    detail.ResultHeaderId == generated.Id &&
                    detail.StudentId == fixture.ComputerScienceStudent.Id);

                result.TotalMaxMarks.ShouldBe(100);
                result.TotalObtainedMarks.ShouldBe(0);
                result.ResultStatus.ShouldBe(ExamStudentResultStatus.Absent);

                var studentView = await _resultAppService.GetStudentResultDetailsAsync(
                    fixture.Exam.Id,
                    fixture.ComputerScienceStudent.Id);
                studentView.Subjects.Count.ShouldBe(1);
                studentView.Subjects.Single().SubjectName.ShouldBe(fixture.ComputerScience.Name);
                studentView.Subjects.Single().MarksObtained.ShouldBe("Ab");
                studentView.Subjects.ShouldNotContain(subject => subject.SubjectName == fixture.English.Name);
            }
        });
    }

    [Fact]
    public async Task GenerateAsync_Should_Rank_Students_By_Percentage_When_Elective_Maximums_Differ()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var fixture = await CreateElectiveExamAsync(
                    englishMarks: 40,
                    computerScienceMarks: 70,
                    computerScienceAbsent: false,
                    englishTotalMarks: 50,
                    computerScienceTotalMarks: 100);

                var generated = await _resultAppService.GenerateAsync(new GenerateResultRequestDto
                {
                    ExamId = fixture.Exam.Id,
                    DivisionId = fixture.Division.Id
                });
                var details = await _resultDetailRepository.GetListAsync(detail =>
                    detail.ResultHeaderId == generated.Id);

                var englishResult = details.Single(detail => detail.StudentId == fixture.EnglishStudent.Id);
                englishResult.TotalMaxMarks.ShouldBe(50);
                englishResult.Percentage.ShouldBe(80);
                englishResult.ClassRank.ShouldBe(1);

                var computerScienceResult = details.Single(detail =>
                    detail.StudentId == fixture.ComputerScienceStudent.Id);
                computerScienceResult.TotalMaxMarks.ShouldBe(100);
                computerScienceResult.Percentage.ShouldBe(70);
                computerScienceResult.ClassRank.ShouldBe(2);
            }
        });
    }

    private async Task<ElectiveExamFixture> CreateElectiveExamAsync(
        double englishMarks,
        double? computerScienceMarks,
        bool computerScienceAbsent,
        double englishTotalMarks = 100,
        double computerScienceTotalMarks = 100)
    {
        await EnsureReadQueryActorAsync();
        var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
        var stage = await _stageRepository.GetAsync(stageId);
        var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
        var board = await _boardRepository.GetAsync(medium.EducationBoardId);
        var unique = Guid.NewGuid().ToString("N");

        await _scopeRepository.InsertAsync(
            new UserScopeAssignment(
                Guid.NewGuid(),
                TestTenantId,
                userId: FakeAdminUserId,
                description: "Tenant-wide exam result regression test scope"),
            autoSave: true);
        await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);

        var standard = await _standardManager.CreateAsync(
            stage.Id,
            $"Result Std {unique}"[..30],
            12,
            shiftId: null);
        await _standardRepository.InsertAsync(standard, autoSave: true);

        var division = await _divisionManager.CreateAsync(
            standard.Id,
            $"C-{unique}"[..30],
            40);
        await _divisionRepository.InsertAsync(division, autoSave: true);

        var english = new Subject(
            Guid.NewGuid(),
            TestTenantId,
            $"English {unique}"[..30],
            "#1976D2",
            medium.Id);
        var computerScience = new Subject(
            Guid.NewGuid(),
            TestTenantId,
            $"Computer Science {unique}"[..30],
            "#00A76F",
            medium.Id);
        await _subjectRepository.InsertManyAsync([english, computerScience], autoSave: true);

        var electiveGroup = new ElectiveGroup(
            Guid.NewGuid(),
            TestTenantId,
            $"English or CS {unique}"[..30],
            "#7C4DFF",
            medium.Id);
        electiveGroup.AddSubject(english.Id);
        electiveGroup.AddSubject(computerScience.Id);
        await _electiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

        await _assignedSubjectRepository.InsertManyAsync(
            [
                new AssignedSubject(
                    Guid.NewGuid(), TestTenantId, standard.Id, english.Id, $"ENG{unique[..5]}",
                    SubjectStatus.Published, SubjectType.Elective, electiveGroup.Id),
                new AssignedSubject(
                    Guid.NewGuid(), TestTenantId, standard.Id, computerScience.Id, $"CSC{unique[..5]}",
                    SubjectStatus.Published, SubjectType.Elective, electiveGroup.Id)
            ],
            autoSave: true);

        var englishStudent = CreateStudent(
            "English",
            1,
            board.AcademicYearId,
            board.Id,
            medium.Id,
            stage.Id,
            standard.Id,
            division.Id,
            unique);
        var computerScienceStudent = CreateStudent(
            "Computer",
            2,
            board.AcademicYearId,
            board.Id,
            medium.Id,
            stage.Id,
            standard.Id,
            division.Id,
            unique);
        await _studentRepository.InsertManyAsync([englishStudent, computerScienceStudent], autoSave: true);

        var englishSelection = new StudentElectiveSelection(
            Guid.NewGuid(), TestTenantId, englishStudent.Id, electiveGroup.Id, english.Id, medium.Id);
        englishSelection.LockSelection();
        var computerScienceSelection = new StudentElectiveSelection(
            Guid.NewGuid(), TestTenantId, computerScienceStudent.Id, electiveGroup.Id, computerScience.Id, medium.Id);
        computerScienceSelection.LockSelection();
        await _selectionRepository.InsertManyAsync([englishSelection, computerScienceSelection], autoSave: true);

        var examType = await _examTypeManager.CreateAsync(
            $"Elective {unique}"[..30],
            description: null,
            medium.Id);
        var exam = _examManager.Create(
            examType.Id,
            $"ENG CS Result {unique}"[..40],
            standard.Id,
            [division.Id],
            [english.Id, computerScience.Id]);
        foreach (var examSubject in exam.ExamSubjects)
        {
            var totalMarks = examSubject.SubjectId == english.Id
                ? englishTotalMarks
                : computerScienceTotalMarks;
            examSubject.UpdateSchedule(
                DateTime.Today,
                DateTime.Today.AddHours(9),
                DateTime.Today.AddHours(12),
                totalMarks,
                totalMarks * 0.33,
                notes: null);
        }
        await _examRepository.InsertAsync(exam, autoSave: true);

        await AddPublishedMarkAsync(exam.Id, division.Id, englishStudent.Id, english.Id, englishMarks, isAbsent: false);
        await AddPublishedMarkAsync(
            exam.Id,
            division.Id,
            computerScienceStudent.Id,
            computerScience.Id,
            computerScienceMarks,
            computerScienceAbsent);
        await AddPublishedWorkflowStateAsync(exam.Id, division.Id, english.Id);
        await AddPublishedWorkflowStateAsync(exam.Id, division.Id, computerScience.Id);

        return new ElectiveExamFixture(
            exam,
            division,
            english,
            computerScience,
            englishStudent,
            computerScienceStudent);
    }

    private async Task AddPublishedMarkAsync(
        Guid examId,
        Guid divisionId,
        Guid studentId,
        Guid subjectId,
        double? marks,
        bool isAbsent)
    {
        var mark = new ExamMark(
            Guid.NewGuid(),
            examId,
            studentId,
            subjectId,
            divisionId,
            marks,
            isAbsent,
            tenantId: TestTenantId);
        mark.UpdateAttendance(isAbsent ? ExamAttendanceStatus.Absent : ExamAttendanceStatus.Present);
        mark.Publish();
        await _markRepository.InsertAsync(mark, autoSave: true);
    }

    private async Task AddPublishedWorkflowStateAsync(Guid examId, Guid divisionId, Guid subjectId)
    {
        var state = new ExamMarksWorkflowState(
            Guid.NewGuid(),
            examId,
            subjectId,
            divisionId,
            TestTenantId);
        state.TransitionTo(ExamMarksWorkflowStatus.Published);
        await _workflowStateRepository.InsertAsync(state, autoSave: true);
    }

    private static Student CreateStudent(
        string firstName,
        int rollNo,
        Guid academicYearId,
        Guid boardId,
        Guid mediumId,
        Guid stageId,
        Guid standardId,
        Guid divisionId,
        string unique)
    {
        return new Student(
            Guid.NewGuid(),
            TestTenantId,
            firstName,
            null,
            $"Student {unique}"[..30],
            academicYearId,
            boardId,
            mediumId,
            stageId,
            standardId,
            divisionId,
            grNo: 10000 + rollNo,
            rollNo,
            StudentLifeCycleStatusEnum.Enrolled);
    }

    private sealed record ElectiveExamFixture(
        Exam Exam,
        Division Division,
        Subject English,
        Subject ComputerScience,
        Student EnglishStudent,
        Student ComputerScienceStudent);
}
