using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

[RemoteService(false)]
public class AttendanceRegisterSnapshotInvalidator(
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository)
	: ApplicationService, ITransientDependency
{
	public async Task InvalidateUnlockedSnapshotsAsync(HashSet<(Guid EmployeeId, string MonthYear)> keys)
	{
		if (keys.Count == 0)
		{
			return;
		}

		var employeeIds = keys.Select(x => x.EmployeeId).Distinct().ToList();
		var months = keys.Select(x => x.MonthYear).Distinct().ToList();
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		var snapshots = await AsyncExecuter.ToListAsync(query.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			months.Contains(x.MonthYear) &&
			!x.IsLocked));

		foreach (var snapshot in snapshots.Where(x => keys.Contains((x.EmployeeId, x.MonthYear))))
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
		}

		var reviewQuery = await payrollRunReviewRepository.GetQueryableAsync();
		var reviews = await AsyncExecuter.ToListAsync(reviewQuery.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			months.Contains(x.MonthYear)));

		foreach (var review in reviews.Where(x => keys.Contains((x.EmployeeId, x.MonthYear))))
		{
			await payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
		}
	}
}
