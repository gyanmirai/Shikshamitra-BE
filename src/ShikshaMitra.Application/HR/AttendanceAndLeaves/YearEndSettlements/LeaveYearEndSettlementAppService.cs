using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

[RemoteService(false)]
public class LeaveYearEndSettlementAppService(
	LeaveYearEndSettlementWorkflow workflow) : ApplicationService, ILeaveYearEndSettlementAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public Task<List<LeaveYearEndSettlementRunDto>> GetListAsync(LeaveYearEndSettlementListRequest input) =>
		workflow.GetListAsync(input.AcademicYearId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)]
	public Task<LeaveYearEndSettlementRunDto> GetAsync(Guid runId) =>
		workflow.GetAsync(runId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Edit)]
	public Task<LeaveYearEndSettlementRunDto> PreviewAsync(LeaveYearEndSettlementPreviewRequest input) =>
		workflow.PreviewAsync(input.AcademicYearId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments)]
	public Task<LeaveYearEndSettlementRunDto> FinalizeAsync(Guid runId) =>
		workflow.FinalizeAsync(runId);
}
