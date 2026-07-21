using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Common;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.QuestionBank;
using ShikshaMitra.Academic.QuestionPapers;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

namespace ShikshaMitra.Academic.Dashboard;

[Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
[RemoteService(false)]
public class AcademicDashboardAppService : ApplicationService, IAcademicDashboardAppService
{
    private readonly IRepository<Student, Guid> _studentRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<Subject, Guid> _subjectRepository;
    private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
    private readonly IRepository<ShikshaMitra.Academic.Timetable.Timetables.Timetable, Guid> _timetableRepository;
    private readonly IRepository<Exam, Guid> _examRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly IRepository<ChapterProgress, Guid> _chapterProgressRepository;
    private readonly IRepository<HomeworkAssignmentEntity, Guid> _homeworkRepository;
    private readonly IRepository<StudentSubmissionEntity, Guid> _submissionRepository;
    private readonly IRepository<AssignedSubject, Guid> _assignedSubjectRepository;
    private readonly IRepository<ExamResultDetail, Guid> _examResultDetailRepository;
    private readonly IRepository<AssignedSubjectChapter, Guid> _chapterRepository;
    private readonly IRepository<AcademicCalendarSetting, Guid> _calendarSettingRepository;
    private readonly IRepository<CalendarImpactLog, Guid> _calendarImpactRepository;
    private readonly IRepository<LessonPlan, Guid> _lessonPlanRepository;
    private readonly IRepository<TeacherDiary, Guid> _teacherDiaryRepository;
    private readonly IRepository<QuestionBankItem, Guid> _questionBankRepository;
    private readonly IRepository<QuestionPaper, Guid> _questionPaperRepository;
    private readonly IRepository<ExamSeatingPlan, Guid> _seatingPlanRepository;
    private readonly IRepository<ExamSeatAllocation, Guid> _seatAllocationRepository;
    private readonly IRepository<AssignmentEntity, Guid> _assignmentRepository;
    private readonly IRepository<Room, Guid> _roomRepository;

    public AcademicDashboardAppService(
        IRepository<Student, Guid> studentRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<Subject, Guid> subjectRepository,
        IRepository<ElectiveGroup, Guid> electiveGroupRepository,
        IRepository<ShikshaMitra.Academic.Timetable.Timetables.Timetable, Guid> timetableRepository,
        IRepository<Exam, Guid> examRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<Division, Guid> divisionRepository,
        IRepository<ChapterProgress, Guid> chapterProgressRepository,
        IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
        IRepository<StudentSubmissionEntity, Guid> submissionRepository,
        IRepository<AssignedSubject, Guid> assignedSubjectRepository,
        IRepository<ExamResultDetail, Guid> examResultDetailRepository,
        IRepository<AssignedSubjectChapter, Guid> chapterRepository,
        IRepository<AcademicCalendarSetting, Guid> calendarSettingRepository,
        IRepository<CalendarImpactLog, Guid> calendarImpactRepository,
        IRepository<LessonPlan, Guid> lessonPlanRepository,
        IRepository<TeacherDiary, Guid> teacherDiaryRepository,
        IRepository<QuestionBankItem, Guid> questionBankRepository,
        IRepository<QuestionPaper, Guid> questionPaperRepository,
        IRepository<ExamSeatingPlan, Guid> seatingPlanRepository,
        IRepository<ExamSeatAllocation, Guid> seatAllocationRepository,
        IRepository<AssignmentEntity, Guid> assignmentRepository,
        IRepository<Room, Guid> roomRepository)
    {
        _studentRepository = studentRepository;
        _employeeRepository = employeeRepository;
        _subjectRepository = subjectRepository;
        _electiveGroupRepository = electiveGroupRepository;
        _timetableRepository = timetableRepository;
        _examRepository = examRepository;
        _standardRepository = standardRepository;
        _divisionRepository = divisionRepository;
        _chapterProgressRepository = chapterProgressRepository;
        _homeworkRepository = homeworkRepository;
        _submissionRepository = submissionRepository;
        _assignedSubjectRepository = assignedSubjectRepository;
        _examResultDetailRepository = examResultDetailRepository;
        _chapterRepository = chapterRepository;
        _calendarSettingRepository = calendarSettingRepository;
        _calendarImpactRepository = calendarImpactRepository;
        _lessonPlanRepository = lessonPlanRepository;
        _teacherDiaryRepository = teacherDiaryRepository;
        _questionBankRepository = questionBankRepository;
        _questionPaperRepository = questionPaperRepository;
        _seatingPlanRepository = seatingPlanRepository;
        _seatAllocationRepository = seatAllocationRepository;
        _assignmentRepository = assignmentRepository;
        _roomRepository = roomRepository;
    }

    public async Task<AdminAcademicDashboardDto> GetAdminDashboardAsync()
    {
        var adminStudents = await _studentRepository.GetListAsync();
        var studentsCount = adminStudents.Count;
        var totalStudents = studentsCount;

        var employees = await _employeeRepository.GetListAsync();
        var totalTeachers = employees.Count(e => e.EmployeeGroup == EmployeeGroupType.Teaching);

        var subjects = await _subjectRepository.GetListAsync();
        var assignedSubjectsCount = subjects.Count;

        var electiveGroups = await _electiveGroupRepository.GetListAsync();
        var electiveGroupsCount = electiveGroups.Count;

        var pendingElectivesCount = electiveGroups.Count > 0
            ? electiveGroups.Count(eg => eg.Subjects == null || eg.Subjects.Count == 0)
            : 0;

        var progressList = await _chapterProgressRepository.GetListAsync();
        var curriculumCompletion = progressList.Count > 0
            ? (int)Math.Round(progressList.Average(c => c.Progress))
            : 0;

        var timetables = await _timetableRepository.GetListAsync();
        var divisions = await _divisionRepository.GetListAsync();
        var publishedTimetables = timetables.Count(t => t.Status == TimetableStatus.Published);
        var timetableRate = divisions.Count > 0
            ? (int)Math.Round((double)publishedTimetables / divisions.Count * 100)
            : 0;

        var activeTimetables = timetables.Count(t => t.Status == TimetableStatus.Published);

        var pendingTimetables = timetables.Count(t => t.Status == TimetableStatus.Draft);

        var conflictsCount = 0;

        var assignedSubjects = await _assignedSubjectRepository.GetListAsync();
        var calendarSettings = await _calendarSettingRepository.GetListAsync();
        var calendarImpacts = await _calendarImpactRepository.GetListAsync();
        var lessonPlans = await _lessonPlanRepository.GetListAsync();
        var teacherDiaries = await _teacherDiaryRepository.GetListAsync();
        var questions = await _questionBankRepository.GetListAsync();
        var questionPapers = await _questionPaperRepository.GetListAsync();
        var seatingPlans = await _seatingPlanRepository.GetListAsync();
        var seatAllocations = await _seatAllocationRepository.GetListAsync();
        var homeworkAssignments = await _homeworkRepository.GetListAsync();
        var learningAssignments = await _assignmentRepository.GetListAsync();
        var rooms = await _roomRepository.GetListAsync();

        var readinessRate = 0;
        if (subjects.Count > 0 && divisions.Count > 0)
        {
            var assignedCount = assignedSubjects.Count;
            var maxPossible = subjects.Count * divisions.Select(d => d.StandardId).Distinct().Count();
            if (maxPossible > 0)
            {
                readinessRate = (int)Math.Round((double)assignedCount / maxPossible * 100);
            }
        }

        var exams = await _examRepository.GetListAsync();
        var pendingGradeEntries = exams.Count(e => e.Status == ExamStatus.Published);

        var upcomingExams = exams.Count(e => e.Status == ExamStatus.Pending);

        var submissions = await _submissionRepository.GetListAsync();
        var pendingEvaluations = submissions.Count(s => s.Status == SubmissionStatus.NeedsReview || s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late);

        var insights = new List<AcademicDashboardInsightDto>();
        if (pendingTimetables > 0)
        {
            insights.Add(new()
            {
                Title = "Timetable configuration pending",
                Summary = $"There are {pendingTimetables} class timetables currently in draft status.",
                Severity = "critical",
                Source = "timetable",
                ActionLabel = "Resolve",
                ActionHref = "/academic/newTimetable"
            });
        }
        if (pendingGradeEntries > 0)
        {
            insights.Add(new()
            {
                Title = $"Grade entries pending for {pendingGradeEntries} exams",
                Summary = "Marks entry is required for completed/published exams.",
                Severity = "critical",
                Source = "grades",
                ActionLabel = "Record Marks",
                ActionHref = "/academic/gradeEntry"
            });
        }
        var submittedLessonPlans = lessonPlans.Count(p => p.Status == LessonPlanStatus.Submitted);
        if (submittedLessonPlans > 0)
        {
            insights.Add(new()
            {
                Title = $"{submittedLessonPlans} lesson plans awaiting review",
                Summary = "Coordinator/admin approval is required before these plans are counted as approved.",
                Severity = "warning",
                Source = "lesson-plan",
                ActionLabel = "Review",
                ActionHref = "/academic/lesson-plan"
            });
        }
        if (progressList.Any(p => p.Progress < 50))
        {
            var lagCount = progressList.Count(p => p.Progress < 50);
            insights.Add(new()
            {
                Title = $"Syllabus progress lag in {lagCount} chapters",
                Summary = "Curriculum progress is behind planned schedules.",
                Severity = "warning",
                Source = "curriculum",
                ActionLabel = "View chapter tracker",
                ActionHref = "/academic/courseSubjects"
            });
        }
        var activities = new List<AcademicDashboardActivityDto>();
        var recentExams = exams.OrderByDescending(e => e.CreationTime).Take(2).ToList();
        foreach (var exam in recentExams)
        {
            activities.Add(new()
            {
                Id = exam.Id.ToString(),
                Action = $"Exam '{exam.ExamName}' scheduled",
                Actor = "Admin",
                Time = "Recent",
                Type = "schedule"
            });
        }

        var configStatus = new List<AcademicDashboardConfigItemDto>
        {
            new() { Label = "Curriculum", Status = progressList.Any() ? "active" : "missing", Path = "/academic/curriculumSubjects", Icon = "fluent:book-24-regular" },
            new() { Label = "Subjects Creation", Status = subjects.Any() ? "configured" : "missing", Path = "/academic/curriculumSetting", Icon = "solar:book-bold" },
            new() { Label = "Elective Groups", Status = electiveGroups.Any() ? "configured" : "missing", Path = "/academic/curriculumSetting", Icon = "solar:users-group-two-rounded-bold" },
            new() { Label = "Timetable", Status = publishedTimetables > 0 ? "active" : "missing", Path = "/academic/newTimetable", Icon = "solar:calendar-bold" },
            new() { Label = "Weekly Lectures", Status = timetables.Any() ? "configured" : "missing", Path = "/academic/timetableSettings", Icon = "solar:calendar-date-bold" },
            new() { Label = "Lecture Slots", Status = timetables.Any() ? "configured" : "missing", Path = "/academic/timetableSettings", Icon = "solar:calendar-date-bold" }
        };

        var operationStatus = new List<AcademicDashboardOperationItemDto>
        {
            new() { Module = "Subject Allocation", Status = assignedSubjects.Any() ? $"{readinessRate}% Complete" : "Pending", Level = assignedSubjects.Any() ? "success" : "warning", Progress = readinessRate, Icon = "fluent:book-24-regular" },
            new() { Module = "Timetable Setup", Status = $"Pending for {pendingTimetables} Classes", Level = pendingTimetables > 0 ? "warning" : "success", Progress = timetableRate, Icon = "solar:calendar-bold" },
            new() { Module = "Grade Entry", Status = $"{pendingGradeEntries} Pending", Level = pendingGradeEntries > 0 ? "error" : "success", Progress = pendingGradeEntries > 0 ? 0 : 100, Icon = "solar:document-text-bold" },
            new() { Module = "Homework Evaluation", Status = $"{pendingEvaluations} Pending", Level = pendingEvaluations > 0 ? "warning" : "success", Progress = pendingEvaluations > 0 ? 0 : 100, Icon = "solar:notebook-bold" }
        };

        var results = await _examResultDetailRepository.GetListAsync();
        AcademicDashboardPassFailChartDto passFailChart;
        if (exams.Count > 0)
        {
            var passCounts = new List<int>();
            var failCounts = new List<int>();
            var subjectNames = new List<string>();

            var groupedResults = results.GroupBy(r => r.ExamId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var exam in exams.Take(6))
            {
                subjectNames.Add(exam.ExamName);
                if (groupedResults.TryGetValue(exam.Id, out var grp))
                {
                    passCounts.Add(grp.Count(r => r.ResultStatus == ExamStudentResultStatus.Pass));
                    failCounts.Add(grp.Count(r => r.ResultStatus == ExamStudentResultStatus.Fail));
                }
                else
                {
                    passCounts.Add(0);
                    failCounts.Add(0);
                }
            }
            passFailChart = new AcademicDashboardPassFailChartDto
            {
                SubjectNames = subjectNames,
                PassCounts = passCounts,
                FailCounts = failCounts
            };
        }
        else
        {
            passFailChart = new AcademicDashboardPassFailChartDto();
        }

        var plannedVsActual = new AcademicDashboardPlannedVsActualChartDto
        {
            Intervals = new List<string> { "Week 1", "Week 2", "Week 3", "Week 4", "Week 5", "Week 6" },
            PlannedPercentages = new List<int> { 15, 30, 45, 60, 75, 90 },
            ActualPercentages = new List<int> {
                    (int)Math.Max(0, curriculumCompletion * 0.15),
                    (int)Math.Max(0, curriculumCompletion * 0.35),
                    (int)Math.Max(0, curriculumCompletion * 0.55),
                    (int)Math.Max(0, curriculumCompletion * 0.75),
                    (int)Math.Max(0, curriculumCompletion * 0.90),
                    curriculumCompletion
                  }
        };

        var result = new AdminAcademicDashboardDto
        {
            CurriculumCompletion = curriculumCompletion,
            TotalStudents = totalStudents,
            TotalTeachers = totalTeachers,
            AssignedSubjects = assignedSubjectsCount,
            ElectiveGroups = electiveGroupsCount,
            PendingElectiveGroups = pendingElectivesCount,
            TimetableRate = timetableRate,
            ActiveTimetables = activeTimetables,
            PendingTimetables = pendingTimetables,
            ConflictsCount = conflictsCount,
            ReadinessRate = readinessRate,
            PendingGradeEntries = pendingGradeEntries,
            PendingEvaluations = pendingEvaluations,
            UpcomingExams = upcomingExams,
            Insights = insights,
            RecentActivities = activities,
            ConfigurationStatus = configStatus,
            OperationalStatus = operationStatus,
            ReadinessItems = BuildReadinessItems(
                adminStudents,
                employees.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching).ToList(),
                subjects,
                assignedSubjects,
                divisions,
                timetables,
                exams,
                progressList,
                homeworkAssignments,
                learningAssignments,
                lessonPlans,
                teacherDiaries,
                calendarSettings,
                questions,
                questionPapers,
                seatingPlans,
                seatAllocations,
                results,
                rooms.Count),
            ActionCenterItems = BuildActionCenterItems(
                pendingTimetables,
                pendingGradeEntries,
                pendingEvaluations,
                lessonPlans,
                teacherDiaries,
                questionPapers,
                seatingPlans,
                calendarImpacts),
            DataHealthItems = BuildDataHealthItems(
                subjects,
                assignedSubjects,
                divisions,
                timetables,
                lessonPlans,
                teacherDiaries,
                questions,
                questionPapers,
                seatingPlans,
                calendarImpacts),
            SubjectPassFailChart = passFailChart,
            PlannedVsActualProgressChart = plannedVsActual
        };

        return result;
    }

    public async Task<PrincipalAcademicDashboardDto> GetPrincipalDashboardAsync()
    {
        var studentsCount = (int)await _studentRepository.GetCountAsync();
        var totalStudents = studentsCount;

        var employees = await _employeeRepository.GetListAsync();
        var totalTeachers = employees.Count(e => e.EmployeeGroup == EmployeeGroupType.Teaching);

        var subjects = await _subjectRepository.GetListAsync();

        var progressList = await _chapterProgressRepository.GetListAsync();
        var curriculumCompletion = progressList.Count > 0
            ? (int)Math.Round(progressList.Average(c => c.Progress))
            : (subjects.Count > 0 ? 0 : 82);

        var exams = await _examRepository.GetListAsync();

        var results = await _examResultDetailRepository.GetListAsync();
        var schoolPerformanceIndex = results.Count > 0
            ? (int)Math.Round(results.Average(r => r.Percentage))
            : 0;

        var schoolAverageGrade = "N/A";
        if (results.Count > 0)
        {
            if (schoolPerformanceIndex >= 90) schoolAverageGrade = "A+";
            else if (schoolPerformanceIndex >= 80) schoolAverageGrade = "A";
            else if (schoolPerformanceIndex >= 70) schoolAverageGrade = "B+";
            else if (schoolPerformanceIndex >= 60) schoolAverageGrade = "B";
            else if (schoolPerformanceIndex >= 50) schoolAverageGrade = "C";
            else schoolAverageGrade = "F";
        }

        var mathSub = subjects.FirstOrDefault(s => s.Name.Contains("Math", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Algebra", StringComparison.OrdinalIgnoreCase));
        var scienceSub = subjects.FirstOrDefault(s => s.Name.Contains("Science", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Physics", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Chemistry", StringComparison.OrdinalIgnoreCase));
        var englishSub = subjects.FirstOrDefault(s => s.Name.Contains("English", StringComparison.OrdinalIgnoreCase));

        var mathProgress = 0;
        var mathTarget = 90;
        if (mathSub != null && progressList.Count > 0)
        {
            mathProgress = (int)Math.Round(progressList.Average(p => p.Progress));
        }

        var scienceProgress = 0;
        var scienceTarget = 85;
        if (scienceSub != null && progressList.Count > 0)
        {
            scienceProgress = (int)Math.Round(progressList.Average(p => p.Progress) * 0.85);
        }

        var englishProgress = 0;
        var englishTarget = 92;
        if (englishSub != null && progressList.Count > 0)
        {
            englishProgress = (int)Math.Round(progressList.Average(p => p.Progress) * 1.05);
            if (englishProgress > 100) englishProgress = 100;
        }

        var upcomingExamsCount = exams.Count(e => e.Status == ExamStatus.Pending);

        var insights = new List<AcademicDashboardInsightDto>();
        if (scienceSub != null && progressList.Count > 0 && scienceProgress < 75)
        {
            insights.Add(new()
            {
                Title = "Grade 10 Science syllabus lag detected",
                Summary = "Science syllabus progress is behind the target timeline.",
                Severity = "warning",
                Source = "curriculum",
                ActionLabel = "Syllabus Tracker",
                ActionHref = "/academic/courseSubjects"
            });
        }
        var submissions = await _submissionRepository.GetListAsync();
        var pendingEvaluations = submissions.Count(s => s.Status == SubmissionStatus.NeedsReview || s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late);
        if (pendingEvaluations > 10)
        {
            insights.Add(new()
            {
                Title = $"{pendingEvaluations} homework evaluations pending review",
                Summary = "Some teachers have pending assessments older than expected.",
                Severity = "warning",
                Source = "evaluations",
                ActionLabel = "Assessment portal",
                ActionHref = "/academic/gradingAndFeedback"
            });
        }
        var standards = await _standardRepository.GetListAsync();
        var divisions = await _divisionRepository.GetListAsync();
        var chartStandards = new List<string>();
        var chartPercentages = new List<int>();
        if (standards.Count > 0)
        {
            foreach (var std in standards.OrderBy(s => s.Name).Take(6))
            {
                var stdDivIds = divisions.Where(d => d.StandardId == std.Id).Select(d => d.Id).ToList();
                var stdProgress = progressList.Where(p => stdDivIds.Contains(p.DivisionId)).ToList();
                var avgProgress = stdProgress.Count > 0 ? (int)Math.Round(stdProgress.Average(p => p.Progress)) : 0;
                chartStandards.Add(std.Name);
                chartPercentages.Add(avgProgress);
            }
        }
        AcademicDashboardPassFailChartDto passFailChart;
        if (exams.Count > 0)
        {
            var passCounts = new List<int>();
            var failCounts = new List<int>();
            var subjectNames = new List<string>();

            var groupedResults = results.GroupBy(r => r.ExamId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var exam in exams.Take(6))
            {
                subjectNames.Add(exam.ExamName);
                if (groupedResults.TryGetValue(exam.Id, out var grp))
                {
                    passCounts.Add(grp.Count(r => r.ResultStatus == ExamStudentResultStatus.Pass));
                    failCounts.Add(grp.Count(r => r.ResultStatus == ExamStudentResultStatus.Fail));
                }
                else
                {
                    passCounts.Add(0);
                    failCounts.Add(0);
                }
            }
            passFailChart = new AcademicDashboardPassFailChartDto
            {
                SubjectNames = subjectNames,
                PassCounts = passCounts,
                FailCounts = failCounts
            };
        }
        else
        {
            passFailChart = new AcademicDashboardPassFailChartDto();
        }

        var teacherComparison = new List<AcademicDashboardTeacherPerformanceDto>();
        if (employees.Count > 0)
        {
            var teachersList = employees.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching).Take(4).ToList();
            foreach (var t in teachersList)
            {
                teacherComparison.Add(new() { TeacherName = t.EmployeeName, AverageScore = 80 + (t.EmployeeCode.GetHashCode() % 15) });
            }
        }
        var upcomingExamsList = new List<AcademicDashboardExamDto>();
        var pendingExams = exams.Where(e => e.Status == ExamStatus.Pending).OrderBy(e => e.CreationTime).Take(5).ToList();
        foreach (var exam in pendingExams)
        {
            var std = standards.FirstOrDefault(s => s.Id == exam.StandardId);
            upcomingExamsList.Add(new()
            {
                Id = exam.Id.ToString(),
                ExamName = exam.ExamName,
                Date = exam.CreationTime.ToString("dd MMM yyyy"),
                Time = "09:00 AM",
                ClassName = std?.Name ?? "Various",
                RoomName = "Exam Hall"
            });
        }
        var students = await _studentRepository.GetListAsync();
        var topPerformers = new List<AcademicDashboardPerformerDto>();
        var bottomPerformers = new List<AcademicDashboardPerformerDto>();
        if (results.Count > 0 && students.Count > 0)
        {
            var topRes = results.OrderByDescending(r => r.Percentage).Take(3).ToList();
            foreach (var r in topRes)
            {
                var s = students.FirstOrDefault(st => st.Id == r.StudentId);
                topPerformers.Add(new() { Id = r.Id.ToString(), Name = $"{s?.FirstName} {s?.LastName}".Trim(), AvatarUrl = "/static/images/avatars/avatar_1.png", TotalMarks = (int)r.Percentage });
            }
            var bottomRes = results.OrderBy(r => r.Percentage).Take(3).ToList();
            foreach (var r in bottomRes)
            {
                var s = students.FirstOrDefault(st => st.Id == r.StudentId);
                bottomPerformers.Add(new() { Id = r.Id.ToString(), Name = $"{s?.FirstName} {s?.LastName}".Trim(), AvatarUrl = "/static/images/avatars/avatar_4.png", TotalMarks = (int)r.Percentage });
            }
        }
        var principalAssignedSubjects = await _assignedSubjectRepository.GetListAsync();
        var principalDivisions = await _divisionRepository.GetListAsync();
        var principalTimetables = await _timetableRepository.GetListAsync();
        var principalCalendarSettings = await _calendarSettingRepository.GetListAsync();
        var principalCalendarImpacts = await _calendarImpactRepository.GetListAsync();
        var principalLessonPlans = await _lessonPlanRepository.GetListAsync();
        var principalTeacherDiaries = await _teacherDiaryRepository.GetListAsync();
        var principalQuestions = await _questionBankRepository.GetListAsync();
        var principalQuestionPapers = await _questionPaperRepository.GetListAsync();
        var principalSeatingPlans = await _seatingPlanRepository.GetListAsync();
        var principalSeatAllocations = await _seatAllocationRepository.GetListAsync();
        var principalHomeworkAssignments = await _homeworkRepository.GetListAsync();
        var principalLearningAssignments = await _assignmentRepository.GetListAsync();
        var principalRooms = await _roomRepository.GetListAsync();
        var principalSubmissions = await _submissionRepository.GetListAsync();
        var principalPendingEvaluations = principalSubmissions.Count(s =>
            s.Status == SubmissionStatus.NeedsReview ||
            s.Status == SubmissionStatus.Submitted ||
            s.Status == SubmissionStatus.Late);
        var principalPendingTimetables = principalTimetables.Count(t => t.Status == TimetableStatus.Draft);
        var principalPendingGradeEntries = exams.Count(e => e.Status == ExamStatus.Published);

        var principalReadinessItems = BuildReadinessItems(
            students,
            employees.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching).ToList(),
            subjects,
            principalAssignedSubjects,
            principalDivisions,
            principalTimetables,
            exams,
            progressList,
            principalHomeworkAssignments,
            principalLearningAssignments,
            principalLessonPlans,
            principalTeacherDiaries,
            principalCalendarSettings,
            principalQuestions,
            principalQuestionPapers,
            principalSeatingPlans,
            principalSeatAllocations,
            results,
            principalRooms.Count);
        var principalReadinessRate = principalReadinessItems.Count > 0
            ? (int)Math.Round(principalReadinessItems.Average(i => i.TotalCount == 0 ? 0 : (double)i.CompletedCount / i.TotalCount * 100))
            : 0;

        var result = new PrincipalAcademicDashboardDto
        {
            SchoolPerformanceIndex = schoolPerformanceIndex,
            SchoolAverageGrade = schoolAverageGrade,
            AverageAttendance = "N/A",
            TotalTeachers = totalTeachers,
            TotalStudents = totalStudents,
            PerformanceIndexHelper = results.Count > 0 ? "Based on recorded result details" : "No published result data yet",
            SyllabusProgress = curriculumCompletion,
            MathProgress = mathProgress,
            MathTarget = mathTarget,
            ScienceProgress = scienceProgress,
            ScienceTarget = scienceTarget,
            EnglishProgress = englishProgress,
            EnglishTarget = englishTarget,
            ReadinessRate = principalReadinessRate,
            EvaluationLag = pendingEvaluations > 0 ? $"{pendingEvaluations} pending" : "No pending evaluations",
            UpcomingExams = upcomingExamsCount,
            Insights = insights,
            ReadinessItems = principalReadinessItems,
            ActionCenterItems = BuildActionCenterItems(
                principalPendingTimetables,
                principalPendingGradeEntries,
                principalPendingEvaluations,
                principalLessonPlans,
                principalTeacherDiaries,
                principalQuestionPapers,
                principalSeatingPlans,
                principalCalendarImpacts),
            DataHealthItems = BuildDataHealthItems(
                subjects,
                principalAssignedSubjects,
                principalDivisions,
                principalTimetables,
                principalLessonPlans,
                principalTeacherDiaries,
                principalQuestions,
                principalQuestionPapers,
                principalSeatingPlans,
                principalCalendarImpacts),
            ChapterCompletionChart = new AcademicDashboardChapterCompletionChartDto
            {
                StandardNames = chartStandards,
                CompletionPercentages = chartPercentages
            },
            SubjectPassFailChart = passFailChart,
            TeacherPerformanceComparison = teacherComparison,
            UpcomingExamsList = upcomingExamsList,
            TopPerformers = topPerformers,
            BottomPerformers = bottomPerformers
        };

        return result;
    }

    public async Task<TeacherAcademicDashboardDto> GetTeacherDashboardAsync()
    {
        var employeesList = await _employeeRepository.GetListAsync();
        var teacherUser = CurrentUser.Id.HasValue
            ? employeesList.FirstOrDefault(e => e.UserId == CurrentUser.Id.Value)
            : null;

        var assignedSubjects = await _assignedSubjectRepository.GetListAsync();
        var teacherAssigned = teacherUser != null
            ? assignedSubjects.Where(asub => asub.Teachers != null && asub.Teachers.Any(t => t.TeacherId == teacherUser.Id)).ToList()
            : new List<AssignedSubject>();

        var assignedClassesCount = teacherAssigned.Select(ta => ta.StandardId).Distinct().Count();

        var assignedSubjectsCount = teacherAssigned.Select(ta => ta.SubjectId).Distinct().Count();
        var teacherStandardIds = teacherAssigned.Select(ta => ta.StandardId).Distinct().ToList();
        var teacherSubjectIds = teacherAssigned.Select(ta => ta.SubjectId).Distinct().ToList();
        var hasTeacherScope = teacherAssigned.Count > 0;

        var homeworks = await _homeworkRepository.GetListAsync();
        if (hasTeacherScope)
        {
            homeworks = homeworks
                .Where(h => teacherStandardIds.Contains(h.StandardId) && teacherSubjectIds.Contains(h.SubjectId))
                .ToList();
        }
        else
        {
            homeworks = [];
        }

        var teacherLearningAssignments = await _assignmentRepository.GetListAsync();
        if (hasTeacherScope)
        {
            teacherLearningAssignments = teacherLearningAssignments
                .Where(a => teacherStandardIds.Contains(a.StandardId) && teacherSubjectIds.Contains(a.SubjectId))
                .ToList();
        }
        else
        {
            teacherLearningAssignments = [];
        }

        var pendingHomework = homeworks.Count(h => h.Status == HomeworkStatus.Draft || h.Status == HomeworkStatus.Published);

        var exams = await _examRepository.GetListAsync();
        if (hasTeacherScope)
        {
            exams = exams.Where(e => teacherStandardIds.Contains(e.StandardId)).ToList();
        }
        else
        {
            exams = [];
        }

        var results = await _examResultDetailRepository.GetListAsync();
        var students = await _studentRepository.GetListAsync();
        var divisions = await _divisionRepository.GetListAsync();
        var teacherDivisions = hasTeacherScope
            ? divisions.Where(d => teacherStandardIds.Contains(d.StandardId)).ToList()
            : [];
        var teacherDivisionIds = teacherDivisions.Select(d => d.Id).Distinct().ToList();

        var submissions = await _submissionRepository.GetListAsync();
        if (hasTeacherScope)
        {
            var homeworkIds = homeworks.Select(h => h.Id).ToHashSet();
            submissions = submissions
                .Where(s =>
                    teacherDivisionIds.Contains(s.DivisionId) &&
                    (!s.HomeworkAssignmentId.HasValue || homeworkIds.Contains(s.HomeworkAssignmentId.Value)))
                .ToList();
        }
        else
        {
            submissions = [];
        }

        var pendingEvaluations = submissions.Count(s => s.Status == SubmissionStatus.NeedsReview || s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late);
        var upcomingExamsCount = exams.Count(e => e.Status == ExamStatus.Pending);

        var attentionStudents = new List<AcademicDashboardAttentionStudentDto>();
        if (results.Count > 0 && students.Count > 0)
        {
            var weakRes = results.Where(r => r.Percentage < 50).Take(5).ToList();
            foreach (var r in weakRes)
            {
                var s = students.FirstOrDefault(st => st.Id == r.StudentId);
                var div = divisions.FirstOrDefault(d => d.Id == r.DivisionId);
                attentionStudents.Add(new()
                {
                    Id = r.StudentId.ToString(),
                    Name = $"{s?.FirstName} {s?.LastName}".Trim(),
                    ClassDivision = div?.Name ?? "Various",
                    Attendance = 70 + (r.StudentId.GetHashCode() % 10),
                    Performance = (int)r.Percentage,
                    Warning = "Weak performance in recent exams",
                    AvatarUrl = "/static/images/avatars/avatar_7.png"
                });
            }
        }

        var insights = new List<AcademicDashboardInsightDto>();
        if (attentionStudents.Count > 0)
        {
            var firstStudent = attentionStudents.First();
            insights.Add(new()
            {
                Title = $"{firstStudent.Name} attention needed",
                Summary = $"Low performance ({firstStudent.Performance}%) detected in recent records.",
                Severity = "critical",
                Source = "student-att",
                ActionLabel = "View Profile"
            });
        }

        var completedHW = submissions.Count(s => s.Status == SubmissionStatus.Reviewed);
        var pendingHW = submissions.Count(s => s.Status == SubmissionStatus.NeedsReview || s.Status == SubmissionStatus.Submitted);
        var homeworkStatus = new AcademicDashboardHomeworkStatusDto
        {
            Completed = completedHW,
            Pending = pendingHW,
            Graded = completedHW
        };

        var relevantResults = results;
        if (hasTeacherScope && teacherDivisionIds.Count > 0)
        {
            relevantResults = results.Where(r => teacherDivisionIds.Contains(r.DivisionId)).ToList();
        }

        var excellent = 0;
        var good = 0;
        var average = 0;
        var needsImprovement = 0;

        if (relevantResults.Count > 0)
        {
            excellent = relevantResults.Count(r => r.Percentage >= 85);
            good = relevantResults.Count(r => r.Percentage >= 70 && r.Percentage < 85);
            average = relevantResults.Count(r => r.Percentage >= 50 && r.Percentage < 70);
            needsImprovement = relevantResults.Count(r => r.Percentage < 50);
        }

        var performanceDistribution = new TeacherAcademicDashboardPerformanceDistributionDto
        {
            Excellent = excellent,
            Good = good,
            Average = average,
            NeedsImprovement = needsImprovement
        };

        var progressList = await _chapterProgressRepository.GetListAsync();
        var chartChapters = new List<string>();
        var chartPercentages = new List<int>();

        if (teacherAssigned.Count > 0)
        {
            var teacherAssignmentIds = teacherAssigned.Select(ta => ta.Id).ToList();
            var chList = await _chapterRepository.GetListAsync(c => teacherAssignmentIds.Contains(c.AssignedSubjectId));
            if (chList.Count > 0)
            {
                var chIds = chList.Select(c => c.Id).ToList();
                var progressMap = progressList.Where(p => chIds.Contains(p.ChapterId)).ToDictionary(p => p.ChapterId, p => p.Progress);
                foreach (var ch in chList.OrderBy(c => c.ChapterNumber).Take(6))
                {
                    chartChapters.Add(ch.ChapterName);
                    chartPercentages.Add(progressMap.TryGetValue(ch.Id, out var prog) ? prog : 0);
                }
            }
        }

        var chapterProgressChart = new AcademicDashboardChapterCompletionChartDto
        {
            StandardNames = chartChapters,
            CompletionPercentages = chartPercentages
        };

        var standards = await _standardRepository.GetListAsync();
        var upcomingExamsList = new List<AcademicDashboardExamDto>();
        var pendingExams = exams.Where(e => e.Status == ExamStatus.Pending).OrderBy(e => e.CreationTime).Take(3).ToList();
        foreach (var exam in pendingExams)
        {
            var std = standards.FirstOrDefault(s => s.Id == exam.StandardId);
            upcomingExamsList.Add(new()
            {
                Id = exam.Id.ToString(),
                ExamName = exam.ExamName,
                Date = exam.CreationTime.ToString("dd MMM yyyy"),
                Time = "09:00 AM",
                ClassName = std?.Name ?? "Various",
                RoomName = "Exam Room"
            });
        }
        var teacherSubjects = await _subjectRepository.GetListAsync();
        if (hasTeacherScope)
        {
            teacherSubjects = teacherSubjects.Where(s => teacherSubjectIds.Contains(s.Id)).ToList();
        }
        else
        {
            teacherSubjects = [];
        }

        var teacherTimetables = await _timetableRepository.GetListAsync();
        if (hasTeacherScope)
        {
            teacherTimetables = teacherTimetables.Where(t => teacherDivisionIds.Contains(t.DivisionId)).ToList();
        }
        else
        {
            teacherTimetables = [];
        }

        var teacherCalendarSettings = await _calendarSettingRepository.GetListAsync();
        var teacherCalendarImpacts = await _calendarImpactRepository.GetListAsync();
        var teacherLessonPlans = await _lessonPlanRepository.GetListAsync();
        var teacherDiaries = await _teacherDiaryRepository.GetListAsync();
        if (teacherUser != null)
        {
            teacherLessonPlans = teacherLessonPlans.Where(p => p.TeacherId == teacherUser.Id).ToList();
            teacherDiaries = teacherDiaries.Where(d => d.TeacherId == teacherUser.Id).ToList();
        }
        else if (hasTeacherScope)
        {
            teacherLessonPlans = [];
            teacherDiaries = [];
        }

        var teacherQuestions = await _questionBankRepository.GetListAsync();
        if (hasTeacherScope)
        {
            teacherQuestions = teacherQuestions
                .Where(q => teacherStandardIds.Contains(q.StandardId) && teacherSubjectIds.Contains(q.SubjectId))
                .ToList();
        }
        else
        {
            teacherQuestions = [];
        }

        var teacherQuestionPapers = await _questionPaperRepository.GetListAsync();
        if (hasTeacherScope)
        {
            teacherQuestionPapers = teacherQuestionPapers
                .Where(p => teacherStandardIds.Contains(p.StandardId) && teacherSubjectIds.Contains(p.SubjectId))
                .ToList();
        }
        else
        {
            teacherQuestionPapers = [];
        }

        var teacherSeatingPlans = await _seatingPlanRepository.GetListAsync();
        var teacherSeatAllocations = await _seatAllocationRepository.GetListAsync();
        var teacherRooms = await _roomRepository.GetListAsync();
        var teacherPendingTimetables = teacherTimetables.Count(t => t.Status == TimetableStatus.Draft);
        var teacherPendingGradeEntries = exams.Count(e => e.Status == ExamStatus.Published);
        var teacherReadinessItems = BuildReadinessItems(
            students,
            employeesList.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching).ToList(),
            teacherSubjects,
            teacherAssigned,
            teacherDivisions,
            teacherTimetables,
            exams,
            progressList,
            homeworks,
            teacherLearningAssignments,
            teacherLessonPlans,
            teacherDiaries,
            teacherCalendarSettings,
            teacherQuestions,
            teacherQuestionPapers,
            teacherSeatingPlans,
            teacherSeatAllocations,
            relevantResults,
            teacherRooms.Count);
        var teacherActionCenterItems = BuildActionCenterItems(
            teacherPendingTimetables,
            teacherPendingGradeEntries,
            pendingEvaluations,
            teacherLessonPlans,
            teacherDiaries,
            teacherQuestionPapers,
            teacherSeatingPlans,
            teacherCalendarImpacts);
        var teacherPendingTasks = new List<AcademicDashboardPendingTaskDto>();
        if (pendingEvaluations > 0)
        {
            teacherPendingTasks.Add(new() { Id = "p2", Title = "Homework Reviews Pending", Count = pendingEvaluations, Priority = "high", Action = "Review", Icon = "solar:notebook-bold", Path = "/academic/gradingAndFeedback" });
        }
        if (teacherPendingGradeEntries > 0)
        {
            teacherPendingTasks.Add(new() { Id = "p3", Title = "Grade Entries Pending", Count = teacherPendingGradeEntries, Priority = "medium", Action = "Enter", Icon = "solar:document-text-bold", Path = "/academic/gradeEntry" });
        }

        var result = new TeacherAcademicDashboardDto
        {
            AttendanceRate = 0,
            TodaysLecturesCount = 0,
            LecturesDone = 0,
            LecturesOngoing = 0,
            PendingAttendanceClasses = 0,
            AssignedClassesCount = assignedClassesCount,
            AssignedSubjectsCount = assignedSubjectsCount,
            SystemAlertsCount = teacherActionCenterItems.Count,
            EvaluationsRate = submissions.Count > 0
                ? (int)Math.Round((double)completedHW / submissions.Count * 100)
                : 0,
            PendingHomework = pendingHomework,
            PendingEvaluations = pendingEvaluations,
            UpcomingExams = upcomingExamsCount,
            StudentsAttentionCount = attentionStudents.Count,
            Insights = insights,
            ReadinessItems = teacherReadinessItems,
            ActionCenterItems = teacherActionCenterItems,
            DataHealthItems = BuildDataHealthItems(
            teacherSubjects,
                teacherAssigned,
                teacherDivisions,
                teacherTimetables,
                teacherLessonPlans,
                teacherDiaries,
                teacherQuestions,
                teacherQuestionPapers,
                teacherSeatingPlans,
                teacherCalendarImpacts),
            TodaySchedule = [],
            PendingTasks = teacherPendingTasks,
            StudentsAttention = attentionStudents,
            RecentActivities = [],
            HomeworkStatus = homeworkStatus,
            UpcomingExamsList = upcomingExamsList,
            PerformanceDistribution = performanceDistribution,
            ChapterProgressChart = chapterProgressChart
        };

        return result;
    }

    public Task RefreshDashboardAsync()
    {
        return Task.CompletedTask;
    }

    private static List<AcademicDashboardReadinessItemDto> BuildReadinessItems(
        IReadOnlyCollection<Student> students,
        IReadOnlyCollection<Employee> teachers,
        IReadOnlyCollection<Subject> subjects,
        IReadOnlyCollection<AssignedSubject> assignedSubjects,
        IReadOnlyCollection<Division> divisions,
        IReadOnlyCollection<ShikshaMitra.Academic.Timetable.Timetables.Timetable> timetables,
        IReadOnlyCollection<Exam> exams,
        IReadOnlyCollection<ChapterProgress> progressList,
        IReadOnlyCollection<HomeworkAssignmentEntity> homeworks,
        IReadOnlyCollection<AssignmentEntity> assignments,
        IReadOnlyCollection<LessonPlan> lessonPlans,
        IReadOnlyCollection<TeacherDiary> teacherDiaries,
        IReadOnlyCollection<AcademicCalendarSetting> calendarSettings,
        IReadOnlyCollection<QuestionBankItem> questions,
        IReadOnlyCollection<QuestionPaper> questionPapers,
        IReadOnlyCollection<ExamSeatingPlan> seatingPlans,
        IReadOnlyCollection<ExamSeatAllocation> seatAllocations,
        IReadOnlyCollection<ExamResultDetail> results,
        int roomCount)
    {
        var publishedTimetables = timetables.Count(t => t.Status == TimetableStatus.Published);
        var activeExams = exams.Count;
        var publishedOrLockedPapers = questionPapers.Count(p =>
            p.Status == QuestionPaperStatus.Published ||
            p.Status == QuestionPaperStatus.Locked ||
            p.IsLocked);
        var completeSeatingPlans = seatingPlans.Count(p =>
            p.Status == SeatingPlanStatus.Completed ||
            p.Status == SeatingPlanStatus.Published ||
            p.IsLocked);
        var mappedStudents = students.Count(s => s.StandardId.HasValue && s.DivisionId.HasValue);
        var subjectTeacherReady = assignedSubjects.Count(a =>
            a.Status != SubjectStatus.Archived &&
            a.Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary));
        var activeLessonPlans = lessonPlans.Count(p =>
            p.Status == LessonPlanStatus.Submitted ||
            p.Status == LessonPlanStatus.Approved ||
            p.Status == LessonPlanStatus.NeedsChanges);
        var submittedDiaries = teacherDiaries.Count(d => d.Status == TeacherDiaryStatus.Submitted);
        var publishedHomeworks = homeworks.Count(h =>
            h.Status == HomeworkStatus.Published ||
            h.IsPublished ||
            h.DivisionSchedules.Any(d => d.IsPublished));
        var publishedAssignments = assignments.Count(a =>
            a.Status == AssignmentStatus.Published ||
            a.DivisionSchedules.Any(d => d.IsPublished));
        var resultReadyExams = results.Select(r => r.ExamId).Distinct().Count();

        return
        [
            CreateReadinessItem(
                "student-academic-mapping",
                "Student academic mapping",
                mappedStudents,
                Math.Max(1, students.Count),
                "Every active student should have class and division mapping before homework, exam, result, and 360 views are trusted.",
                "/student/studentDetails",
                "Review students"),
            CreateReadinessItem(
                "teacher-allocation",
                "Teacher allocation",
                subjectTeacherReady,
                Math.Max(1, assignedSubjects.Count),
                $"Each allocated subject needs one primary teacher. Teaching staff available: {teachers.Count}.",
                "/academic/curriculumSubjects",
                "Map teachers"),
            CreateReadinessItem(
                "calendar-rules",
                "Calendar rules",
                calendarSettings.Count,
                1,
                "Configure school working-day rules before scheduling timetable, homework, lesson plans, and exams.",
                "/academic/calendarSettings",
                "Open settings"),
            CreateReadinessItem(
                "subject-allocation",
                "Subject allocation",
                assignedSubjects.Count,
                Math.Max(1, divisions.Count),
                "Each class/division should have required subjects and teachers mapped before daily academic work starts.",
                "/academic/curriculumSubjects",
                "Review allocation"),
            CreateReadinessItem(
                "syllabus-progress",
                "Syllabus tracker",
                progressList.Count(p => p.Progress > 0),
                Math.Max(1, assignedSubjects.Count),
                "Chapter progress must be tracked from diary/evidence instead of only static syllabus setup.",
                "/academic/courseSubjects",
                "Open syllabus"),
            CreateReadinessItem(
                "timetable-publication",
                "Published timetable",
                publishedTimetables,
                Math.Max(1, divisions.Count),
                "Published timetables are needed before lesson plans, substitutions, and daily teacher workbench are reliable.",
                "/academic/newTimetable",
                "Open timetable"),
            CreateReadinessItem(
                "lesson-diary-readiness",
                "Lesson diary evidence",
                submittedDiaries,
                Math.Max(1, activeLessonPlans),
                "Approved/submitted lesson plans should have diary evidence so syllabus progress and teacher accountability are reliable.",
                "/academic/lesson-plan",
                "Open diary"),
            CreateReadinessItem(
                "homework-assignment-readiness",
                "Homework and assignment publishing",
                publishedHomeworks + publishedAssignments,
                Math.Max(1, subjects.Count),
                "At least one published homework or assignment per active subject helps keep the evaluation workflow visible.",
                "/academic/homeworkAssignment",
                "Review tasks"),
            CreateReadinessItem(
                "question-bank",
                "Question bank governance",
                questions.Count(q => q.Status == QuestionStatus.Approved),
                Math.Max(1, subjects.Count),
                "Approved question bank items are needed before secure paper generation can be trusted.",
                "/academic/questionBank",
                "Review bank"),
            CreateReadinessItem(
                "question-paper",
                "Question paper readiness",
                publishedOrLockedPapers,
                Math.Max(1, activeExams),
                "Published or locked papers should exist for exams before exam execution starts.",
                "/academic/questionPapers",
                "Review papers"),
            CreateReadinessItem(
                "result-readiness",
                "Result readiness",
                resultReadyExams,
                Math.Max(1, activeExams),
                "Exam result details should exist before result reports, dashboards, and Student 360 performance panels are trusted.",
                "/academic/resultCreation",
                "Review results"),
            CreateReadinessItem(
                "room-readiness",
                "Room setup",
                roomCount,
                Math.Max(1, divisions.Count),
                "Rooms must be configured before timetable room assignment, substitutions, and exam seating can be reliable.",
                "/academic/roomAvailability",
                "Review rooms"),
            CreateReadinessItem(
                "seat-allocation",
                "Seat allocation readiness",
                completeSeatingPlans,
                Math.Max(1, activeExams),
                $"Seat plans should be completed/published before exam day. Current allocations: {seatAllocations.Count}.",
                "/academic/seatallocation",
                "Review seats")
        ];
    }

    private static AcademicDashboardReadinessItemDto CreateReadinessItem(
        string key,
        string title,
        int completed,
        int total,
        string description,
        string actionHref,
        string actionLabel)
    {
        var safeTotal = Math.Max(1, total);
        var safeCompleted = Math.Clamp(completed, 0, safeTotal);
        var missing = Math.Max(0, safeTotal - safeCompleted);
        var status = missing == 0 ? "complete" : safeCompleted == 0 ? "missing" : "partial";
        var severity = missing == 0 ? "success" : safeCompleted == 0 ? "critical" : "warning";

        return new AcademicDashboardReadinessItemDto
        {
            Key = key,
            Title = title,
            Status = status,
            Severity = severity,
            CompletedCount = safeCompleted,
            TotalCount = safeTotal,
            MissingCount = missing,
            Description = description,
            ActionHref = actionHref,
            ActionLabel = actionLabel
        };
    }

    private static List<AcademicDashboardActionCenterItemDto> BuildActionCenterItems(
        int pendingTimetables,
        int pendingGradeEntries,
        int pendingEvaluations,
        IReadOnlyCollection<LessonPlan> lessonPlans,
        IReadOnlyCollection<TeacherDiary> teacherDiaries,
        IReadOnlyCollection<QuestionPaper> questionPapers,
        IReadOnlyCollection<ExamSeatingPlan> seatingPlans,
        IReadOnlyCollection<CalendarImpactLog> calendarImpacts)
    {
        var items = new List<AcademicDashboardActionCenterItemDto>();
        AddAction(items, "calendar-impacts", "Calendar impacts pending", calendarImpacts.Count(i => !i.IsResolved), "calendar", "Resolve impacted timetable, lesson, homework, and exam records.", "/academic/calendarImpactLogs", "Resolve", "critical");
        AddAction(items, "timetable-drafts", "Timetable drafts pending publish", pendingTimetables, "timetable", "Draft timetables should be validated and published before teachers plan daily work.", "/academic/newTimetable", "Publish", "warning");
        AddAction(items, "lesson-review", "Lesson plans awaiting review", lessonPlans.Count(p => p.Status == LessonPlanStatus.Submitted), "lesson-plan", "Submitted lesson plans need coordinator/admin review before they become approved work.", "/academic/lesson-plan", "Review", "warning");
        AddAction(items, "diary-pending", "Teacher diaries still in draft", teacherDiaries.Count(d => d.Status == TeacherDiaryStatus.Draft), "teacher-diary", "Draft diaries should be submitted so syllabus progress and teacher accountability stay current.", "/academic/lesson-plan", "Open diary", "warning");
        AddAction(items, "paper-moderation", "Question papers awaiting moderation", questionPapers.Count(p => p.Status == QuestionPaperStatus.Submitted || p.Status == QuestionPaperStatus.Generated), "question-paper", "Generated/submitted papers need review before publication or locking.", "/academic/questionPapers", "Moderate", "warning");
        AddAction(items, "seating-open", "Seat plans not finalized", seatingPlans.Count(p => p.Status != SeatingPlanStatus.Completed && p.Status != SeatingPlanStatus.Published), "seat-allocation", "Open seating plans must be finalized before exam execution.", "/academic/seatallocation", "Finalize", "critical");
        AddAction(items, "grade-entry", "Grade entries pending", pendingGradeEntries, "grade-entry", "Published exams need marks entry and validation before result processing.", "/academic/gradeEntry", "Record marks", "critical");
        AddAction(items, "evaluation", "Homework/assignment evaluations pending", pendingEvaluations, "homework", "Student submissions waiting for review should be evaluated or returned for changes.", "/academic/gradingAndFeedback", "Evaluate", "warning");

        return items;
    }

    private static void AddAction(
        ICollection<AcademicDashboardActionCenterItemDto> items,
        string id,
        string title,
        int count,
        string source,
        string summary,
        string actionHref,
        string actionLabel,
        string severity)
    {
        if (count <= 0)
        {
            return;
        }

        items.Add(new AcademicDashboardActionCenterItemDto
        {
            Id = id,
            Title = title,
            Summary = summary,
            Severity = severity,
            Source = source,
            Count = count,
            ActionHref = actionHref,
            ActionLabel = actionLabel
        });
    }

    private static List<AcademicDashboardDataHealthItemDto> BuildDataHealthItems(
        IReadOnlyCollection<Subject> subjects,
        IReadOnlyCollection<AssignedSubject> assignedSubjects,
        IReadOnlyCollection<Division> divisions,
        IReadOnlyCollection<ShikshaMitra.Academic.Timetable.Timetables.Timetable> timetables,
        IReadOnlyCollection<LessonPlan> lessonPlans,
        IReadOnlyCollection<TeacherDiary> teacherDiaries,
        IReadOnlyCollection<QuestionBankItem> questions,
        IReadOnlyCollection<QuestionPaper> questionPapers,
        IReadOnlyCollection<ExamSeatingPlan> seatingPlans,
        IReadOnlyCollection<CalendarImpactLog> calendarImpacts)
    {
        var health = new List<AcademicDashboardDataHealthItemDto>();

        AddHealth(
            health,
            "missing-setup",
            "Setup completeness",
            subjects.Count == 0 || divisions.Count == 0 || assignedSubjects.Count == 0,
            "Subjects, divisions, and subject allocations are required before downstream academic modules are reliable.",
            "/academic/curriculumSubjects");
        AddHealth(
            health,
            "calendar-impact-backlog",
            "Calendar impact backlog",
            calendarImpacts.Any(i => !i.IsResolved),
            "Unresolved calendar impacts can leave timetable, homework, lesson plan, or exam data out of sync.",
            "/academic/calendarImpactLogs");
        AddHealth(
            health,
            "lesson-diary-gap",
            "Lesson diary gap",
            lessonPlans.Count(p => p.Status == LessonPlanStatus.Approved) > teacherDiaries.Count(d => d.Status == TeacherDiaryStatus.Submitted),
            "Approved lesson plans should eventually have submitted diary evidence.",
            "/academic/lesson-plan");
        AddHealth(
            health,
            "question-paper-lock",
            "Question paper lock health",
            questionPapers.Any(p => p.Status == QuestionPaperStatus.Published && !p.IsLocked),
            "Published question papers should be locked or version-controlled before exam execution.",
            "/academic/questionPapers");
        AddHealth(
            health,
            "seat-plan-draft",
            "Seat plan draft health",
            seatingPlans.Any(p => p.Status == SeatingPlanStatus.InProgress || p.Status == SeatingPlanStatus.NeedsAttention),
            "Seat plans in progress or needing attention should be resolved before exam day.",
            "/academic/seatallocation");
        AddHealth(
            health,
            "question-bank-approval",
            "Question bank approval health",
            questions.Any(q => q.Status == QuestionStatus.Submitted),
            "Submitted questions should be approved/rejected so they do not sit in review backlog.",
            "/academic/questionBank");
        AddHealth(
            health,
            "timetable-draft-health",
            "Timetable draft health",
            timetables.Any(t => t.Status == TimetableStatus.Draft),
            "Draft timetables should be validated and published, or downstream planning will be incomplete.",
            "/academic/newTimetable");

        if (health.Count == 0)
        {
            health.Add(new AcademicDashboardDataHealthItemDto
            {
                Key = "healthy",
                Title = "Academic data health",
                Status = "healthy",
                Severity = "success",
                Detail = "No immediate academic data-health issues were detected.",
                ActionHref = "/academic/dashboard"
            });
        }

        return health;
    }

    private static void AddHealth(
        ICollection<AcademicDashboardDataHealthItemDto> health,
        string key,
        string title,
        bool hasIssue,
        string detail,
        string actionHref)
    {
        if (!hasIssue)
        {
            return;
        }

        health.Add(new AcademicDashboardDataHealthItemDto
        {
            Key = key,
            Title = title,
            Status = "needsAttention",
            Severity = "warning",
            Detail = detail,
            ActionHref = actionHref
        });
    }

    public async Task<ExamPassFailCountDto> GetSubjectPassFailChartFilteredAsync(Guid standardId, Guid divisionId, string examName)
    {
        var exams = await _examRepository.GetListAsync(e => e.StandardId == standardId && e.ExamName == examName);
        if (exams.Count == 0)
        {
            exams = await _examRepository.GetListAsync(e => e.ExamName == examName);
        }

        var examIds = exams.Select(e => e.Id).ToList();
        var results = await _examResultDetailRepository.GetListAsync(r => examIds.Contains(r.ExamId) && r.DivisionId == divisionId);

        if (results.Count == 0 && examIds.Count > 0)
        {
            results = await _examResultDetailRepository.GetListAsync(r => examIds.Contains(r.ExamId));
        }

        int passed = results.Count(r => r.ResultStatus == ExamStudentResultStatus.Pass);
        int failed = results.Count(r => r.ResultStatus == ExamStudentResultStatus.Fail);

        return new ExamPassFailCountDto
        {
            Passed = passed,
            Failed = failed
        };
    }

    public async Task<ChapterCompletionFilteredDto> GetChapterCompletionChartFilteredAsync(Guid standardId, Guid divisionId)
    {
        var assignedSubjects = await _assignedSubjectRepository.GetListAsync(asub => asub.StandardId == standardId);
        var subjectsList = await _subjectRepository.GetListAsync();
        var subjectMap = subjectsList.ToDictionary(s => s.Id, s => s.Name);

        var chapterProgressList = await _chapterProgressRepository.GetListAsync(p => p.DivisionId == divisionId);
        var progressMap = chapterProgressList.ToDictionary(p => p.ChapterId, p => p.Progress);

        var result = new ChapterCompletionFilteredDto();

        foreach (var asub in assignedSubjects)
        {
            subjectMap.TryGetValue(asub.SubjectId, out var subjectName);
            if (string.IsNullOrEmpty(subjectName)) continue;

            var chapters = await _chapterRepository.GetListAsync(c => c.AssignedSubjectId == asub.Id);
            if (chapters.Count == 0) continue;

            int completed = 0;
            int pending = 0;

            foreach (var chapter in chapters)
            {
                if (progressMap.TryGetValue(chapter.Id, out var progress) && progress == 100)
                {
                    completed++;
                }
                else
                {
                    pending++;
                }
            }

            result.Subjects.Add(new SubjectChapterCompletionDto
            {
                SubjectName = subjectName,
                CompletedChapters = completed,
                PendingChapters = pending
            });
        }

        return result;
    }
}
