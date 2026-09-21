using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    public async Task<AdminAcademicDashboardCurriculumDto> GetAdminCurriculumAsync()
    {
        await EnsureAdminDashboardAccessAsync();

        var studentQuery = await _studentRepository.GetQueryableAsync();
        var studentCountRows = await AsyncExecuter.ToListAsync(studentQuery
            .GroupBy(_ => 1)
            .Select(group => new DashboardStudentCounts(
                group.Count(),
                group.Count(student => student.StandardId.HasValue && student.DivisionId.HasValue))));
        var studentCounts = studentCountRows.SingleOrDefault();
        var totalTeachers = await _employeeRepository.CountAsync(employee =>
            employee.EmployeeGroup == EmployeeGroupType.Teaching);
        var subjectsCount = (int)await _subjectRepository.GetCountAsync();

        var electiveGroupQuery = await _electiveGroupRepository.GetQueryableAsync();
        var electiveGroupStates = await AsyncExecuter.ToListAsync(electiveGroupQuery
            .Select(group => !group.Subjects.Any()));
        var progressList = await GetDashboardChapterProgressAsync();
        var curriculumCompletion = progressList.Count > 0
            ? (int)Math.Round(progressList.Average(progress => progress.Progress))
            : 0;

        var result = new AdminAcademicDashboardCurriculumDto
        {
            CurriculumCompletion = curriculumCompletion,
            TotalStudents = studentCounts?.Total ?? 0,
            TotalTeachers = totalTeachers,
            AssignedSubjects = subjectsCount,
            ElectiveGroups = electiveGroupStates.Count,
            PendingElectiveGroups = electiveGroupStates.Count(isPending => isPending),
            ConfigurationStatus =
            [
                new() { Label = "Curriculum", Status = progressList.Count > 0 ? "active" : "missing", Path = "/academic/curriculumSubjects", Icon = "fluent:book-24-regular" },
                new() { Label = "Subjects Creation", Status = subjectsCount > 0 ? "configured" : "missing", Path = "/academic/curriculumSetting", Icon = "solar:book-bold" },
                new() { Label = "Elective Groups", Status = electiveGroupStates.Count > 0 ? "configured" : "missing", Path = "/academic/curriculumSetting", Icon = "solar:users-group-two-rounded-bold" }
            ]
        };

        var lagCount = progressList.Count(progress => progress.Progress < 50);
        if (lagCount > 0)
        {
            result.Insights.Add(new AcademicDashboardInsightDto
            {
                Title = $"Syllabus progress lag in {lagCount} chapters",
                Summary = "Curriculum progress is behind planned schedules.",
                Severity = "warning",
                Source = "curriculum",
                ActionLabel = "View chapter tracker",
                ActionHref = "/academic/courseSubjects"
            });
        }

        return result;
    }

    private async Task EnsureAdminDashboardAccessAsync()
    {
        if (!await _userScopeAccessService.CurrentUserHasTenantWideScopeAsync())
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied);
        }
    }
}
