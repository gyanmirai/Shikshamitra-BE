using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance/configuration")]
[Authorize]
public class AttendanceConfigurationController(IAttendanceConfigurationAppService attendanceConfigurationAppService) : AbpControllerBase
{
	[HttpGet]
	[ProducesResponseType(typeof(AttendanceConfigurationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<AttendanceConfigurationResponse> GetAsync(
		[FromQuery, Required] Guid academicYearId)
	{
		return await attendanceConfigurationAppService.GetAsync(academicYearId);
	}

	[HttpPost]
	[ProducesResponseType(typeof(AttendanceConfigurationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
	public virtual async Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromBody, Required] AttendanceConfigurationRequest request)
	{
		return await attendanceConfigurationAppService.CreateOrUpdateAsync(academicYearId, request);
	}
}
