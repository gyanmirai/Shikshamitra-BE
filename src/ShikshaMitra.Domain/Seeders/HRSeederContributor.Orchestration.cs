using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	public override async Task SeedAsync(DataSeedContext context)
	{
		_logger.LogInformation("[HRSeeder] Start seeding for TenantId={TenantId}", CurrentTenant.Id);

		await SeedPayrollCatalogAsync();
		await SeedEmployeeFoundationAsync();
		var activeEmployeeIds = await SeedEmployeesAndPoliciesAsync();
		await SeedPayrollAttendanceAndLeaveSamplesAsync(activeEmployeeIds);
		await SeedPerformanceSamplesAsync(activeEmployeeIds);

		_logger.LogInformation("[HRSeeder] Finished seeding for TenantId={TenantId}", CurrentTenant.Id);
	}

	private async Task SeedPayrollCatalogAsync()
	{
		await SeedEarningComponentsAsync();
		await SeedDeductionComponentsAsync();
		await SeedPayrollSettingsAsync();
		_logger.LogInformation("[HRSeeder] Phase 1 complete (payroll components)");
	}

	private async Task SeedEmployeeFoundationAsync()
	{
		await SeedShiftManagementAsync();
		_logger.LogInformation("[HRSeeder] Phase 2 complete (shift management)");

		await SeedLeavePoliciesAsync();
		_logger.LogInformation("[HRSeeder] Phase 3 complete (leave types/policies)");
	}

	private async Task<List<Guid>> SeedEmployeesAndPoliciesAsync()
	{
		var seededEmployeeIds = await SeedEmployeesAsync();
		_logger.LogInformation("[HRSeeder] Phase 4 complete (employees seeded={Count})", seededEmployeeIds.Count);

		await EnsureSeededEmployeeCompletenessAsync();
		_logger.LogInformation("[HRSeeder] Phase 5 complete (employee completeness backfill)");

		var activeEmployeeIds = await EnsureEmployeeStatusDistributionAsync(seededEmployeeIds);
		_logger.LogInformation("[HRSeeder] Phase 6 complete (active employees={Count})", activeEmployeeIds.Count);

		await SeedDemoHrPolicyAssignmentsAsync(activeEmployeeIds);
		_logger.LogInformation("[HRSeeder] Phase 7 complete (HR policy assignments)");

		return activeEmployeeIds;
	}

	private async Task SeedPayrollAttendanceAndLeaveSamplesAsync(List<Guid> activeEmployeeIds)
	{
		var samplePayrollEmployeeIds = await SeedSampleEmployeePayrollAndAttendanceAsync(activeEmployeeIds);
		if (ShouldSeedSampleLeaveScenarios())
		{
			await SeedSampleLeaveScenariosAsync(samplePayrollEmployeeIds);
		}

		_logger.LogInformation("[HRSeeder] Phase 8 complete (sample payroll/attendance/leaves)");
	}

	private async Task SeedPerformanceSamplesAsync(List<Guid> activeEmployeeIds)
	{
		if (!ShouldSeedPerformanceManagementDemoData())
		{
			return;
		}

		await SeedPerformanceManagementDemoDataAsync(activeEmployeeIds);
		_logger.LogInformation("[HRSeeder] Phase 9 complete (performance management demo data)");
	}
}
