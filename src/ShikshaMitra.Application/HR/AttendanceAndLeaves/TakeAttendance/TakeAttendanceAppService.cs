using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class TakeAttendanceAppService(
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	TakeAttendanceDayReader dayReader,
	TakeAttendanceSaveWorkflow saveWorkflow,
	TakeAttendanceAccessService accessService,
	TakeAttendanceHolidayReader holidayReader)
	: ApplicationService, ITakeAttendanceAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.View)]
	public Task<TakeAttendanceDayResponse> GetDayAsync(GetTakeAttendanceDayInput input) =>
		dayReader.GetDayAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Add)]
	public Task<TakeAttendanceSaveOutput> SaveDayAsync(TakeAttendanceSaveInput input) =>
		saveWorkflow.SaveDayAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Delete)]
	public async Task LockDayAsync(string attendanceDate)
	{
		var date = TakeAttendanceDateParser.ParseDate(attendanceDate).Date;
		var dateOnly = DateOnly.FromDateTime(date);
		var scope = await accessService.EnsureCanOperateAttendanceAsync();
		TakeAttendanceAccessService.EnsureTenantWideDayLockAllowed(scope);
		var holiday = await holidayReader.FindHolidayAsync(dateOnly);
		if (holiday != null)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceHolidayNotAllowed)
				.WithData("attendanceDate", dateOnly)
				.WithData("holidayName", holiday.HolidayName);
		}

		if (await staffAttendanceDayLockRepository.FindAsync(dateOnly) != null)
		{
			return;
		}

		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var entity = new StaffAttendanceDayLock(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			dateOnly,
			userId,
			Clock.Now);
		await staffAttendanceDayLockRepository.InsertAsync(entity, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Delete)]
	public async Task UnlockDayAsync(string attendanceDate)
	{
		var date = TakeAttendanceDateParser.ParseDate(attendanceDate).Date;
		var dateOnly = DateOnly.FromDateTime(date);
		var scope = await accessService.EnsureCanOperateAttendanceAsync();
		TakeAttendanceAccessService.EnsureTenantWideDayLockAllowed(scope);
		var existing = await staffAttendanceDayLockRepository.FindAsync(dateOnly);
		if (existing != null)
		{
			await staffAttendanceDayLockRepository.DeleteAsync(existing, autoSave: true);
		}
	}
}
