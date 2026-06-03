using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceIntegrationRunAppService(AttendanceIntegrationRunWorkflow workflow)
	: ApplicationService, IAttendanceIntegrationRunAppService
{
	public Task<AttendanceIntegrationRunResponseDto> RecordIntegrationRunAsync(AttendanceIntegrationRunRequestDto input) =>
		workflow.RecordIntegrationRunAsync(input);
}
