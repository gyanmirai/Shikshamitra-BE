using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentDashboardAppService(StudentDashboardPreviewService previewService)
	: ApplicationService, IStudentDashboardAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Dashboard.View)]
	public Task<StudentDashboardPreviewDto> GetPreviewAsync(string dashboardKey) =>
		previewService.GetPreviewAsync(dashboardKey);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Dashboard.Add)]
	public Task<StudentDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey) =>
		previewService.RefreshPreviewAsync(dashboardKey);

	[AllowAnonymous]
	public Task RefreshExistingPreviewsAsync() =>
		previewService.RefreshExistingPreviewsAsync();

	[AllowAnonymous]
	public Task ExecutePreviewRefreshAsync(StudentDashboardPreviewRefreshJobArgs args) =>
		previewService.ExecutePreviewRefreshAsync(args);
}
