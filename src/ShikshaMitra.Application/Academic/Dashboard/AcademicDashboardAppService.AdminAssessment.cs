using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Common;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<AdminAcademicDashboardAssessmentDto> GetAdminAssessmentAsync()
    {
        await EnsureAdminDashboardAccessAsync();

        var subjectsCount = (int)await _subjectRepository.GetCountAsync();
        var divisionQuery = await _divisionRepository.GetQueryableAsync();
        var standardCount = await AsyncExecuter.CountAsync(
            divisionQuery.Select(division => division.StandardId).Distinct());
        var assignedSubjectQuery = await _assignedSubjectRepository.GetQueryableAsync();
        var assignedSubjectStates = await AsyncExecuter.ToListAsync(assignedSubjectQuery
            .Select(assignedSubject => new DashboardAssignedSubjectState(
                assignedSubject.Status,
                assignedSubject.Teachers.Any(teacher => teacher.TeacherType == SubjectTeacherType.Primary))));
        var readinessRate = subjectsCount > 0 && standardCount > 0
            ? (int)Math.Round((double)assignedSubjectStates.Count / (subjectsCount * standardCount) * 100)
            : 0;

        var examQuery = await _examRepository.GetQueryableAsync();
        var examStatuses = await AsyncExecuter.ToListAsync(examQuery.Select(exam => exam.Status));
        var pendingGradeEntries = examStatuses.Count(status => status == ExamStatus.Published);
        var upcomingExams = examStatuses.Count(status => status == ExamStatus.Pending);
        var pendingEvaluations = await _submissionRepository.CountAsync(submission =>
            submission.Status == SubmissionStatus.NeedsReview ||
            submission.Status == SubmissionStatus.Submitted ||
            submission.Status == SubmissionStatus.Late);
        var lessonPlans = await GetDashboardLessonPlanCountsAsync();

        var result = new AdminAcademicDashboardAssessmentDto
        {
            ReadinessRate = readinessRate,
            PendingGradeEntries = pendingGradeEntries,
            PendingEvaluations = pendingEvaluations,
            UpcomingExams = upcomingExams,
            OperationalStatus =
            [
                new()
                {
                    Module = "Subject Allocation",
                    Status = assignedSubjectStates.Count > 0 ? $"{readinessRate}% Complete" : "Pending",
                    Level = assignedSubjectStates.Count > 0 ? "success" : "warning",
                    Progress = readinessRate,
                    Icon = "fluent:book-24-regular"
                },
                new()
                {
                    Module = "Grade Entry",
                    Status = $"{pendingGradeEntries} Pending",
                    Level = pendingGradeEntries > 0 ? "error" : "success",
                    Progress = pendingGradeEntries > 0 ? 0 : 100,
                    Icon = "solar:document-text-bold"
                },
                new()
                {
                    Module = "Homework Evaluation",
                    Status = $"{pendingEvaluations} Pending",
                    Level = pendingEvaluations > 0 ? "warning" : "success",
                    Progress = pendingEvaluations > 0 ? 0 : 100,
                    Icon = "solar:notebook-bold"
                }
            ]
        };

        if (pendingGradeEntries > 0)
        {
            result.Insights.Add(new AcademicDashboardInsightDto
            {
                Title = $"Grade entries pending for {pendingGradeEntries} exams",
                Summary = "Marks entry is required for completed/published exams.",
                Severity = "critical",
                Source = "grades",
                ActionLabel = "Record Marks",
                ActionHref = "/academic/gradeEntry"
            });
        }

        var submittedLessonPlans = lessonPlans
            .Where(plan => plan.Status == LessonPlanStatus.Submitted)
            .Sum(plan => plan.Count);
        if (submittedLessonPlans > 0)
        {
            result.Insights.Add(new AcademicDashboardInsightDto
            {
                Title = $"{submittedLessonPlans} lesson plans awaiting review",
                Summary = "Coordinator/admin approval is required before these plans are counted as approved.",
                Severity = "warning",
                Source = "lesson-plan",
                ActionLabel = "Review",
                ActionHref = "/academic/lesson-plan"
            });
        }

        return result;
    }
}
