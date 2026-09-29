using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Academic.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Dashboard;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/academic/dashboard")]
[Authorize]
public class AcademicDashboardController(IAcademicDashboardAppService dashboardAppService) : AbpControllerBase
{
    [HttpGet("context")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(AcademicDashboardContextDto), StatusCodes.Status200OK)]
    public virtual async Task<AcademicDashboardContextDto> GetContextAsync()
    {
        return await dashboardAppService.GetContextAsync();
    }

    [HttpGet("admin")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(AdminAcademicDashboardDto), StatusCodes.Status200OK)]
    public virtual async Task<AdminAcademicDashboardDto> GetAdminDashboardAsync()
    {
        return await dashboardAppService.GetAdminDashboardAsync();
    }

    [HttpGet("admin/curriculum")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(AdminAcademicDashboardCurriculumDto), StatusCodes.Status200OK)]
    public virtual async Task<AdminAcademicDashboardCurriculumDto> GetAdminCurriculumAsync()
    {
        return await dashboardAppService.GetAdminCurriculumAsync();
    }

    [HttpGet("admin/timetable")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(AdminAcademicDashboardTimetableDto), StatusCodes.Status200OK)]
    public virtual async Task<AdminAcademicDashboardTimetableDto> GetAdminTimetableAsync()
    {
        return await dashboardAppService.GetAdminTimetableAsync();
    }

    [HttpGet("admin/assessment")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(AdminAcademicDashboardAssessmentDto), StatusCodes.Status200OK)]
    public virtual async Task<AdminAcademicDashboardAssessmentDto> GetAdminAssessmentAsync()
    {
        return await dashboardAppService.GetAdminAssessmentAsync();
    }

    [HttpGet("admin/analytics")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(AdminAcademicDashboardAnalyticsDto), StatusCodes.Status200OK)]
    public virtual async Task<AdminAcademicDashboardAnalyticsDto> GetAdminAnalyticsAsync()
    {
        return await dashboardAppService.GetAdminAnalyticsAsync();
    }

    [HttpGet("principal")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(PrincipalAcademicDashboardDto), StatusCodes.Status200OK)]
    public virtual async Task<PrincipalAcademicDashboardDto> GetPrincipalDashboardAsync()
    {
        return await dashboardAppService.GetPrincipalDashboardAsync();
    }

    [HttpGet("principal/overview")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(PrincipalAcademicDashboardDto), StatusCodes.Status200OK)]
    public virtual async Task<PrincipalAcademicDashboardDto> GetPrincipalOverviewAsync()
    {
        return await dashboardAppService.GetPrincipalOverviewAsync();
    }

    [HttpGet("principal/performance")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual Task<PrincipalAcademicDashboardPerformanceDto> GetPrincipalPerformanceAsync() =>
        dashboardAppService.GetPrincipalPerformanceAsync();

    [HttpGet("principal/curriculum")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual Task<PrincipalAcademicDashboardCurriculumDto> GetPrincipalCurriculumAsync() =>
        dashboardAppService.GetPrincipalCurriculumAsync();

    [HttpGet("principal/operations")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual Task<PrincipalAcademicDashboardOperationsDto> GetPrincipalOperationsAsync() =>
        dashboardAppService.GetPrincipalOperationsAsync();

    [HttpGet("principal/analytics")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(PrincipalAcademicDashboardAnalyticsDto), StatusCodes.Status200OK)]
    public virtual async Task<PrincipalAcademicDashboardAnalyticsDto> GetPrincipalAnalyticsAsync()
    {
        return await dashboardAppService.GetPrincipalAnalyticsAsync();
    }

    [HttpGet("teacher")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(TeacherAcademicDashboardDto), StatusCodes.Status200OK)]
    public virtual async Task<TeacherAcademicDashboardDto> GetTeacherDashboardAsync()
    {
        return await dashboardAppService.GetTeacherDashboardAsync();
    }

    [HttpGet("teacher/overview")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(TeacherAcademicDashboardDto), StatusCodes.Status200OK)]
    public virtual async Task<TeacherAcademicDashboardDto> GetTeacherOverviewAsync()
    {
        return await dashboardAppService.GetTeacherOverviewAsync();
    }

    [HttpGet("teacher/assignments")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual Task<TeacherAcademicDashboardAssignmentsDto> GetTeacherAssignmentsAsync() =>
        dashboardAppService.GetTeacherAssignmentsAsync();

    [HttpGet("teacher/assessment")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual Task<TeacherAcademicDashboardAssessmentDto> GetTeacherAssessmentAsync() =>
        dashboardAppService.GetTeacherAssessmentAsync();

    [HttpGet("teacher/attention")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual Task<TeacherAcademicDashboardAttentionDto> GetTeacherAttentionAsync() =>
        dashboardAppService.GetTeacherAttentionAsync();

    [HttpGet("teacher/analytics")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(TeacherAcademicDashboardAnalyticsDto), StatusCodes.Status200OK)]
    public virtual async Task<TeacherAcademicDashboardAnalyticsDto> GetTeacherAnalyticsAsync()
    {
        return await dashboardAppService.GetTeacherAnalyticsAsync();
    }

    [HttpPost("refresh")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    public virtual async Task RefreshDashboardAsync()
    {
        await dashboardAppService.RefreshDashboardAsync();
    }

    [HttpGet("exam-pass-fail")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(ExamPassFailCountDto), StatusCodes.Status200OK)]
    public virtual async Task<ExamPassFailCountDto> GetSubjectPassFailChartFilteredAsync(
        [FromQuery] System.Guid standardId,
        [FromQuery] System.Guid divisionId,
        [FromQuery] string examName)
    {
        return await dashboardAppService.GetSubjectPassFailChartFilteredAsync(standardId, divisionId, examName);
    }

    [HttpGet("chapter-completion")]
    [Authorize(ShikshaMitraSubModulePermissions.AcademicsDashboard)]
    [ProducesResponseType(typeof(ChapterCompletionFilteredDto), StatusCodes.Status200OK)]
    public virtual async Task<ChapterCompletionFilteredDto> GetChapterCompletionChartFilteredAsync(
        [FromQuery] System.Guid standardId,
        [FromQuery] System.Guid divisionId)
    {
        return await dashboardAppService.GetChapterCompletionChartFilteredAsync(standardId, divisionId);
    }
}
