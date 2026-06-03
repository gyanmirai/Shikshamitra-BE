using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotRefreshLookupReader(
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<Dictionary<Guid, List<EmployeeAttendance>>> GetAttendanceRecordsAsync(
		List<Guid> employeeIds,
		DateOnly startDate,
		DateOnly endDate)
	{
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();

		var attendanceRecords = await asyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a =>
					employeeIds.Contains(a.EmployeeId) &&
					a.AttendanceDate >= startDate &&
					a.AttendanceDate <= endDate)
		);

		return attendanceRecords
			.GroupBy(a => a.EmployeeId)
			.ToDictionary(g => g.Key, g => g.ToList());
	}

	public async Task<Dictionary<Guid, PayrollRunReview>> GetReviewsByEmployeeAsync(string monthYear)
	{
		var query = await payrollRunReviewRepository.GetQueryableAsync();

		return (await asyncExecuter.ToListAsync(query.Where(x => x.MonthYear == monthYear)))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReviewedAt).First());
	}
}
