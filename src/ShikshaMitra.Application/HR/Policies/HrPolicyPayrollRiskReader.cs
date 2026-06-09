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
			.Select(x => BuildPeriod(x.Key, x.Count(), "snapshot"))
			.OrderBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ThenBy(x => x.Label, StringComparer.Ordinal)
			.Take(12)
			.ToList();
		preview.LockedPayrollMonths = snapshots
			.Where(x => x.IsLocked)
			.GroupBy(x => x.MonthYear)
			.Select(x => BuildPeriod(x.Key, x.Count(), "locked"))
			.OrderBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ThenBy(x => x.Label, StringComparer.Ordinal)
			.Take(12)
			.ToList();
		preview.HasStalePayrollRisk = preview.LockedPayrollMonths.Count > 0;
	}

	private static HrPolicyAffectedPeriodDto BuildPeriod(string monthYear, int count, string status)
	{
		var (year, month) = ParseMonthYear(monthYear);
		return new HrPolicyAffectedPeriodDto
		{
			Year = year,
			Month = month,
			Label = monthYear,
			Count = count,
			Status = status
		};
	}

	private static (int Year, int Month) ParseMonthYear(string monthYear)
	{
		if (monthYear.Length >= 7 &&
		    int.TryParse(monthYear[..4], out var year) &&
		    int.TryParse(monthYear.Substring(5, 2), out var month))
		{
			return (year, month);
		}

		return (0, 0);
	}
}
