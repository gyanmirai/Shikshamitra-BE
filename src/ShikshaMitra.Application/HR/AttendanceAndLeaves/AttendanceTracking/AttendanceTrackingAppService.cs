using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceTrackingAppService(AttendanceTrackingReader reader)
	: ApplicationService, IAttendanceTrackingAppService
{
	public Task<AttendanceDateWiseResponse> GetDateWiseAsync(GetAttendanceDateWiseInput input) =>
		reader.GetDateWiseAsync(input);

	public Task<AttendanceEmployeeWiseResponse> GetEmployeeWiseAsync(GetAttendanceEmployeeWiseInput input) =>
		reader.GetEmployeeWiseAsync(input);
}
