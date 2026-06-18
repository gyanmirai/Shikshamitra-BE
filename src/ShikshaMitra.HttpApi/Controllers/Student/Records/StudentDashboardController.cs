using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Student.Records;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/students/dashboard")]
[Authorize]
public class StudentDashboardController(IStudentDashboardAppService dashboardAppService) : AbpControllerBase
{
	[HttpGet("previews/{dashboardKey}")]
	[Authorize(ShikshaMitraSubModulePermissions.StudentDashboard)]
	[ProducesResponseType(typeof(StudentDashboardPreviewDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<StudentDashboardPreviewDto> GetPreviewAsync([FromRoute] string dashboardKey)
	{
		return await dashboardAppService.GetPreviewAsync(dashboardKey);
	}

	[HttpPost("previews/{dashboardKey}/refresh")]
	[Authorize(ShikshaMitraSubModulePermissions.StudentDashboard)]
	[ProducesResponseType(typeof(StudentDashboardPreviewDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<StudentDashboardPreviewDto> RefreshPreviewAsync([FromRoute] string dashboardKey)
	{
		return await dashboardAppService.RefreshPreviewAsync(dashboardKey);
	}
}
