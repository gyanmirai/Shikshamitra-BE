using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotAttendanceInvalidationReader(
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	IAsyncQueryableExecuter asyncExecuter,
	ILogger<PayrollSnapshotAttendanceInvalidationReader> logger) : ITransientDependency
{
	public async Task<bool> HasAttendanceInvalidationsAsync(
		string monthYear,
		int year,
		int month,
		IReadOnlySet<Guid> employeeIds,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee)
	{
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();

		var attendanceChanges = await asyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a =>
					employeeIds.Contains(a.EmployeeId) &&
					a.AttendanceDate >= startDate &&
					a.AttendanceDate <= endDate)
				.Select(a => new
				{
					a.EmployeeId,
					ChangedAt = a.LastModificationTime ?? a.CreationTime
				}));

		var employeesWithAttendance = attendanceChanges.Select(x => x.EmployeeId).ToHashSet();
		foreach (var employeeId in employeesWithAttendance)
		{
			var snapshot = snapshotByEmployee[employeeId];
			if (!snapshot.IsLocked &&
			    (JsonSerializer.Deserialize<List<string>>(snapshot.IssueCodesJson) ?? new List<string>()).Contains(IssueCodes.NoAttendance))
			{
				logger.LogDebug(
					"Payroll snapshot for staff {EmployeeId} in {MonthYear} is stale because attendance now exists.",
					employeeId,
					monthYear);
				return true;
			}
		}

		foreach (var attendance in attendanceChanges)
		{
			var snapshot = snapshotByEmployee[attendance.EmployeeId];
			if (!snapshot.IsLocked &&
			    attendance.ChangedAt > (snapshot.LastModificationTime ?? snapshot.CreationTime))
			{
				logger.LogDebug(
					"Payroll snapshot for staff {EmployeeId} in {MonthYear} is stale after attendance change.",
					attendance.EmployeeId,
					monthYear);
				return true;
			}
		}

		return false;
	}
}
