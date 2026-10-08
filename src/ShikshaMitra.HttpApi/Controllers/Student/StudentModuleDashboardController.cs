using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Student;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/students/dashboard")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Dashboard.View)]
public class StudentModuleDashboardController(IStudentModuleDashboardAppService service) : AbpControllerBase
{
	[HttpGet("context")]
	public Task<StudentModuleDashboardContextDto> GetContextAsync() => service.GetContextAsync();

	[HttpGet("sections/{section}")]
	public Task<StudentModuleDashboardSectionDto> GetSectionAsync(string section, [FromQuery] Guid academicYearId) =>
		service.GetSectionAsync(section, academicYearId);
}
