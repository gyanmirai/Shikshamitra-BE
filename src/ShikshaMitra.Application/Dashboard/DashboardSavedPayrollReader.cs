using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Dashboard;

public record DashboardSavedPayroll(string Month, int Employees, int Snapshots, int Reviewed, int Locked, int Blocked, DateTime? AsOfUtc);

[RemoteService(false)]
public class DashboardSavedPayrollReader(PayrollScopedEmployeeResolver employees, IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	PayrollAcademicYearScopeResolver years) : ApplicationService
{
	public async Task<DashboardSavedPayroll> ReadAsync(string month, Guid? academicYearId = null)
	{
		var (year, number, normalized) = PayrollRunRules.ParseMonth(month);
		await years.ResolveAsync(academicYearId, year, number);
		var ids = (await employees.GetScopedPayrollEmployeesAsync(year, number, false)).Select(x => x.Id).ToArray();
		var query = (await snapshots.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.MonthYear == normalized && Enumerable.Contains(ids, x.EmployeeId));
		var rows = await AsyncExecuter.ToListAsync(query.Select(x => new { x.IsLocked, x.ReviewStatus, x.IssueCodesJson, AsOf = x.LastModificationTime ?? x.CreationTime }));
		return new(normalized, ids.Length, rows.Count, rows.Count(x => !x.IsLocked && x.ReviewStatus == "REVIEWED"),
			rows.Count(x => x.IsLocked || x.ReviewStatus == "LOCKED"), rows.Count(x => !x.IsLocked && PayrollReadinessProjector.HasBlockingLockIssue(x.IssueCodesJson)),
			rows.Count == 0 ? null : DateTime.SpecifyKind(rows.Min(x => x.AsOf), DateTimeKind.Utc));
	}
}
