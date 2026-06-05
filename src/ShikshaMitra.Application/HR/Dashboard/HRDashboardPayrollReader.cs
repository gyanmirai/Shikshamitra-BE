using ShikshaMitra.HR.Payroll.RunPayroll;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardPayrollReader(IRunPayrollAppService runPayrollAppService) : ITransientDependency
{
	public async Task<HRDashboardPayrollDto> BuildPayrollAsync(string month)
	{
		RunPayrollReadinessSummary readiness;
		try
		{
			readiness = await runPayrollAppService.GetReadinessAsync(month);
		}
		catch (BusinessException ex)
		{
			var diagnostic = HRDashboardRefreshErrorFormatter.Format(ex);
			return new HRDashboardPayrollDto
			{
				Month = month,
				CanPreview = false,
				CanLock = false,
				BlockingIssues = ["Payroll readiness could not be calculated. Review payroll setup before locking this month."],
				RecommendedActions = [diagnostic, "Review payroll configuration."]
			};
		}

		return new HRDashboardPayrollDto
		{
			Month = readiness.Month,
			CanPreview = readiness.CanPreview,
			CanLock = readiness.CanLock,
			EmployeeCount = readiness.EmployeeCount,
			ReviewedCount = readiness.ReviewedCount,
			LockedCount = readiness.LockedCount,
			PendingCount = Math.Max(0, readiness.EmployeeCount - readiness.ReviewedCount - readiness.LockedCount),
			MissingAttendanceCount = readiness.MissingAttendanceCount,
			MissingSalaryStructureCount = readiness.MissingSalaryStructureCount,
			PolicyConfigurationIssueCount = readiness.PolicyConfigurationIssueCount,
			BlockingIssues = readiness.BlockingIssues,
			RecommendedActions = readiness.RecommendedActions
		};
	}
}
