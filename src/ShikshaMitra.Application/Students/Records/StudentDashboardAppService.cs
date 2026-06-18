using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

[Authorize(ShikshaMitraSubModulePermissions.StudentDashboard)]
[RemoteService(false)]
public class StudentDashboardAppService(StudentDashboardPreviewService previewService)
	: ApplicationService, IStudentDashboardAppService
{
	public Task<StudentDashboardPreviewDto> GetPreviewAsync(string dashboardKey) =>
		previewService.GetPreviewAsync(dashboardKey);

	public Task<StudentDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey) =>
		previewService.RefreshPreviewAsync(dashboardKey);

	[AllowAnonymous]
	public Task RefreshExistingPreviewsAsync() =>
		previewService.RefreshExistingPreviewsAsync();

	[AllowAnonymous]
	public Task ExecutePreviewRefreshAsync(StudentDashboardPreviewRefreshJobArgs args) =>
		previewService.ExecutePreviewRefreshAsync(args);
}
