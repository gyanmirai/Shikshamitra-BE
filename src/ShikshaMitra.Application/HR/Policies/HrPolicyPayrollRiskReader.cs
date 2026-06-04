using ShikshaMitra.HR.Payroll.RunPayroll;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyPayrollRiskReader(
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task FillPayrollRiskAsync(HrPolicyImpactPreviewDto preview, string policyType, Guid? policyId)
	{
		if (!policyId.HasValue)
		{
			return;
		}

		var query = await payrollSnapshotRepository.GetQueryableAsync();
		var snapshots = await asyncExecuter.ToListAsync(query.Where(x =>
			(policyType == "payroll" && x.PayrollPolicyId == policyId.Value) ||
			(policyType == "attendance" && x.AttendancePolicyId == policyId.Value) ||
			(policyType == "work_calendar" && x.WorkCalendarPolicyId == policyId.Value)));

		preview.AffectedPayrollMonths = snapshots
			.GroupBy(x => x.MonthYear)
			.Select(x => new HrPolicyAffectedPeriodDto { Label = x.Key, Count = x.Count(), Status = "snapshot" })
			.Take(12)
			.ToList();
		preview.LockedPayrollMonths = snapshots
			.Where(x => x.IsLocked)
			.GroupBy(x => x.MonthYear)
			.Select(x => new HrPolicyAffectedPeriodDto { Label = x.Key, Count = x.Count(), Status = "locked" })
			.Take(12)
			.ToList();
		preview.HasStalePayrollRisk = preview.LockedPayrollMonths.Count > 0;
	}
}
