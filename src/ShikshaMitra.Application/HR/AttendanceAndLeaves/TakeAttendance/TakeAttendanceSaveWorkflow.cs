using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceSaveWorkflow(
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	IEmployeeLeaveRepository employeeLeaveRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	TakeAttendanceAccessService accessService,
	TakeAttendanceHolidayReader holidayReader,
	TakeAttendanceEmployeeStageResolver employeeStageResolver,
	TakeAttendanceEntryWriter entryWriter,
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
		await EnsureRequestedEmployeesAreInScopeAsync(requestedEmployeeIds, dateOnly, scope, employeeStageMap);
		await EnsureNoApprovedLeaveAttendanceMutationAsync(requestedEmployeeIds, dateOnly);

		var upserted = 0;
		foreach (var parsedEntry in parsedEntries)
		{
			await entryWriter.SaveEntryAsync(parsedEntry, dateOnly, scope, employeeStageMap);
			upserted++;
		}

		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		await payrollSnapshotInvalidator.InvalidateAsync(dateOnly, requestedEmployeeIds);

		return new TakeAttendanceSaveOutput { UpsertedCount = upserted };
	}

	private async Task EnsureNoApprovedLeaveAttendanceMutationAsync(
		List<Guid> requestedEmployeeIds,
		DateOnly dateOnly)
	{
		var approvedLeaveEmployeeIds = (await employeeLeaveRepository.GetApprovedAttendanceLeaveRowsForDateAsync(
				requestedEmployeeIds,
				dateOnly))
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var blockedEmployeeId = requestedEmployeeIds
			.Select(id => (Guid?) id)
			.FirstOrDefault(id => id.HasValue && approvedLeaveEmployeeIds.Contains(id.Value));
		if (blockedEmployeeId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceApprovedLeaveMutationNotAllowed)
				.WithData("attendanceDate", dateOnly)
				.WithData("employeeId", blockedEmployeeId.Value);
		}
	}

	private async Task EnsureRequestedEmployeesAreInScopeAsync(
		List<Guid> requestedEmployeeIds,
		DateOnly dateOnly,
		UserScopeContext scope,
		IReadOnlyDictionary<Guid, Guid?> employeeStageMap)
	{
		var existingAttendanceStageByEmployeeId = await LoadExistingAttendanceStagesAsync(requestedEmployeeIds, dateOnly);
		foreach (var employeeId in requestedEmployeeIds)
		{
			employeeStageMap.TryGetValue(employeeId, out var assignmentStageId);
			existingAttendanceStageByEmployeeId.TryGetValue(employeeId, out var existingStageId);

			var resolvedStageId = assignmentStageId ?? existingStageId;
			resolvedStageId = TakeAttendanceAccessService.ResolveScopedStageFallback(scope, resolvedStageId);

			await accessService.EnsureEmployeeScopeAllowedAsync(scope, resolvedStageId);
		}
	}

	private async Task<Dictionary<Guid, Guid?>> LoadExistingAttendanceStagesAsync(
		List<Guid> requestedEmployeeIds,
		DateOnly dateOnly)
	{
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var existingRows = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(x =>
					requestedEmployeeIds.Contains(x.EmployeeId) &&
					x.AttendanceDate == dateOnly)
				.Select(x => new
				{
					x.EmployeeId,
					x.EducationMediumStageId
				}));

		if (existingRows.Count == 0)
		{
			return new Dictionary<Guid, Guid?>();
		}

		return existingRows
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => x.Select(y => y.EducationMediumStageId).FirstOrDefault());
	}
}
