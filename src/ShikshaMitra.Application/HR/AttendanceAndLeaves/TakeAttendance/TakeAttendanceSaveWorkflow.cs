using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceSaveWorkflow(
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	TakeAttendanceAccessService accessService,
	TakeAttendanceHolidayReader holidayReader,
	TakeAttendanceEmployeeStageResolver employeeStageResolver,
	TakeAttendanceEntryWriter entryWriter,
	TakeAttendanceTeacherAvailabilitySyncService teacherAvailabilitySyncService,
	TakeAttendancePayrollSnapshotInvalidator payrollSnapshotInvalidator
	)
	: ApplicationService, ITransientDependency
{
	public async Task<TakeAttendanceSaveOutput> SaveDayAsync(TakeAttendanceSaveInput input)
	{
		var date = TakeAttendanceDateParser.ParseDate(input.AttendanceDate);
		var dateOnly = DateOnly.FromDateTime(date);
		if (date.Date > Clock.Now.Date)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceFutureDateNotAllowed);
		}

		var scope = await accessService.EnsureCanOperateAttendanceAsync();
		var holiday = await holidayReader.FindHolidayAsync(dateOnly);
		if (holiday != null)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceHolidayNotAllowed)
				.WithData("attendanceDate", dateOnly)
				.WithData("holidayName", holiday.HolidayName);
		}

		if (await staffAttendanceDayLockRepository.FindAsync(dateOnly) != null)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceDayLocked);
		}

		if (input.Entries == null || input.Entries.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		var parsedEntries = input.Entries
			.Select(TakeAttendanceSaveEntryParser.Parse)
			.ToList();
		var requestedEmployeeIds = parsedEntries.Select(x => x.Entry.EmployeeId).Distinct().ToList();
		var employeeStageMap = await employeeStageResolver.ResolveEmployeeStagesAsync(requestedEmployeeIds, scope);
		var upserted = 0;
		var savedEntries = new List<TakeAttendanceSavedEntry>();
		foreach (var parsedEntry in parsedEntries)
		{
			var savedEntry = await entryWriter.SaveEntryAsync(parsedEntry, dateOnly, scope, employeeStageMap);
			savedEntries.Add(savedEntry);
			upserted++;
		}

		foreach (var savedEntry in savedEntries)
		{
			await teacherAvailabilitySyncService.SyncAsync(savedEntry);
		}

		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		await payrollSnapshotInvalidator.InvalidateAsync(dateOnly, requestedEmployeeIds);

		return new TakeAttendanceSaveOutput { UpsertedCount = upserted };
	}
}
