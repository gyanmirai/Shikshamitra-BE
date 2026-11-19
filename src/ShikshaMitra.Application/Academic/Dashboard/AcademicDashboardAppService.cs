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
using ShikshaMitra.Security.UserScope;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Dashboard;

[Authorize(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View)]
[RemoteService(false)]
public partial class AcademicDashboardAppService : ApplicationService, IAcademicDashboardAppService
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
    private readonly IUserScopeAccessService _userScopeAccessService;
    private readonly IRepository<ClassTeacherEntity, Guid> _classTeacherRepository;

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
        IRepository<Room, Guid> roomRepository,
        IRepository<ClassTeacherEntity, Guid> classTeacherRepository,
        IUserScopeAccessService userScopeAccessService)
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
        _classTeacherRepository = classTeacherRepository;
        _userScopeAccessService = userScopeAccessService;
    }

    public async Task<AdminAcademicDashboardDto> GetAdminDashboardAsync()
    {
        await EnsureAdminDashboardAccessAsync();

        var studentQuery = await _studentRepository.GetQueryableAsync();
        var studentCountRows = await AsyncExecuter.ToListAsync(studentQuery
            .GroupBy(_ => 1)
            .Select(group => new DashboardStudentCounts(
                group.Count(),
                group.Count(student => student.StandardId.HasValue && student.DivisionId.HasValue))));
        var studentCounts = studentCountRows.SingleOrDefault();
        var studentsCount = studentCounts?.Total ?? 0;
        var totalStudents = studentsCount;

        var totalTeachers = await _employeeRepository.CountAsync(employee =>
            employee.EmployeeGroup == EmployeeGroupType.Teaching);

        var subjectsCount = (int)await _subjectRepository.GetCountAsync();
        var assignedSubjectsCount = subjectsCount;

        var electiveGroupQuery = await _electiveGroupRepository.GetQueryableAsync();
        var electiveGroupStates = await AsyncExecuter.ToListAsync(electiveGroupQuery
            .Select(group => !group.Subjects.Any()));
        var electiveGroupsCount = electiveGroupStates.Count;
        var pendingElectivesCount = electiveGroupStates.Count(isPending => isPending);

        var progressList = await GetDashboardChapterProgressAsync();
        var curriculumCompletion = progressList.Count > 0
            ? (int)Math.Round(progressList.Average(c => c.Progress))
            : 0;

        var timetableQuery = await _timetableRepository.GetQueryableAsync();
        var timetableStatuses = await AsyncExecuter.ToListAsync(timetableQuery.Select(timetable => timetable.Status));
        var divisionQuery = await _divisionRepository.GetQueryableAsync();
        var divisionStandardIds = await AsyncExecuter.ToListAsync(divisionQuery.Select(division => division.StandardId));
        var publishedTimetables = timetableStatuses.Count(status => status == TimetableStatus.Published);
        var timetableRate = divisionStandardIds.Count > 0
            ? (int)Math.Round((double)publishedTimetables / divisionStandardIds.Count * 100)
            : 0;

        var activeTimetables = publishedTimetables;

        var pendingTimetables = timetableStatuses.Count(status => status == TimetableStatus.Draft);

        var conflictsCount = 0;

        var assignedSubjectQuery = await _assignedSubjectRepository.GetQueryableAsync();
        var assignedSubjectStates = await AsyncExecuter.ToListAsync(assignedSubjectQuery
            .Select(assignedSubject => new DashboardAssignedSubjectState(
                assignedSubject.Status,
                assignedSubject.Teachers.Any(teacher => teacher.TeacherType == SubjectTeacherType.Primary))));
        var calendarSettingsCount = await _calendarSettingRepository.CountAsync();
        var calendarImpacts = await _calendarImpactRepository.CountAsync(impact => !impact.IsResolved);
        var lessonPlans = await GetDashboardLessonPlanCountsAsync();
        var teacherDiaries = await GetDashboardDiaryCountsAsync();
        var questions = await GetDashboardQuestionCountsAsync();
        var questionPapers = await GetDashboardPaperCountsAsync();
        var seatingPlans = await GetDashboardSeatingCountsAsync();
        var seatAllocationCount = await _seatAllocationRepository.CountAsync();
        var publishedHomeworkCount = await _homeworkRepository.CountAsync(homework =>
            homework.Status == HomeworkStatus.Published ||
            homework.IsPublished ||
            homework.DivisionSchedules.Any(schedule => schedule.IsPublished));
        var publishedAssignmentCount = await _assignmentRepository.CountAsync(assignment =>
            assignment.Status == AssignmentStatus.Published ||
            assignment.DivisionSchedules.Any(schedule => schedule.IsPublished));
        var roomCount = await _roomRepository.CountAsync();

        var readinessRate = 0;
        if (subjectsCount > 0 && divisionStandardIds.Count > 0)
        {
            var assignedCount = assignedSubjectStates.Count;
            var maxPossible = subjectsCount * divisionStandardIds.Distinct().Count();
            if (maxPossible > 0)
            {
                readinessRate = (int)Math.Round((double)assignedCount / maxPossible * 100);
            }
        }

        var examQuery = await _examRepository.GetQueryableAsync();
        var exams = await AsyncExecuter.ToListAsync(examQuery.Select(exam =>
            new DashboardExamSummary(exam.Id, exam.ExamName, exam.Status, exam.CreationTime)));
        var pendingGradeEntries = exams.Count(exam => exam.Status == ExamStatus.Published);

        var upcomingExams = exams.Count(exam => exam.Status == ExamStatus.Pending);

        var pendingEvaluations = await _submissionRepository.CountAsync(s =>
            s.Status == SubmissionStatus.NeedsReview || s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late);

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
        var submittedLessonPlans = lessonPlans.Where(p => p.Status == LessonPlanStatus.Submitted).Sum(p => p.Count);
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
            new() { Label = "Subjects Creation", Status = subjectsCount > 0 ? "configured" : "missing", Path = "/academic/curriculumSetting", Icon = "solar:book-bold" },
            new() { Label = "Elective Groups", Status = electiveGroupsCount > 0 ? "configured" : "missing", Path = "/academic/curriculumSetting", Icon = "solar:users-group-two-rounded-bold" },
            new() { Label = "Timetable", Status = publishedTimetables > 0 ? "active" : "missing", Path = "/academic/newTimetable", Icon = "solar:calendar-bold" },
            new() { Label = "Weekly Lectures", Status = timetableStatuses.Count > 0 ? "configured" : "missing", Path = "/academic/timetableSettings", Icon = "solar:calendar-date-bold" },
            new() { Label = "Lecture Slots", Status = timetableStatuses.Count > 0 ? "configured" : "missing", Path = "/academic/timetableSettings", Icon = "solar:calendar-date-bold" }
        };

        var operationStatus = new List<AcademicDashboardOperationItemDto>
        {
            new() { Module = "Subject Allocation", Status = assignedSubjectStates.Count > 0 ? $"{readinessRate}% Complete" : "Pending", Level = assignedSubjectStates.Count > 0 ? "success" : "warning", Progress = readinessRate, Icon = "fluent:book-24-regular" },
            new() { Module = "Timetable Setup", Status = $"Pending for {pendingTimetables} Classes", Level = pendingTimetables > 0 ? "warning" : "success", Progress = timetableRate, Icon = "solar:calendar-bold" },
            new() { Module = "Grade Entry", Status = $"{pendingGradeEntries} Pending", Level = pendingGradeEntries > 0 ? "error" : "success", Progress = pendingGradeEntries > 0 ? 0 : 100, Icon = "solar:document-text-bold" },
            new() { Module = "Homework Evaluation", Status = $"{pendingEvaluations} Pending", Level = pendingEvaluations > 0 ? "warning" : "success", Progress = pendingEvaluations > 0 ? 0 : 100, Icon = "solar:notebook-bold" }
        };

        var resultCounts = await GetDashboardResultCountsAsync();
        AcademicDashboardPassFailChartDto passFailChart;
        if (exams.Count > 0)
        {
            var passCounts = new List<int>();
            var failCounts = new List<int>();
            var subjectNames = new List<string>();

            var groupedResults = resultCounts.GroupBy(r => r.ExamId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var exam in exams.Take(6))
            {
                subjectNames.Add(exam.ExamName);
                if (groupedResults.TryGetValue(exam.Id, out var grp))
                {
                    passCounts.Add(grp.Where(r => r.ResultStatus == ExamStudentResultStatus.Pass).Sum(r => r.Count));
                    failCounts.Add(grp.Where(r => r.ResultStatus == ExamStudentResultStatus.Fail).Sum(r => r.Count));
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
            ReadinessItems = BuildReadinessItems(new DashboardReadinessMetrics
            {
                StudentCount = studentsCount,
                MappedStudentCount = studentCounts?.Mapped ?? 0,
                TeacherCount = totalTeachers,
                SubjectCount = subjectsCount,
                AssignedSubjectCount = assignedSubjectStates.Count,
                SubjectTeacherReadyCount = assignedSubjectStates.Count(assigned =>
                    assigned.Status != SubjectStatus.Archived && assigned.HasPrimaryTeacher),
                DivisionCount = divisionStandardIds.Count,
                PublishedTimetableCount = publishedTimetables,
                ActiveExamCount = exams.Count,
                ProgressTrackedCount = progressList.Count(progress => progress.Progress > 0),
                PublishedTaskCount = publishedHomeworkCount + publishedAssignmentCount,
                CalendarSettingsCount = calendarSettingsCount,
                ApprovedQuestionCount = questions.Where(question => question.Status == QuestionStatus.Approved).Sum(question => question.Count),
                PublishedOrLockedPaperCount = questionPapers.Where(paper =>
                    paper.Status == QuestionPaperStatus.Published ||
                    paper.Status == QuestionPaperStatus.Locked ||
                    paper.IsLocked).Sum(paper => paper.Count),
                SubmittedDiaryCount = teacherDiaries.Where(diary => diary.Status == TeacherDiaryStatus.Submitted).Sum(diary => diary.Count),
                ActiveLessonPlanCount = lessonPlans.Where(plan =>
                    plan.Status == LessonPlanStatus.Submitted ||
                    plan.Status == LessonPlanStatus.Approved ||
                    plan.Status == LessonPlanStatus.NeedsChanges).Sum(plan => plan.Count),
                ResultReadyExamCount = resultCounts.Select(examResult => examResult.ExamId).Distinct().Count(),
                RoomCount = roomCount,
                CompleteSeatingPlanCount = seatingPlans.Where(plan =>
                    plan.Status == SeatingPlanStatus.Completed ||
                    plan.Status == SeatingPlanStatus.Published ||
                    plan.IsLocked).Sum(plan => plan.Count),
                SeatAllocationCount = seatAllocationCount
            }),
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
                subjectsCount == 0 || divisionStandardIds.Count == 0 || assignedSubjectStates.Count == 0,
                timetableStatuses.Any(status => status == TimetableStatus.Draft),
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

    public Task<PrincipalAcademicDashboardDto> GetPrincipalDashboardAsync() => BuildPrincipalDashboardAsync(includeAnalytics: true);

    public Task<PrincipalAcademicDashboardDto> GetPrincipalOverviewAsync() => BuildPrincipalDashboardAsync(includeAnalytics: false);

    private async Task<PrincipalAcademicDashboardDto> BuildPrincipalDashboardAsync(
        bool includeAnalytics, bool includePerformance = true)
    {
        var studentsCount = (int)await _studentRepository.GetCountAsync();
        var totalStudents = studentsCount;

        var employees = await _employeeRepository.GetListAsync(employee => employee.EmployeeGroup == EmployeeGroupType.Teaching);
        var totalTeachers = employees.Count(e => e.EmployeeGroup == EmployeeGroupType.Teaching);

        var subjects = await _subjectRepository.GetListAsync();

        var progressList = await GetDashboardChapterProgressAsync();
        var curriculumCompletion = progressList.Count > 0
            ? (int)Math.Round(progressList.Average(c => c.Progress))
            : 0;

        var exams = await _examRepository.GetListAsync();

        var results = includePerformance ? await GetDashboardResultDetailsAsync() : [];
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

        var mathProgress = 0;
        var mathTarget = 0;

        var scienceProgress = 0;
        var scienceTarget = 0;

        var englishProgress = 0;
        var englishTarget = 0;

        var upcomingExamsCount = exams.Count(e => e.Status == ExamStatus.Pending);

        var insights = new List<AcademicDashboardInsightDto>();
        var pendingEvaluations = await _submissionRepository.CountAsync(s =>
            s.Status == SubmissionStatus.NeedsReview || s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late);
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
        var divisions = await _divisionRepository.GetListAsync();
        var students = await _studentRepository.GetListAsync();
        var principalAssignedSubjects = await _assignedSubjectRepository.GetListAsync();
        var principalDivisions = divisions;
        var principalTimetables = await _timetableRepository.GetListAsync();
        var principalCalendarSettingsCount = await _calendarSettingRepository.CountAsync();
        var principalCalendarImpacts = await _calendarImpactRepository.CountAsync(impact => !impact.IsResolved);
        var principalLessonPlans = await GetDashboardLessonPlanCountsAsync();
        var principalTeacherDiaries = await GetDashboardDiaryCountsAsync();
        var principalQuestions = await GetDashboardQuestionCountsAsync();
        var principalQuestionPapers = await GetDashboardPaperCountsAsync();
        var principalSeatingPlans = await GetDashboardSeatingCountsAsync();
        var principalSeatAllocationCount = await _seatAllocationRepository.CountAsync();
        var principalHomeworkAssignments = await _homeworkRepository.GetListAsync();
        var principalLearningAssignments = await _assignmentRepository.GetListAsync();
        var principalRoomCount = await _roomRepository.CountAsync();
        var principalPendingEvaluations = pendingEvaluations;
        var principalPendingTimetables = principalTimetables.Count(t => t.Status == TimetableStatus.Draft);
        var principalPendingGradeEntries = exams.Count(e => e.Status == ExamStatus.Published);

        var resultReadyExamCount = includePerformance
            ? results.Select(result => result.ExamId).Distinct().Count()
            : (await GetDashboardResultCountsAsync()).Select(result => result.ExamId).Distinct().Count();
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
            principalCalendarSettingsCount,
            principalQuestions,
            principalQuestionPapers,
            principalSeatingPlans,
            principalSeatAllocationCount,
            resultReadyExamCount,
            principalRoomCount);
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
        };

        if (includeAnalytics)
        {
            var analytics = await GetPrincipalAnalyticsAsync();
            result.ChapterCompletionChart = analytics.ChapterCompletionChart;
            result.SubjectPassFailChart = analytics.SubjectPassFailChart;
            result.TeacherPerformanceComparison = analytics.TeacherPerformanceComparison;
            result.UpcomingExamsList = analytics.UpcomingExamsList;
            result.TopPerformers = analytics.TopPerformers;
            result.BottomPerformers = analytics.BottomPerformers;
        }

        return result;
    }

    public Task<TeacherAcademicDashboardDto> GetTeacherDashboardAsync() => BuildTeacherDashboardAsync(includeAnalytics: true);

    public Task<TeacherAcademicDashboardDto> GetTeacherOverviewAsync() => BuildTeacherDashboardAsync(includeAnalytics: false);

    private async Task<TeacherAcademicDashboardDto> BuildTeacherDashboardAsync(
        bool includeAnalytics, bool includeAttention = true)
    {
        var currentUserId = CurrentUser.Id;
        var employeesList = await _employeeRepository.GetListAsync(employee =>
            employee.EmployeeGroup == EmployeeGroupType.Teaching ||
            (currentUserId.HasValue && employee.UserId == currentUserId.Value));
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

        var homeworks = hasTeacherScope
            ? await _homeworkRepository.GetListAsync(h => teacherStandardIds.Contains(h.StandardId) && teacherSubjectIds.Contains(h.SubjectId))
            : [];

        var teacherLearningAssignments = hasTeacherScope
            ? await _assignmentRepository.GetListAsync(a => teacherStandardIds.Contains(a.StandardId) && teacherSubjectIds.Contains(a.SubjectId))
            : [];

        var pendingHomework = homeworks.Count(h => h.Status == HomeworkStatus.Draft || h.Status == HomeworkStatus.Published);

        var exams = hasTeacherScope
            ? await _examRepository.GetListAsync(e => teacherStandardIds.Contains(e.StandardId))
            : [];

        var students = await _studentRepository.GetListAsync();
        var divisions = await _divisionRepository.GetListAsync();
        var teacherDivisions = hasTeacherScope
            ? divisions.Where(d => teacherStandardIds.Contains(d.StandardId)).ToList()
            : [];
        var teacherDivisionIds = teacherDivisions.Select(d => d.Id).Distinct().ToList();
        var results = includeAttention && hasTeacherScope && teacherDivisionIds.Count > 0
            ? await GetDashboardResultDetailsAsync(divisionIds: teacherDivisionIds)
            : [];

        var homeworkIds = homeworks.Select(homework => homework.Id).ToList();
        var submissionCounts = new Dictionary<SubmissionStatus, int>();
        if (hasTeacherScope)
        {
            var submissionQuery = await _submissionRepository.GetQueryableAsync();
            var counts = await AsyncExecuter.ToListAsync(submissionQuery.Where(s =>
                    teacherDivisionIds.Contains(s.DivisionId) &&
                    (!s.HomeworkAssignmentId.HasValue || homeworkIds.Contains(s.HomeworkAssignmentId.Value)))
                .GroupBy(submission => submission.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() }));
            submissionCounts = counts.ToDictionary(group => group.Status, group => group.Count);
        }
        var totalSubmissions = submissionCounts.Values.Sum();
        var pendingEvaluations = submissionCounts.GetValueOrDefault(SubmissionStatus.NeedsReview) +
            submissionCounts.GetValueOrDefault(SubmissionStatus.Submitted) + submissionCounts.GetValueOrDefault(SubmissionStatus.Late);
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
                    Attendance = null,
                    Performance = (int)r.Percentage,
                    Warning = "Weak performance in recent exams",
                    AvatarUrl = string.Empty
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

        var completedHW = submissionCounts.GetValueOrDefault(SubmissionStatus.Reviewed);
        var pendingHW = submissionCounts.GetValueOrDefault(SubmissionStatus.NeedsReview) + submissionCounts.GetValueOrDefault(SubmissionStatus.Submitted);
        var homeworkStatus = new AcademicDashboardHomeworkStatusDto
        {
            Completed = completedHW,
            Pending = pendingHW,
            Graded = completedHW
        };

        var resultReadyExamCount = includeAttention
            ? results.Select(result => result.ExamId).Distinct().Count()
            : teacherDivisionIds.Count == 0 ? 0
                : (await GetDashboardResultCountsAsync(teacherDivisionIds))
                    .Select(result => result.ExamId).Distinct().Count();

        var progressList = await GetDashboardChapterProgressAsync();

        var upcomingExamsList = new List<AcademicDashboardExamDto>();
        var teacherSubjects = hasTeacherScope
            ? await _subjectRepository.GetListAsync(subject => teacherSubjectIds.Contains(subject.Id))
            : [];

        var teacherTimetables = hasTeacherScope
            ? await _timetableRepository.GetListAsync(timetable => teacherDivisionIds.Contains(timetable.DivisionId))
            : [];

        var teacherCalendarSettingsCount = await _calendarSettingRepository.CountAsync();
        var teacherCalendarImpacts = await _calendarImpactRepository.CountAsync(impact => !impact.IsResolved);
        var teacherLessonPlans = teacherUser == null && hasTeacherScope
            ? [] : await GetDashboardLessonPlanCountsAsync(teacherUser?.Id);
        var teacherDiaries = teacherUser == null && hasTeacherScope
            ? [] : await GetDashboardDiaryCountsAsync(teacherUser?.Id);

        var teacherQuestions = hasTeacherScope
            ? await GetDashboardQuestionCountsAsync(teacherStandardIds, teacherSubjectIds)
            : [];

        var teacherQuestionPapers = hasTeacherScope
            ? await GetDashboardPaperCountsAsync(teacherStandardIds, teacherSubjectIds)
            : [];

        var teacherSeatingPlans = await GetDashboardSeatingCountsAsync();
        var teacherSeatAllocationCount = await _seatAllocationRepository.CountAsync();
        var teacherRoomCount = await _roomRepository.CountAsync();
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
            teacherCalendarSettingsCount,
            teacherQuestions,
            teacherQuestionPapers,
            teacherSeatingPlans,
            teacherSeatAllocationCount,
            resultReadyExamCount,
            teacherRoomCount);
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
            EvaluationsRate = totalSubmissions > 0
                ? (int)Math.Round((double)completedHW / totalSubmissions * 100)
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
            UpcomingExamsList = upcomingExamsList
        };

        if (includeAnalytics)
        {
            var analytics = await GetTeacherAnalyticsAsync();
            result.PerformanceDistribution = analytics.PerformanceDistribution;
            result.ChapterProgressChart = analytics.ChapterProgressChart;
        }

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
        IReadOnlyCollection<DashboardChapterProgress> progressList,
        IReadOnlyCollection<HomeworkAssignmentEntity> homeworks,
        IReadOnlyCollection<AssignmentEntity> assignments,
        IReadOnlyCollection<DashboardLessonPlanCount> lessonPlans,
        IReadOnlyCollection<DashboardDiaryCount> teacherDiaries,
        int calendarSettingsCount,
        IReadOnlyCollection<DashboardQuestionCount> questions,
        IReadOnlyCollection<DashboardPaperCount> questionPapers,
        IReadOnlyCollection<DashboardSeatingCount> seatingPlans,
        int seatAllocationCount,
        int resultReadyExams,
        int roomCount)
    {
        return BuildReadinessItems(new DashboardReadinessMetrics
        {
            StudentCount = students.Count,
            MappedStudentCount = students.Count(student => student.StandardId.HasValue && student.DivisionId.HasValue),
            TeacherCount = teachers.Count,
            SubjectCount = subjects.Count,
            AssignedSubjectCount = assignedSubjects.Count,
            SubjectTeacherReadyCount = assignedSubjects.Count(assignedSubject =>
                assignedSubject.Status != SubjectStatus.Archived &&
                assignedSubject.Teachers.Any(teacher => teacher.TeacherType == SubjectTeacherType.Primary)),
            DivisionCount = divisions.Count,
            PublishedTimetableCount = timetables.Count(timetable => timetable.Status == TimetableStatus.Published),
            ActiveExamCount = exams.Count,
            ProgressTrackedCount = progressList.Count(progress => progress.Progress > 0),
            PublishedTaskCount = homeworks.Count(homework =>
                homework.Status == HomeworkStatus.Published ||
                homework.IsPublished ||
                homework.DivisionSchedules.Any(schedule => schedule.IsPublished)) + assignments.Count(assignment =>
                assignment.Status == AssignmentStatus.Published ||
                assignment.DivisionSchedules.Any(schedule => schedule.IsPublished)),
            CalendarSettingsCount = calendarSettingsCount,
            ApprovedQuestionCount = questions.Where(question => question.Status == QuestionStatus.Approved).Sum(question => question.Count),
            PublishedOrLockedPaperCount = questionPapers.Where(paper =>
                paper.Status == QuestionPaperStatus.Published ||
                paper.Status == QuestionPaperStatus.Locked ||
                paper.IsLocked).Sum(paper => paper.Count),
            SubmittedDiaryCount = teacherDiaries.Where(diary => diary.Status == TeacherDiaryStatus.Submitted).Sum(diary => diary.Count),
            ActiveLessonPlanCount = lessonPlans.Where(plan =>
                plan.Status == LessonPlanStatus.Submitted ||
                plan.Status == LessonPlanStatus.Approved ||
                plan.Status == LessonPlanStatus.NeedsChanges).Sum(plan => plan.Count),
            ResultReadyExamCount = resultReadyExams,
            RoomCount = roomCount,
            CompleteSeatingPlanCount = seatingPlans.Where(plan =>
                plan.Status == SeatingPlanStatus.Completed ||
                plan.Status == SeatingPlanStatus.Published ||
                plan.IsLocked).Sum(plan => plan.Count),
            SeatAllocationCount = seatAllocationCount
        });
    }

    private static List<AcademicDashboardReadinessItemDto> BuildReadinessItems(
        DashboardReadinessMetrics metrics)
    {
        return
        [
            CreateReadinessItem(
                "student-academic-mapping",
                "Student academic mapping",
                metrics.MappedStudentCount,
                Math.Max(1, metrics.StudentCount),
                "Every active student should have class and division mapping before homework, exam, result, and 360 views are trusted.",
                "/student/studentDetails",
                "Review students"),
            CreateReadinessItem(
                "teacher-allocation",
                "Teacher allocation",
                metrics.SubjectTeacherReadyCount,
                Math.Max(1, metrics.AssignedSubjectCount),
                $"Each allocated subject needs one primary teacher. Teaching staff available: {metrics.TeacherCount}.",
                "/academic/curriculumSubjects",
                "Map teachers"),
            CreateReadinessItem(
                "calendar-rules",
                "Calendar rules",
                metrics.CalendarSettingsCount,
                1,
                "Configure school working-day rules before scheduling timetable, homework, lesson plans, and exams.",
                "/academic/calendarSettings",
                "Open settings"),
            CreateReadinessItem(
                "subject-allocation",
                "Subject allocation",
                metrics.AssignedSubjectCount,
                Math.Max(1, metrics.DivisionCount),
                "Each class/division should have required subjects and teachers mapped before daily academic work starts.",
                "/academic/curriculumSubjects",
                "Review allocation"),
            CreateReadinessItem(
                "syllabus-progress",
                "Syllabus tracker",
                metrics.ProgressTrackedCount,
                Math.Max(1, metrics.AssignedSubjectCount),
                "Chapter progress must be tracked from diary/evidence instead of only static syllabus setup.",
                "/academic/courseSubjects",
                "Open syllabus"),
            CreateReadinessItem(
                "timetable-publication",
                "Published timetable",
                metrics.PublishedTimetableCount,
                Math.Max(1, metrics.DivisionCount),
                "Published timetables are needed before lesson plans, substitutions, and daily teacher workbench are reliable.",
                "/academic/newTimetable",
                "Open timetable"),
            CreateReadinessItem(
                "lesson-diary-readiness",
                "Lesson diary evidence",
                metrics.SubmittedDiaryCount,
                Math.Max(1, metrics.ActiveLessonPlanCount),
                "Approved/submitted lesson plans should have diary evidence so syllabus progress and teacher accountability are reliable.",
                "/academic/lesson-plan",
                "Open diary"),
            CreateReadinessItem(
                "homework-assignment-readiness",
                "Homework and assignment publishing",
                metrics.PublishedTaskCount,
                Math.Max(1, metrics.SubjectCount),
                "At least one published homework or assignment per active subject helps keep the evaluation workflow visible.",
                "/academic/homeworkAssignment",
                "Review tasks"),
            CreateReadinessItem(
                "question-bank",
                "Question bank governance",
                metrics.ApprovedQuestionCount,
                Math.Max(1, metrics.SubjectCount),
                "Approved question bank items are needed before secure paper generation can be trusted.",
                "/academic/questionBank",
                "Review bank"),
            CreateReadinessItem(
                "question-paper",
                "Question paper readiness",
                metrics.PublishedOrLockedPaperCount,
                Math.Max(1, metrics.ActiveExamCount),
                "Published or locked papers should exist for exams before exam execution starts.",
                "/academic/questionPapers",
                "Review papers"),
            CreateReadinessItem(
                "result-readiness",
                "Result readiness",
                metrics.ResultReadyExamCount,
                Math.Max(1, metrics.ActiveExamCount),
                "Exam result details should exist before result reports, dashboards, and Student 360 performance panels are trusted.",
                "/academic/resultCreation",
                "Review results"),
            CreateReadinessItem(
                "room-readiness",
                "Room setup",
                metrics.RoomCount,
                Math.Max(1, metrics.DivisionCount),
                "Rooms must be configured before timetable room assignment, substitutions, and exam seating can be reliable.",
                "/academic/roomAvailability",
                "Review rooms"),
            CreateReadinessItem(
                "seat-allocation",
                "Seat allocation readiness",
                metrics.CompleteSeatingPlanCount,
                Math.Max(1, metrics.ActiveExamCount),
                $"Seat plans should be completed/published before exam day. Current allocations: {metrics.SeatAllocationCount}.",
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
        IReadOnlyCollection<DashboardLessonPlanCount> lessonPlans,
        IReadOnlyCollection<DashboardDiaryCount> teacherDiaries,
        IReadOnlyCollection<DashboardPaperCount> questionPapers,
        IReadOnlyCollection<DashboardSeatingCount> seatingPlans,
        int unresolvedCalendarImpacts)
    {
        var items = new List<AcademicDashboardActionCenterItemDto>();
        AddAction(items, "calendar-impacts", "Calendar impacts pending", unresolvedCalendarImpacts, "calendar", "Resolve impacted timetable, lesson, homework, and exam records.", "/academic/calendarImpactLogs", "Resolve", "critical");
        AddAction(items, "timetable-drafts", "Timetable drafts pending publish", pendingTimetables, "timetable", "Draft timetables should be validated and published before teachers plan daily work.", "/academic/newTimetable", "Publish", "warning");
        AddAction(items, "lesson-review", "Lesson plans awaiting review", lessonPlans.Where(p => p.Status == LessonPlanStatus.Submitted).Sum(p => p.Count), "lesson-plan", "Submitted lesson plans need coordinator/admin review before they become approved work.", "/academic/lesson-plan", "Review", "warning");
        AddAction(items, "diary-pending", "Teacher diaries still in draft", teacherDiaries.Where(d => d.Status == TeacherDiaryStatus.Draft).Sum(d => d.Count), "teacher-diary", "Draft diaries should be submitted so syllabus progress and teacher accountability stay current.", "/academic/lesson-plan", "Open diary", "warning");
        AddAction(items, "paper-moderation", "Question papers awaiting moderation", questionPapers.Where(p => p.Status == QuestionPaperStatus.Submitted || p.Status == QuestionPaperStatus.Generated).Sum(p => p.Count), "question-paper", "Generated/submitted papers need review before publication or locking.", "/academic/questionPapers", "Moderate", "warning");
        AddAction(items, "seating-open", "Seat plans not finalized", seatingPlans.Where(p => p.Status != SeatingPlanStatus.Completed && p.Status != SeatingPlanStatus.Published).Sum(p => p.Count), "seat-allocation", "Open seating plans must be finalized before exam execution.", "/academic/seatallocation", "Finalize", "critical");
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
        IReadOnlyCollection<DashboardLessonPlanCount> lessonPlans,
        IReadOnlyCollection<DashboardDiaryCount> teacherDiaries,
        IReadOnlyCollection<DashboardQuestionCount> questions,
        IReadOnlyCollection<DashboardPaperCount> questionPapers,
        IReadOnlyCollection<DashboardSeatingCount> seatingPlans,
        int unresolvedCalendarImpacts)
    {
        return BuildDataHealthItems(
            subjects.Count == 0 || divisions.Count == 0 || assignedSubjects.Count == 0,
            timetables.Any(timetable => timetable.Status == TimetableStatus.Draft),
            lessonPlans,
            teacherDiaries,
            questions,
            questionPapers,
            seatingPlans,
            unresolvedCalendarImpacts);
    }

    private static List<AcademicDashboardDataHealthItemDto> BuildDataHealthItems(
        bool hasMissingSetup,
        bool hasDraftTimetables,
        IReadOnlyCollection<DashboardLessonPlanCount> lessonPlans,
        IReadOnlyCollection<DashboardDiaryCount> teacherDiaries,
        IReadOnlyCollection<DashboardQuestionCount> questions,
        IReadOnlyCollection<DashboardPaperCount> questionPapers,
        IReadOnlyCollection<DashboardSeatingCount> seatingPlans,
        int unresolvedCalendarImpacts)
    {
        var health = new List<AcademicDashboardDataHealthItemDto>();

        AddHealth(
            health,
            "missing-setup",
            "Setup completeness",
            hasMissingSetup,
            "Subjects, divisions, and subject allocations are required before downstream academic modules are reliable.",
            "/academic/curriculumSubjects");
        AddHealth(
            health,
            "calendar-impact-backlog",
            "Calendar impact backlog",
            unresolvedCalendarImpacts > 0,
            "Unresolved calendar impacts can leave timetable, homework, lesson plan, or exam data out of sync.",
            "/academic/calendarImpactLogs");
        AddHealth(
            health,
            "lesson-diary-gap",
            "Lesson diary gap",
            lessonPlans.Where(p => p.Status == LessonPlanStatus.Approved).Sum(p => p.Count) > teacherDiaries.Where(d => d.Status == TeacherDiaryStatus.Submitted).Sum(d => d.Count),
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
            hasDraftTimetables,
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
        var results = await GetDashboardResultDetailsAsync(examIds, [divisionId]);

        if (results.Count == 0 && examIds.Count > 0)
        {
            results = await GetDashboardResultDetailsAsync(examIds);
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
        var subjectIds = assignedSubjects.Select(assignment => assignment.SubjectId).Distinct().ToList();
        var subjectsList = await _subjectRepository.GetListAsync(subject => subjectIds.Contains(subject.Id));
        var subjectMap = subjectsList.ToDictionary(s => s.Id, s => s.Name);

        var chapterProgressList = await _chapterProgressRepository.GetListAsync(p => p.DivisionId == divisionId);
        var progressMap = chapterProgressList.ToDictionary(p => p.ChapterId, p => p.Progress);

        var result = new ChapterCompletionFilteredDto();
        var assignmentIds = assignedSubjects.Select(assignment => assignment.Id).ToList();
        var chapterQuery = await _chapterRepository.GetQueryableAsync();
        var chapters = await AsyncExecuter.ToListAsync(chapterQuery
            .Where(chapter => assignmentIds.Contains(chapter.AssignedSubjectId))
            .Select(chapter => new { chapter.Id, chapter.AssignedSubjectId }));
        var chaptersByAssignment = chapters.ToLookup(chapter => chapter.AssignedSubjectId);

        foreach (var asub in assignedSubjects)
        {
            subjectMap.TryGetValue(asub.SubjectId, out var subjectName);
            if (string.IsNullOrEmpty(subjectName)) continue;

            var subjectChapters = chaptersByAssignment[asub.Id];
            if (!subjectChapters.Any()) continue;

            int completed = 0;
            int pending = 0;

            foreach (var chapter in subjectChapters)
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

    private async Task<List<DashboardChapterProgress>> GetDashboardChapterProgressAsync()
    {
        var query = await GetTenantDashboardProgressQueryAsync();
        return await AsyncExecuter.ToListAsync(query.Select(progress =>
            new DashboardChapterProgress(progress.ChapterId, progress.DivisionId, progress.Progress)));
    }

    private async Task<List<DashboardLessonPlanCount>> GetDashboardLessonPlanCountsAsync(Guid? teacherId = null)
    {
        var query = await _lessonPlanRepository.GetQueryableAsync();
        if (teacherId.HasValue) query = query.Where(plan => plan.TeacherId == teacherId.Value);
        return await AsyncExecuter.ToListAsync(query.GroupBy(plan => plan.Status)
            .Select(group => new DashboardLessonPlanCount(group.Key, group.Count())));
    }

    private async Task<List<DashboardDiaryCount>> GetDashboardDiaryCountsAsync(Guid? teacherId = null)
    {
        var query = await _teacherDiaryRepository.GetQueryableAsync();
        if (teacherId.HasValue) query = query.Where(diary => diary.TeacherId == teacherId.Value);
        return await AsyncExecuter.ToListAsync(query.GroupBy(diary => diary.Status)
            .Select(group => new DashboardDiaryCount(group.Key, group.Count())));
    }

    private async Task<List<DashboardQuestionCount>> GetDashboardQuestionCountsAsync(
        IReadOnlyCollection<Guid>? standardIds = null, IReadOnlyCollection<Guid>? subjectIds = null)
    {
        var query = await _questionBankRepository.GetQueryableAsync();
        if (standardIds != null) query = query.Where(question => standardIds.Contains(question.StandardId));
        if (subjectIds != null) query = query.Where(question => subjectIds.Contains(question.SubjectId));
        return await AsyncExecuter.ToListAsync(query.GroupBy(question => question.Status)
            .Select(group => new DashboardQuestionCount(group.Key, group.Count())));
    }

    private async Task<List<DashboardPaperCount>> GetDashboardPaperCountsAsync(
        IReadOnlyCollection<Guid>? standardIds = null, IReadOnlyCollection<Guid>? subjectIds = null)
    {
        var query = await _questionPaperRepository.GetQueryableAsync();
        if (standardIds != null) query = query.Where(paper => standardIds.Contains(paper.StandardId));
        if (subjectIds != null) query = query.Where(paper => subjectIds.Contains(paper.SubjectId));
        return await AsyncExecuter.ToListAsync(query.GroupBy(paper => new { paper.Status, paper.IsLocked })
            .Select(group => new DashboardPaperCount(group.Key.Status, group.Key.IsLocked, group.Count())));
    }

    private async Task<List<DashboardSeatingCount>> GetDashboardSeatingCountsAsync()
    {
        var query = await _seatingPlanRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(query.GroupBy(plan => new { plan.Status, plan.IsLocked })
            .Select(group => new DashboardSeatingCount(group.Key.Status, group.Key.IsLocked, group.Count())));
    }

    private async Task<List<DashboardExamResultCount>> GetDashboardResultCountsAsync(
        IReadOnlyCollection<Guid>? divisionIds = null)
    {
        var query = await _examResultDetailRepository.GetQueryableAsync();
        if (divisionIds is { Count: > 0 })
        {
            query = query.Where(result => divisionIds.Contains(result.DivisionId));
        }
        return await AsyncExecuter.ToListAsync(query.GroupBy(result => new { result.ExamId, result.ResultStatus })
            .Select(group => new DashboardExamResultCount(group.Key.ExamId, group.Key.ResultStatus, group.Count())));
    }

    private async Task<List<DashboardResultDetail>> GetDashboardResultDetailsAsync(
        IReadOnlyCollection<Guid>? examIds = null,
        IReadOnlyCollection<Guid>? divisionIds = null)
    {
        var query = await _examResultDetailRepository.GetQueryableAsync();

        if (examIds is { Count: > 0 })
        {
            query = query.Where(result => examIds.Contains(result.ExamId));
        }

        if (divisionIds is { Count: > 0 })
        {
            query = query.Where(result => divisionIds.Contains(result.DivisionId));
        }

        return await AsyncExecuter.ToListAsync(
            query.Select(result => new DashboardResultDetail(
                result.Id,
                result.ExamId,
                result.StudentId,
                result.DivisionId,
                result.Percentage,
                result.ResultStatus)));
    }

    private sealed record DashboardResultDetail(
        Guid Id,
        Guid ExamId,
        Guid StudentId,
        Guid DivisionId,
        double Percentage,
        ExamStudentResultStatus ResultStatus);

    private sealed class DashboardReadinessMetrics
    {
        public int StudentCount { get; init; }
        public int MappedStudentCount { get; init; }
        public int TeacherCount { get; init; }
        public int SubjectCount { get; init; }
        public int AssignedSubjectCount { get; init; }
        public int SubjectTeacherReadyCount { get; init; }
        public int DivisionCount { get; init; }
        public int PublishedTimetableCount { get; init; }
        public int ActiveExamCount { get; init; }
        public int ProgressTrackedCount { get; init; }
        public int PublishedTaskCount { get; init; }
        public int CalendarSettingsCount { get; init; }
        public int ApprovedQuestionCount { get; init; }
        public int PublishedOrLockedPaperCount { get; init; }
        public int SubmittedDiaryCount { get; init; }
        public int ActiveLessonPlanCount { get; init; }
        public int ResultReadyExamCount { get; init; }
        public int RoomCount { get; init; }
        public int CompleteSeatingPlanCount { get; init; }
        public int SeatAllocationCount { get; init; }
    }

    private sealed record DashboardStudentCounts(int Total, int Mapped);
    private sealed record DashboardAssignedSubjectState(SubjectStatus Status, bool HasPrimaryTeacher);
    private sealed record DashboardExamSummary(Guid Id, string ExamName, ExamStatus Status, DateTime CreationTime);
    private sealed record DashboardChapterProgress(Guid ChapterId, Guid DivisionId, int Progress);
    private sealed record DashboardExamResultCount(Guid ExamId, ExamStudentResultStatus ResultStatus, int Count);
    private sealed record DashboardLessonPlanCount(LessonPlanStatus Status, int Count);
    private sealed record DashboardDiaryCount(TeacherDiaryStatus Status, int Count);
    private sealed record DashboardQuestionCount(QuestionStatus Status, int Count);
    private sealed record DashboardPaperCount(QuestionPaperStatus Status, bool IsLocked, int Count);
    private sealed record DashboardSeatingCount(SeatingPlanStatus Status, bool IsLocked, int Count);
}
