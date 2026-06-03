using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View)]
public class AttendanceRegisterAppService(
	AttendanceRegisterReader registerReader,
	AttendanceRegisterAdjustmentWorkflow adjustmentWorkflow)
	: ApplicationService, IAttendanceRegisterAppService
{
	public Task<AttendanceRegisterResponse> GetAsync(GetAttendanceRegisterInput input) =>
		registerReader.GetAsync(input);

	public Task<GroupedAttendanceRegisterResponse> GetGroupedAsync(GetGroupedAttendanceRegisterInput input) =>
		registerReader.GetGroupedAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.Add)]
	public Task<AttendanceRegisterAdjustmentResult> AdjustAsync(AttendanceRegisterAdjustRequest input) =>
		adjustmentWorkflow.AdjustAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.Add)]
	public Task<AttendanceRegisterAdjustmentResult> BulkAdjustAsync(AttendanceRegisterBulkAdjustRequest input) =>
		adjustmentWorkflow.BulkAdjustAsync(input);
}
