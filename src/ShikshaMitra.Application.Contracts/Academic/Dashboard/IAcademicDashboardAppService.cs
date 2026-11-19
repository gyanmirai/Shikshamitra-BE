using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Dashboard;

public interface IAcademicDashboardAppService : IApplicationService
{
    Task<AcademicDashboardContextDto> GetContextAsync();
    Task<AdminAcademicDashboardDto> GetAdminDashboardAsync();
    Task<AdminAcademicDashboardCurriculumDto> GetAdminCurriculumAsync();
    Task<AdminAcademicDashboardTimetableDto> GetAdminTimetableAsync();
    Task<AdminAcademicDashboardAssessmentDto> GetAdminAssessmentAsync();
    Task<AdminAcademicDashboardAnalyticsDto> GetAdminAnalyticsAsync();
    Task<PrincipalAcademicDashboardDto> GetPrincipalDashboardAsync();
    Task<PrincipalAcademicDashboardDto> GetPrincipalOverviewAsync();
    Task<PrincipalAcademicDashboardPerformanceDto> GetPrincipalPerformanceAsync();
    Task<PrincipalAcademicDashboardCurriculumDto> GetPrincipalCurriculumAsync();
    Task<PrincipalAcademicDashboardOperationsDto> GetPrincipalOperationsAsync();
    Task<PrincipalAcademicDashboardAnalyticsDto> GetPrincipalAnalyticsAsync();
    Task<TeacherAcademicDashboardDto> GetTeacherDashboardAsync();
    Task<TeacherAcademicDashboardDto> GetTeacherOverviewAsync();
    Task<TeacherAcademicDashboardAssignmentsDto> GetTeacherAssignmentsAsync();
    Task<TeacherAcademicDashboardAssessmentDto> GetTeacherAssessmentAsync();
    Task<TeacherAcademicDashboardAttentionDto> GetTeacherAttentionAsync();
    Task<TeacherAcademicDashboardAnalyticsDto> GetTeacherAnalyticsAsync();
    Task RefreshDashboardAsync();
    Task<ExamPassFailCountDto> GetSubjectPassFailChartFilteredAsync(Guid standardId, Guid divisionId, string examName);
    Task<ChapterCompletionFilteredDto> GetChapterCompletionChartFilteredAsync(Guid standardId, Guid divisionId);
}
