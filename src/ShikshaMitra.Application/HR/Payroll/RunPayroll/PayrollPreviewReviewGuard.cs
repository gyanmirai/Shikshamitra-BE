using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPreviewReviewGuard(PayrollPreviewRunAppService previews, IPayrollInputRevisionStore revisions,
	IRepository<PayrollRunReview, Guid> reviews, IRepository<PayrollPreviewEmployee, Guid> employees) : ITransientDependency
{
	public async Task EnsureCurrentAsync(Guid previewRunId, string month, Guid? academicYearId, IReadOnlyCollection<Guid>? employeeIds)
	{
		if (previewRunId == Guid.Empty || employeeIds == null || employeeIds.Count == 0 || employeeIds.Any(x => x == Guid.Empty))
			throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
		var revision = await revisions.LockAsync();
		var run = await previews.GetAuthorizedRunAsync(previewRunId);
		var (year, monthNumber, _) = PayrollRunRules.ParseMonth(month);
		if (run.Month != $"{year:0000}-{monthNumber:00}" || run.AcademicYearId != academicYearId ||
			run.Status != "SUCCEEDED" || revision != run.InputRevision)
			throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
		var ids = employeeIds.Distinct().ToList();
		if (await employees.CountAsync(x => x.RunId == previewRunId && ids.Contains(x.EmployeeId)) != ids.Count)
			throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
	}

	public async Task EnsureReviewedAsync(Guid previewRunId, IReadOnlyCollection<Guid> employeeIds)
	{
		var ids = employeeIds.Distinct().ToList();
		if (await reviews.CountAsync(x => x.PreviewRunId == previewRunId && ids.Contains(x.EmployeeId)) != ids.Count)
			throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock);
	}
}
