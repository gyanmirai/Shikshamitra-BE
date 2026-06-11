using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/attendance/automated-runs")]
[Authorize]
public class HrAutomatedAttendanceRunsController(
	IAttendanceImportLogAppService attendanceImportLogAppService,
	IPermissionChecker permissionChecker) : AbpControllerBase
{
	private async Task EnsureHrRunLogReadAccessAsync()
	{
		var import = await permissionChecker.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.View);
		var tracking = await permissionChecker.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.View);
		if (!import && !tracking)
		{
			throw new AbpAuthorizationException(
				"Permission denied: requires HR.AttendanceAndLeave.ImportAttendance.View or HR.AttendanceAndLeave.AttendanceTracking.View");
		}
	}

	[HttpGet]
	[ProducesResponseType(typeof(PagedResultDto<AttendanceImportLogListItemDto>), StatusCodes.Status200OK)]
	public virtual async Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(
		[FromQuery] string? search = null,
		[FromQuery] string? payrollMonth = null,
		[FromQuery] [Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery] [Range(1, 200)] int maxResultCount = 50)
	{
		await EnsureHrRunLogReadAccessAsync();
		return await attendanceImportLogAppService.GetListAsync(new GetAttendanceImportLogListInput
		{
			Search = search,
			PayrollMonth = payrollMonth,
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}

	[HttpGet("{importId:guid}")]
	[ProducesResponseType(typeof(AttendanceImportLogDetailDto), StatusCodes.Status200OK)]
	public virtual async Task<AttendanceImportLogDetailDto> GetAsync([FromRoute] Guid importId)
	{
		await EnsureHrRunLogReadAccessAsync();
		return await attendanceImportLogAppService.GetAsync(importId);
	}

	[HttpGet("{importId:guid}/rows")]
	[ProducesResponseType(typeof(PagedResultDto<AttendanceImportRowDto>), StatusCodes.Status200OK)]
	public virtual async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(
		[FromRoute] Guid importId,
		[FromQuery] [Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery] [Range(1, 500)] int maxResultCount = 50)
	{
		await EnsureHrRunLogReadAccessAsync();
		return await attendanceImportLogAppService.GetRowsAsync(importId, new GetAttendanceImportRowsInput
		{
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}

	[HttpGet("{importId:guid}/download-file")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public virtual async Task<IActionResult> DownloadFileAsync([FromRoute] Guid importId)
	{
		await EnsureHrRunLogReadAccessAsync();
		var file = await attendanceImportLogAppService.DownloadFileAsync(importId);
		return File(file.Content, file.ContentType, file.FileName);
	}

	[HttpGet("{importId:guid}/download-error-report")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public virtual async Task<IActionResult> DownloadErrorReportAsync([FromRoute] Guid importId)
	{
		await EnsureHrRunLogReadAccessAsync();
		var file = await attendanceImportLogAppService.DownloadErrorReportAsync(importId);
		return File(file.Content, file.ContentType, file.FileName);
	}

	[HttpPost("{importId:guid}/rows/save")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.Add)]
	[ProducesResponseType(typeof(AttendanceImportRowsSaveOutput), StatusCodes.Status200OK)]
	public virtual async Task<AttendanceImportRowsSaveOutput> SaveRowsAsync(
		[FromRoute] Guid importId,
		[FromBody] AttendanceImportRowsSaveInput input)
	{
		return await attendanceImportLogAppService.SaveRowsAsync(importId, input);
	}

	[HttpPost("{batchId:guid}/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.Add)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> LockBatchAsync([FromRoute] Guid batchId)
	{
		await attendanceImportLogAppService.LockBatchAsync(batchId);
		return NoContent();
	}

	[HttpDelete("{batchId:guid}/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.Add)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> UnlockBatchAsync([FromRoute] Guid batchId)
	{
		await attendanceImportLogAppService.UnlockBatchAsync(batchId);
		return NoContent();
	}
}
