using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Dashboard;

[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
[RemoteService(false)]
public class HRDashboardAppService(
	HRDashboardOperationalReader operationalReader,
	HRDashboardPreviewService previewService)
	: ApplicationService, IHRDashboardAppService
{
	public Task<HRDashboardOverviewDto> GetOverviewAsync(HRDashboardOverviewInput input) =>
		operationalReader.GetOverviewAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View)]
	public Task<HRDashboardStaffPageDto> GetStaffAsync(HRDashboardStaffInput input) =>
		operationalReader.GetStaffAsync(input);

	public Task<HRDashboardPreviewDto> GetPreviewAsync(string dashboardKey) =>
		previewService.GetPreviewAsync(dashboardKey);

	public Task<HRDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey) =>
		previewService.RefreshPreviewAsync(dashboardKey);

	[AllowAnonymous]
	public Task RefreshExistingPreviewsAsync() =>
		previewService.RefreshExistingPreviewsAsync();

	[AllowAnonymous]
	public Task ExecutePreviewRefreshAsync(HrDashboardPreviewRefreshJobArgs args) =>
		previewService.ExecutePreviewRefreshAsync(args);
}
