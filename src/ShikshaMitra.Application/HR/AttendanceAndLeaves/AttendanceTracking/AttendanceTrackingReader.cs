using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceTrackingReader(
	AttendanceTrackingAccessService accessService,
	AttendanceTrackingDateWiseReader dateWiseReader,
	AttendanceTrackingEmployeeWiseReader employeeWiseReader)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceDateWiseResponse> GetDateWiseAsync(GetAttendanceDateWiseInput input)
	{
		var scope = await accessService.EnsureCanReadAttendanceTrackingAsync();
		await accessService.EnsureMediumStageInScopeAsync(scope, input.EducationMediumStageId);
		return await dateWiseReader.GetDateWiseAsync(input);
	}

	public async Task<AttendanceEmployeeWiseResponse> GetEmployeeWiseAsync(GetAttendanceEmployeeWiseInput input)
	{
		var scope = await accessService.EnsureCanReadAttendanceTrackingAsync();
		var stageIds = await accessService.ResolveEmployeeWiseStageIdsAsync(
			scope,
			input.EmployeeId,
			input.EducationMediumStageId,
			input.AcademicYearId);
		return await employeeWiseReader.GetEmployeeWiseAsync(input, stageIds);
	}
}
