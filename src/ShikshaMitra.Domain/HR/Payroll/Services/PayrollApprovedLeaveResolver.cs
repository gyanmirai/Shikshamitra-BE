using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Services;

public class PayrollApprovedLeaveResolver : DomainService
{
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;

	public PayrollApprovedLeaveResolver(IEmployeeLeaveRepository employeeLeaveRepository)
	{
		_employeeLeaveRepository = employeeLeaveRepository;
	}

	public async Task<Dictionary<Guid, IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>>> ResolveLeaveUnitsByEmployeeAsync(
		IReadOnlyCollection<Guid> employeeIds,
		int year,
		int month,
		CancellationToken cancellationToken = default)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>>();
		}

		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var rangeStart = new DateTimeOffset(firstDay);
		var rangeEnd = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));
		var approvedLeaves = await _employeeLeaveRepository.GetApprovedPayrollLeaveRowsAsync(
			employeeIds,
			rangeStart,
			rangeEnd,
			cancellationToken);

		var mutableResult = employeeIds.ToDictionary(
			id => id,
			_ => new Dictionary<DateOnly, PayrollLeaveUnits>());

		foreach (var leave in approvedLeaves)
		{
			if (!mutableResult.TryGetValue(leave.EmployeeId, out var map))
			{
				continue;
			}

			var paidType = leave.ApprovedPaidType ?? leave.PolicyPaidType;
			var overlapStart = leave.StartDate.Date > firstDay ? leave.StartDate.Date : firstDay;
			var overlapEnd = leave.EndDate.Date < lastDay ? leave.EndDate.Date : lastDay;
			for (var date = overlapStart; date <= overlapEnd; date = date.AddDays(1))
			{
				if (date.DayOfWeek == DayOfWeek.Sunday)
				{
					continue;
				}

				var unitsForDate = leave.HalfDay ? 0.5m : 1m;
				var dateOnly = DateOnly.FromDateTime(date);
				var current = map.GetValueOrDefault(dateOnly, new PayrollLeaveUnits(0m, 0m));
				map[dateOnly] = paidType == LeavePaidType.Paid
					? new PayrollLeaveUnits(Math.Min(1m, current.PaidUnits + unitsForDate), current.UnpaidUnits)
					: new PayrollLeaveUnits(current.PaidUnits, Math.Min(1m, current.UnpaidUnits + unitsForDate));
			}
		}

		return mutableResult.ToDictionary(
			x => x.Key,
			x => (IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>)x.Value);
	}

}
