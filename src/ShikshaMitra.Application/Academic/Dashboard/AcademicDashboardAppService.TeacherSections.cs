using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    private async Task<List<AssignedSubject>> GetCurrentTeacherAssignmentsAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return [];
        }

        var employees = await _employeeRepository.GetListAsync(employee =>
            employee.UserId == CurrentUser.Id.Value);
        var teacher = employees.FirstOrDefault();
        return teacher == null ? [] : await _assignedSubjectRepository.GetListAsync(assignment =>
            assignment.Teachers.Any(item => item.TeacherId == teacher.Id));
    }

    public async Task<TeacherAcademicDashboardAssignmentsDto> GetTeacherAssignmentsAsync()
    {
        var assignments = await GetCurrentTeacherAssignmentsAsync();
        var result = new TeacherAcademicDashboardAssignmentsDto
        {
            AssignedClassesCount = assignments.Select(item => item.StandardId).Distinct().Count(),
            AssignedSubjectsCount = assignments.Select(item => item.SubjectId).Distinct().Count()
        };
        if (assignments.Count == 0)
        {
            return result;
        }

        var standardIds = assignments.Select(item => item.StandardId).Distinct().ToList();
        var subjectIds = assignments.Select(item => item.SubjectId).Distinct().ToList();
        var homeworks = await _homeworkRepository.GetListAsync(homework =>
            standardIds.Contains(homework.StandardId) && subjectIds.Contains(homework.SubjectId));
        result.PendingHomework = homeworks.Count(homework =>
            homework.Status == HomeworkStatus.Draft || homework.Status == HomeworkStatus.Published);
        return result;
    }

    public async Task<TeacherAcademicDashboardAssessmentDto> GetTeacherAssessmentAsync()
    {
        var overview = await ReadDashboardSectionAggregatesAsync(teacherScope: true);
        var pendingTasks = new List<AcademicDashboardPendingTaskDto>();
        if (overview.PendingEvaluations > 0)
        {
            pendingTasks.Add(new() { Id = "p2", Title = "Homework Reviews Pending", Count = overview.PendingEvaluations, Priority = "high", Action = "Review", Icon = "solar:notebook-bold", Path = "/academic/gradingAndFeedback" });
        }
        if (overview.PendingGradeEntries > 0)
        {
            pendingTasks.Add(new() { Id = "p3", Title = "Grade Entries Pending", Count = overview.PendingGradeEntries, Priority = "medium", Action = "Enter", Icon = "solar:document-text-bold", Path = "/academic/gradeEntry" });
        }
        return new TeacherAcademicDashboardAssessmentDto
        {
            SystemAlertsCount = overview.ActionCenterItems.Count,
            EvaluationsRate = overview.TotalSubmissions > 0
                ? (int)Math.Round((double)overview.ReviewedSubmissions / overview.TotalSubmissions * 100) : 0,
            PendingEvaluations = overview.PendingEvaluations,
            UpcomingExams = overview.UpcomingExams,
            ReadinessItems = overview.ReadinessItems,
            ActionCenterItems = overview.ActionCenterItems,
            DataHealthItems = overview.DataHealthItems,
            PendingTasks = pendingTasks,
            HomeworkStatus = new AcademicDashboardHomeworkStatusDto
            {
                Completed = overview.ReviewedSubmissions,
                Graded = overview.ReviewedSubmissions,
                Pending = overview.PendingSubmissions
            }
        };
    }

    public async Task<TeacherAcademicDashboardAttentionDto> GetTeacherAttentionAsync()
    {
        var result = new TeacherAcademicDashboardAttentionDto();
        var assignments = await GetCurrentTeacherAssignmentsAsync();
        if (assignments.Count == 0)
        {
            return result;
        }

        var standardIds = assignments.Select(item => item.StandardId).Distinct().ToList();
        var divisions = await _divisionRepository.GetListAsync(division =>
            standardIds.Contains(division.StandardId));
        var divisionIds = divisions.Select(item => item.Id).ToList();
        if (divisionIds.Count == 0)
        {
            return result;
        }

        var resultQuery = await _examResultDetailRepository.GetQueryableAsync();
        var weakResults = await AsyncExecuter.ToListAsync(resultQuery
            .Where(item => divisionIds.Contains(item.DivisionId) && item.Percentage < 50)
            .Take(5)
            .Select(item => new { item.StudentId, item.DivisionId, item.Percentage }));
        var studentIds = weakResults.Select(item => item.StudentId).Distinct().ToList();
        var students = studentIds.Count == 0 ? [] : await _studentRepository.GetListAsync(student =>
            studentIds.Contains(student.Id));
        var names = students.ToDictionary(student => student.Id,
            student => $"{student.FirstName} {student.LastName}".Trim());
        var divisionNames = divisions.ToDictionary(division => division.Id, division => division.Name);

        result.StudentsAttention = weakResults.Select(item => new AcademicDashboardAttentionStudentDto
        {
            Id = item.StudentId.ToString(),
            Name = names.GetValueOrDefault(item.StudentId, string.Empty),
            ClassDivision = divisionNames.GetValueOrDefault(item.DivisionId, "Various"),
            Attendance = null,
            Performance = (int)item.Percentage,
            Warning = "Weak performance in recent exams",
            AvatarUrl = string.Empty
        }).ToList();
        result.StudentsAttentionCount = result.StudentsAttention.Count;
        if (result.StudentsAttention.Count > 0)
        {
            var first = result.StudentsAttention[0];
            result.Insights.Add(new AcademicDashboardInsightDto
            {
                Title = $"{first.Name} attention needed",
                Summary = $"Low performance ({first.Performance}%) detected in recent records.",
                Severity = "critical",
                Source = "student-att",
                ActionLabel = "View Profile"
            });
        }
        return result;
    }
}
