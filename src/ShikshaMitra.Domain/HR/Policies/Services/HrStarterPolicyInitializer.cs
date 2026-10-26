using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Policies;

/// <summary>
/// Called inside academic-year creation's unit of work. No IDataSeedContributor registration:
/// deployment/migrator runs must never backfill operational schools.
/// </summary>
public class HrStarterPolicyInitializer(
	ITenantRepository tenants,
	IRepository<WorkCalendarPolicy, Guid> calendars,
	IRepository<PayrollPolicy, Guid> payroll,
	IRepository<AttendancePolicy, Guid> attendance,
	IRepository<LeavePolicy, Guid> leave,
	IRepository<ShiftManagement, Guid> shifts) : DomainService
{
	[UnitOfWork(isTransactional: true)]
	public virtual async Task InitializeAsync(AcademicYear year)
	{
		if (year.TenantId is not Guid tenantId || year.GetProperty(HrStarterPolicyDefaults.YearInitializedProperty, false)) return;
		var tenant = await tenants.FindAsync(tenantId);
		if (tenant == null || !tenant.GetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, false)) return;

		using (CurrentTenant.Change(tenantId))
		{
			// Preserve an existing setup as a whole, including archived/customized policies.
			// Never promote drafts, change defaults, or recreate deliberately removed starter records.
			if (await calendars.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id) ||
				await payroll.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id) ||
				await attendance.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id) ||
				await leave.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id)) return;

			if (!tenant.GetProperty(HrStarterPolicyDefaults.ShiftInitializedProperty, false))
			{
				if (!await shifts.AnyAsync(x => x.TenantId == tenantId))
					await shifts.InsertAsync(HrStarterPolicyDefaults.Shift(tenantId));
				tenant.SetProperty(HrStarterPolicyDefaults.ShiftInitializedProperty, true);
				await tenants.UpdateAsync(tenant);
			}

			var calendar = HrStarterPolicyDefaults.Calendar(HrStarterPolicyDefaults.RecordId(year, "calendar"), year);
			// Tenant/year/kind IDs are stable retry keys, even before a marker has been saved.
			// Database PKs reject concurrent duplicate delivery; the entire creation UOW rolls back.
			await calendars.InsertAsync(calendar);
			await payroll.InsertAsync(HrStarterPolicyDefaults.Payroll(HrStarterPolicyDefaults.RecordId(year, "payroll"), year, calendar.Id));
			await attendance.InsertAsync(HrStarterPolicyDefaults.Attendance(HrStarterPolicyDefaults.RecordId(year, "attendance"), year, calendar.Id));
			await leave.InsertAsync(HrStarterPolicyDefaults.Leave(HrStarterPolicyDefaults.RecordId(year, "leave"), year,
				HrStarterPolicyDefaults.RecordId(year, "paid-rule"), HrStarterPolicyDefaults.RecordId(year, "unpaid-rule")));
			year.SetProperty(HrStarterPolicyDefaults.YearInitializedProperty, true);
		}
	}
}
