using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceIntegrationRunAppService(AttendanceIntegrationRunWorkflow workflow)
	: ApplicationService, IAttendanceIntegrationRunAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.Add)]
	public Task<AttendanceIntegrationRunResponseDto> RecordIntegrationRunAsync(AttendanceIntegrationRunRequestDto input) =>
		workflow.RecordIntegrationRunAsync(input);
}
