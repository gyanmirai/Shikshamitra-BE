using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendancePayrollSnapshotInvalidator(
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository)
	: ApplicationService, ITransientDependency
{
	public async Task InvalidateAsync(DateOnly attendanceDate, IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return;
		}

		var monthYear = attendanceDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		var snapshotQuery = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		var snapshots = await AsyncExecuter.ToListAsync(snapshotQuery.Where(x =>
			x.MonthYear == monthYear &&
			employeeIds.Contains(x.EmployeeId) &&
			!x.IsLocked &&
			x.ReviewStatus != "REVIEWED"));
		if (snapshots.Count == 0)
		{
			return;
		}

		var invalidatedEmployeeIds = snapshots.Select(x => x.EmployeeId).Distinct().ToList();
		var autoSave = UnitOfWorkManager.Current == null;
		foreach (var snapshot in snapshots)
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave);
		}

		var reviewQuery = await payrollRunReviewRepository.GetQueryableAsync();

		var reviews = await AsyncExecuter.ToListAsync(reviewQuery.Where(x =>
			x.MonthYear == monthYear &&
			invalidatedEmployeeIds.Contains(x.EmployeeId)));
		foreach (var review in reviews)
		{
			await payrollRunReviewRepository.DeleteAsync(review, autoSave);
		}

		if (!autoSave && UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}
	}
}
