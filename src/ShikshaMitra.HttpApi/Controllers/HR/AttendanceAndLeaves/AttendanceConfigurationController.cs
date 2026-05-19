using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance/configuration")]
[Authorize]
public class AttendanceConfigurationController(IAttendanceConfigurationAppService attendanceConfigurationAppService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.View)]
	[ProducesResponseType(typeof(AttendanceConfigurationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<AttendanceConfigurationResponse> GetAsync(
		[FromQuery, Required] Guid academicYearId)
	{
		return await attendanceConfigurationAppService.GetAsync(academicYearId);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.Add)]
	[ProducesResponseType(typeof(AttendanceConfigurationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
	public virtual async Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromBody, Required] AttendanceConfigurationRequest request)
	{
		return await attendanceConfigurationAppService.CreateOrUpdateAsync(academicYearId, request);
	}

	[HttpPost("preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.View)]
	[ProducesResponseType(typeof(AttendanceConfigurationPreviewResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
	public virtual async Task<AttendanceConfigurationPreviewResponse> PreviewAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromBody, Required] AttendanceConfigurationPreviewRequest request)
	{
		return await attendanceConfigurationAppService.PreviewAsync(academicYearId, request);
	}

	[HttpPost("evaluations/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.View)]
	[ProducesResponseType(typeof(AttendancePolicyEvaluationRunResponse), StatusCodes.Status200OK)]
	public virtual async Task<AttendancePolicyEvaluationRunResponse> PreviewEvaluationAsync(
		[FromBody, Required] AttendancePolicyEvaluationRequest request)
	{
		return await attendanceConfigurationAppService.PreviewEvaluationAsync(request);
	}

	[HttpPost("evaluations")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.Add)]
	[ProducesResponseType(typeof(AttendancePolicyEvaluationRunResponse), StatusCodes.Status200OK)]
	public virtual async Task<AttendancePolicyEvaluationRunResponse> CreateEvaluationAsync(
		[FromBody, Required] AttendancePolicyEvaluationRequest request)
	{
		return await attendanceConfigurationAppService.CreateEvaluationAsync(request);
	}

	[HttpGet("evaluations")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.View)]
	[ProducesResponseType(typeof(List<AttendancePolicyEvaluationRunResponse>), StatusCodes.Status200OK)]
	public virtual async Task<List<AttendancePolicyEvaluationRunResponse>> GetEvaluationsAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery, Required] int month,
		[FromQuery, Required] int year)
	{
		return await attendanceConfigurationAppService.GetEvaluationsAsync(academicYearId, month, year);
	}

	[HttpGet("evaluations/{runId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.View)]
	[ProducesResponseType(typeof(AttendancePolicyEvaluationRunResponse), StatusCodes.Status200OK)]
	public virtual async Task<AttendancePolicyEvaluationRunResponse> GetEvaluationAsync(Guid runId)
	{
		return await attendanceConfigurationAppService.GetEvaluationAsync(runId);
	}

	[HttpGet("evaluations/{runId:guid}/results")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.View)]
	[ProducesResponseType(typeof(PagedResultDto<AttendancePolicyEvaluationResultResponse>), StatusCodes.Status200OK)]
	public virtual async Task<PagedResultDto<AttendancePolicyEvaluationResultResponse>> GetEvaluationResultsAsync(
		Guid runId,
		[FromQuery] AttendancePolicyEvaluationResultsRequest request)
	{
		return await attendanceConfigurationAppService.GetEvaluationResultsAsync(runId, request);
	}

	[HttpPost("evaluations/{runId:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.Add)]
	[ProducesResponseType(typeof(AttendancePolicyEvaluationRunResponse), StatusCodes.Status200OK)]
	public virtual async Task<AttendancePolicyEvaluationRunResponse> ApproveEvaluationAsync(Guid runId)
	{
		return await attendanceConfigurationAppService.ApproveEvaluationAsync(runId);
	}

	[HttpPost("evaluations/{runId:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.Add)]
	[ProducesResponseType(typeof(AttendancePolicyEvaluationRunResponse), StatusCodes.Status200OK)]
	public virtual async Task<AttendancePolicyEvaluationRunResponse> PublishEvaluationAsync(Guid runId)
	{
		return await attendanceConfigurationAppService.PublishEvaluationAsync(runId);
	}

	[HttpPost("evaluations/{runId:guid}/cancel")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingAttendanceConfiguration.Add)]
	[ProducesResponseType(typeof(AttendancePolicyEvaluationRunResponse), StatusCodes.Status200OK)]
	public virtual async Task<AttendancePolicyEvaluationRunResponse> CancelEvaluationAsync(Guid runId)
	{
		return await attendanceConfigurationAppService.CancelEvaluationAsync(runId);
	}
}
