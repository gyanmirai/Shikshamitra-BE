using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Dashboard;

public interface IAcademicDashboardAppService : IApplicationService
{
    Task<AdminAcademicDashboardDto> GetAdminDashboardAsync();
    Task<PrincipalAcademicDashboardDto> GetPrincipalDashboardAsync();
    Task<TeacherAcademicDashboardDto> GetTeacherDashboardAsync();
    Task RefreshDashboardAsync();
    Task<ExamPassFailCountDto> GetSubjectPassFailChartFilteredAsync(Guid standardId, Guid divisionId, string examName);
    Task<ChapterCompletionFilteredDto> GetChapterCompletionChartFilteredAsync(Guid standardId, Guid divisionId);
}
