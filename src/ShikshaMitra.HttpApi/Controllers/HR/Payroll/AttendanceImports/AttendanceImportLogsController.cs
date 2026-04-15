using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.AttendanceImports;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/attendance-imports")]
[Authorize]
public class AttendanceImportLogsController(IAttendanceImportLogAppService attendanceImportLogAppService)
	: AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	[ProducesResponseType(typeof(PagedResultDto<AttendanceImportLogListItemDto>), StatusCodes.Status200OK)]
	public async Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(
		[FromQuery] string? search = null,
		[FromQuery] string? payrollMonth = null,
		[FromQuery, Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery, Range(1, 200)] int maxResultCount = 50)
	{
		return await attendanceImportLogAppService.GetListAsync(new GetAttendanceImportLogListInput
		{
			Search = search,
			PayrollMonth = payrollMonth,
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}

	[HttpGet("{importId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	[ProducesResponseType(typeof(AttendanceImportLogDetailDto), StatusCodes.Status200OK)]
	public async Task<AttendanceImportLogDetailDto> GetAsync([FromRoute] Guid importId)
	{
		return await attendanceImportLogAppService.GetAsync(importId);
	}

	[HttpGet("{importId:guid}/rows")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	[ProducesResponseType(typeof(PagedResultDto<AttendanceImportRowDto>), StatusCodes.Status200OK)]
	public async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(
		[FromRoute] Guid importId,
		[FromQuery, Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery, Range(1, 500)] int maxResultCount = 50)
	{
		return await attendanceImportLogAppService.GetRowsAsync(importId, new GetAttendanceImportRowsInput
		{
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}
}
