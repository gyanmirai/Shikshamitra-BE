using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Dashboard;

[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
[RemoteService(false)]
public class HRDashboardAppService(
	HRDashboardSummaryReader summaryReader,
	HRDashboardScopeService scopeService,
	HRDashboardPreviewService previewService)
	: ApplicationService, IHRDashboardAppService
{
	public async Task<HRDashboardSummaryDto> GetSummaryAsync(HRDashboardSummaryRequestDto input)
	{
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		return await summaryReader.GetSummaryAsync(input, scope);
	}

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
