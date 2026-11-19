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
	public Task<StudentModuleDashboardSectionDto> GetSectionAsync(string section, [FromQuery] Guid academicYearId,
		[FromQuery] Guid? boardId = null, [FromQuery] Guid? mediumId = null, [FromQuery] Guid? standardId = null, [FromQuery] Guid? divisionId = null) =>
		service.GetSectionAsync(section, academicYearId, boardId, mediumId, standardId, divisionId);

	[HttpGet("filters")]
	public Task<StudentModuleDashboardFiltersDto> GetFiltersAsync([FromQuery] Guid academicYearId) => service.GetFiltersAsync(academicYearId);

	[HttpGet("attendance-registers")]
	public Task<Volo.Abp.Application.Dtos.PagedResultDto<StudentModuleDashboardRegisterDto>> GetAttendanceRegistersAsync(
		[FromQuery] Guid academicYearId, [FromQuery] Guid? boardId = null, [FromQuery] Guid? mediumId = null,
		[FromQuery] Guid? standardId = null, [FromQuery] Guid? divisionId = null, [FromQuery] int skipCount = 0, [FromQuery] int maxResultCount = 20) =>
		service.GetAttendanceRegistersAsync(academicYearId, boardId, mediumId, standardId, divisionId, skipCount, maxResultCount);
}
