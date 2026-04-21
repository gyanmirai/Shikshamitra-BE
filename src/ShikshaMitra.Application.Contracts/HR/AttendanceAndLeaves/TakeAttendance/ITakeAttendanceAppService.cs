using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public interface ITakeAttendanceAppService : IApplicationService
{
	Task<TakeAttendanceDayResponse> GetDayAsync(GetTakeAttendanceDayInput input);
	Task<TakeAttendanceSaveOutput> SaveDayAsync(TakeAttendanceSaveInput input);
	Task LockDayAsync(Guid educationMediumStageId, string attendanceDate);
	Task UnlockDayAsync(Guid educationMediumStageId, string attendanceDate);
}