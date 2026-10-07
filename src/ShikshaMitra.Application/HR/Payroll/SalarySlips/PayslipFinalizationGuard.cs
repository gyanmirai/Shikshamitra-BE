using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipFinalizationGuard(IRepository<PayrollRunReview, Guid> reviews,
	IRepository<PayrollPreviewRun, Guid> runs, PayrollPreviewReviewGuard guard,
	SalarySlipSnapshotReader snapshots) : ITransientDependency
{
	public async Task EnsureReviewedAsync(SalarySlip slip)
	{
		var review = await reviews.FindAsync(x => x.EmployeeId == slip.EmployeeId && x.MonthYear == slip.MonthYear);
		if (review?.PreviewRunId == null) throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock);
		var run = await runs.GetAsync(review.PreviewRunId.Value);
		await guard.EnsureCurrentAsync(run.Id, slip.MonthYear, run.AcademicYearId, [slip.EmployeeId]);
		await guard.EnsureReviewedAsync(run.Id, [slip.EmployeeId]);
		var snapshot = await snapshots.GetAsync(slip.EmployeeId, slip.MonthYear)
			?? throw new BusinessException(HRErrorCodes.PayrollSnapshotRequiredForSalarySlip);
		var byEmployee = new Dictionary<Guid, PayrollRunItemSnapshot> { [slip.EmployeeId] = snapshot };
		PayrollLockPreconditionGuard.EnsureReviewed([slip.EmployeeId], byEmployee, slip.MonthYear);
		PayrollLockPreconditionGuard.EnsureNoBlockingSetupIssues([slip.EmployeeId], byEmployee, slip.MonthYear);
	}
}
