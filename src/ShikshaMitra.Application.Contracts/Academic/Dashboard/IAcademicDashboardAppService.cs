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
    Task<TeacherAcademicDashboardDto> GetTeacherDashboardAsync();
    Task RefreshDashboardAsync();
    Task<ExamPassFailCountDto> GetSubjectPassFailChartFilteredAsync(Guid standardId, Guid divisionId, string examName);
    Task<ChapterCompletionFilteredDto> GetChapterCompletionChartFilteredAsync(Guid standardId, Guid divisionId);
}
