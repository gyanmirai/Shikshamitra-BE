using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister.AttendanceRegisterAdjustmentMapper;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

[RemoteService(false)]
public class AttendanceRegisterAdjustmentWorkflow(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	AttendanceRegisterReader registerReader,
	AttendanceRegisterSnapshotInvalidator snapshotInvalidator,
	AttendanceRegisterExistingRowAdjuster existingRowAdjuster,
	AttendanceRegisterMissingRowCreator missingRowCreator)
	: ApplicationService, ITransientDependency
{
	private const int RemarksMaxLength = 512;

	public async Task<AttendanceRegisterAdjustmentResult> AdjustAsync(AttendanceRegisterAdjustRequest input)
	{
		if (input.AttendanceId.HasValue)
		{
			return await BulkAdjustAsync(new AttendanceRegisterBulkAdjustRequest
			{
				AttendanceIds = [input.AttendanceId.Value],
				StatusWire = input.StatusWire,
				InTime = input.InTime,
				OutTime = input.OutTime,
				Remarks = input.Remarks,
				Reason = input.Reason
			});
		}

		if (!input.EmployeeId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		var status = ParseWireStatus(input.StatusWire);
		var timeIn = ParseOptionalTime(input.InTime);
		var timeOut = ParseOptionalTime(input.OutTime);
		var remarks = Truncate(input.Remarks, RemarksMaxLength);
		var attendanceDate = ParseAdjustmentDate(input.AttendanceDate);
		var scope = await registerReader.EnsureCanUseRegisterAsync();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(attendanceQuery.Where(x =>
			x.EmployeeId == input.EmployeeId.Value &&
			x.AttendanceDate == attendanceDate));
		var results = new List<AttendanceRegisterAdjustmentRowResult>();
		var changedEmployeeMonths = new HashSet<(Guid EmployeeId, string MonthYear)>();

		if (existing != null)
		{
			var holidayLookup = await registerReader.BuildHolidayLookupAsync([existing.AttendanceDate]);
			await existingRowAdjuster.TryAdjustRowAsync(input.Reason, existing, status, timeIn, timeOut, remarks, holidayLookup, results, changedEmployeeMonths, scope);
		}
		else
		{
			await missingRowCreator.TryCreateRowAsync(input, input.EmployeeId.Value, attendanceDate, status, timeIn, timeOut, remarks, results, changedEmployeeMonths, scope);
		}

		await snapshotInvalidator.InvalidateUnlockedSnapshotsAsync(changedEmployeeMonths);
		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		return new AttendanceRegisterAdjustmentResult
		{
			UpdatedCount = results.Count(x => x.Updated),
			SkippedCount = results.Count(x => !x.Updated),
			Rows = results
		};
	}

	public async Task<AttendanceRegisterAdjustmentResult> BulkAdjustAsync(AttendanceRegisterBulkAdjustRequest input)
	{
		if (input.AttendanceIds.Count == 0 && input.EmployeeDates.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		var status = ParseWireStatus(input.StatusWire);
		var timeIn = ParseOptionalTime(input.InTime);
		var timeOut = ParseOptionalTime(input.OutTime);
		var remarks = Truncate(input.Remarks, RemarksMaxLength);
		var scope = await registerReader.EnsureCanUseRegisterAsync();
		var results = new List<AttendanceRegisterAdjustmentRowResult>();
		var changedEmployeeMonths = new HashSet<(Guid EmployeeId, string MonthYear)>();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();

		if (input.AttendanceIds.Count > 0)
		{
			var uniqueIds = input.AttendanceIds.Distinct().ToList();
			var rows = await AsyncExecuter.ToListAsync(attendanceQuery.Where(x => uniqueIds.Contains(x.Id)));
			var foundById = rows.ToDictionary(x => x.Id);
			var holidayLookup = await registerReader.BuildHolidayLookupAsync(rows.Select(x => x.AttendanceDate));

			foreach (var id in uniqueIds)
			{
				if (!foundById.TryGetValue(id, out var row))
				{
					results.Add(new AttendanceRegisterAdjustmentRowResult
					{
						AttendanceId = id,
						Updated = false,
						Reason = "Attendance row not found."
					});
					continue;
				}

				await existingRowAdjuster.TryAdjustRowAsync(input.Reason, row, status, timeIn, timeOut, remarks, holidayLookup, results, changedEmployeeMonths, scope);
			}
		}

		if (input.EmployeeDates.Count > 0)
		{
			var employeeIds = input.EmployeeDates.Select(x => x.EmployeeId).Distinct().ToList();
			var dates = input.EmployeeDates.Select(x => ParseAdjustmentDate(x.AttendanceDate)).Distinct().ToList();

			var existingRows = await AsyncExecuter.ToListAsync(
				attendanceQuery.Where(x => employeeIds.Contains(x.EmployeeId) && dates.Contains(x.AttendanceDate))
			);

			var existingLookup = existingRows.ToLookup(x => (x.EmployeeId, x.AttendanceDate));
			var holidayLookupForDates = await registerReader.BuildHolidayLookupAsync(dates);

			foreach (var empDate in input.EmployeeDates)
			{
				var parsedDate = ParseAdjustmentDate(empDate.AttendanceDate);
				var existingRow = existingLookup[(empDate.EmployeeId, parsedDate)].FirstOrDefault();

				if (existingRow != null)
				{
					await existingRowAdjuster.TryAdjustRowAsync(input.Reason, existingRow, status, timeIn, timeOut, remarks, holidayLookupForDates, results, changedEmployeeMonths, scope);
				}
				else
				{
					var singleRequest = new AttendanceRegisterAdjustRequest
					{
						EmployeeId = empDate.EmployeeId,
						AttendanceDate = empDate.AttendanceDate,
						StatusWire = input.StatusWire,
						InTime = input.InTime,
						OutTime = input.OutTime,
						Remarks = input.Remarks,
						Reason = input.Reason
					};
					await missingRowCreator.TryCreateRowAsync(singleRequest, empDate.EmployeeId, parsedDate, status, timeIn, timeOut, remarks, results, changedEmployeeMonths, scope);
				}
			}
		}

		await snapshotInvalidator.InvalidateUnlockedSnapshotsAsync(changedEmployeeMonths);
		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		return new AttendanceRegisterAdjustmentResult
		{
			UpdatedCount = results.Count(x => x.Updated),
			SkippedCount = results.Count(x => !x.Updated),
			Rows = results
		};
	}
}
