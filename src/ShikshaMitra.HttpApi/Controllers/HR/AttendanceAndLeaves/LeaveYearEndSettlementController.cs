using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/leave-management/year-end-settlements")]
[Authorize]
public class LeaveYearEndSettlementController(
	ILeaveYearEndSettlementAppService appService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	[ProducesResponseType(typeof(List<LeaveYearEndSettlementRunDto>), StatusCodes.Status200OK)]
	public Task<List<LeaveYearEndSettlementRunDto>> GetListAsync(
		[FromQuery, Required] Guid academicYearId) =>
		appService.GetListAsync(new LeaveYearEndSettlementListRequest { AcademicYearId = academicYearId });

	[HttpGet("{runId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	[ProducesResponseType(typeof(LeaveYearEndSettlementRunDto), StatusCodes.Status200OK)]
	public Task<LeaveYearEndSettlementRunDto> GetAsync([FromRoute] Guid runId) =>
		appService.GetAsync(runId);

	[HttpPost("preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Edit)]
	[ProducesResponseType(typeof(LeaveYearEndSettlementRunDto), StatusCodes.Status200OK)]
	public Task<LeaveYearEndSettlementRunDto> PreviewAsync(
		[FromBody] LeaveYearEndSettlementPreviewRequest input) =>
		appService.PreviewAsync(input);

	[HttpPost("{runId:guid}/finalize")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments)]
	[ProducesResponseType(typeof(LeaveYearEndSettlementRunDto), StatusCodes.Status200OK)]
	public Task<LeaveYearEndSettlementRunDto> FinalizeAsync([FromRoute] Guid runId) =>
		appService.FinalizeAsync(runId);
}
