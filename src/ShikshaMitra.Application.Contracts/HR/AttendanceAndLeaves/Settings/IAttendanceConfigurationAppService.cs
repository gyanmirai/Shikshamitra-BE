using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface IAttendanceConfigurationAppService : IApplicationService
{
	Task<AttendanceConfigurationResponse> CreateOrUpdateAsync(Guid academicYearId, AttendanceConfigurationRequest input);
}
