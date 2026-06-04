using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<PayrollPolicy?> EnsurePayrollPolicyAsync(
		AcademicYear academicYear,
		WorkCalendarPolicy workCalendar,
		string name,
		bool isDefault,
		DayCalculationType dayCalculationType,
		int? manualDays,
		IReadOnlyList<DayCalculationManualMonth> manualMonthDays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		var existing = await _payrollPolicyRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.Name == name);
		if (existing != null)
		{
			if (isDefault && !await _payrollPolicyRepository.AnyAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active))
			{
				if (existing.Status == HrPolicyStatus.Active)
				{
					existing.SetDefault(true);
				}
				else
				{
					existing.Update(
						workCalendar.Id,
						name,
						HrPolicyStatus.Active,
						isDefault: true,
						dayCalculationType,
						manualDays,
						manualMonthDays,
						saturdayWorkPolicy,
						holidayOverrides,
						lockedMonthSettings: Array.Empty<PayrollLockedMonthSetting>());
				}

				await _payrollPolicyRepository.UpdateAsync(existing, autoSave: false);
			}

			return existing;
		}

		if (isDefault && await _payrollPolicyRepository.AnyAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active))
		{
			isDefault = false;
		}

		var policy = new PayrollPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			workCalendar.Id,
			name,
			HrPolicyStatus.Active,
			isDefault,
			dayCalculationType,
			manualDays,
			manualMonthDays,
			saturdayWorkPolicy,
			holidayOverrides,
			lockedMonthSettings: Array.Empty<PayrollLockedMonthSetting>());
		await _payrollPolicyRepository.InsertAsync(policy, autoSave: false);
		return policy;
	}
}
