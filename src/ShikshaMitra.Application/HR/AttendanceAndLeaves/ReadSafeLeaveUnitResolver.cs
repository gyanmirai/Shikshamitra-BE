using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public sealed class ReadSafeLeaveUnitResolver(
	LeaveWorkCalendarUnitService leaveWorkCalendarUnitService,
	ILogger<ReadSafeLeaveUnitResolver> logger) : ITransientDependency
{
	public async Task<decimal> CalculateUnitsForReadAsync(
		Guid employeeId,
		DateTime startDate,
		DateTime endDate,
		bool halfDay,
		Guid? academicYearId,
		IDictionary<ReadSafeLeaveUnitCacheKey, decimal>? cache = null,
		CancellationToken cancellationToken = default)
	{
		var key = new ReadSafeLeaveUnitCacheKey(
			employeeId,
			startDate.Date,
			endDate.Date,
			halfDay,
			academicYearId);
		if (cache?.TryGetValue(key, out var cachedUnits) == true)
		{
			return cachedUnits;
		}

		decimal units;
		try
		{
			units = await leaveWorkCalendarUnitService.CalculateUnitsAsync(
				employeeId,
				startDate,
				endDate,
				halfDay,
				academicYearId,
				cancellationToken);
		}
		catch (BusinessException ex) when (PayrollRunRules.IsPayrollPolicySetupException(ex))
		{
			units = CountLegacyDisplayUnits(startDate, endDate, halfDay);
			logger.LogWarning(
				ex,
				"Using legacy display leave units because payroll policy setup is incomplete. EmployeeId={EmployeeId}, AcademicYearId={AcademicYearId}, StartDate={StartDate}, EndDate={EndDate}, HalfDay={HalfDay}, Units={Units}, ErrorCode={ErrorCode}",
				employeeId,
				academicYearId,
				startDate.Date,
				endDate.Date,
				halfDay,
				units,
				ex.Code);
		}

		if (cache != null)
		{
			cache[key] = units;
		}

		return units;
	}

	internal static decimal CountLegacyDisplayUnits(DateTime startDate, DateTime endDate, bool halfDay)
	{
		var start = startDate.Date;
		var end = endDate.Date;
		if (start > end)
		{
			return 0m;
		}

		if (halfDay)
		{
			return start == end ? 0.5m : 0m;
		}

		var count = 0m;
		for (var date = start; date <= end; date = date.AddDays(1))
		{
			if (date.DayOfWeek != DayOfWeek.Sunday)
			{
				count += 1m;
			}
		}

		return count;
	}
}

public readonly record struct ReadSafeLeaveUnitCacheKey(
	Guid EmployeeId,
	DateTime StartDate,
	DateTime EndDate,
	bool HalfDay,
	Guid? AcademicYearId);
