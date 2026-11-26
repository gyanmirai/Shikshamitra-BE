using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.WorkShifts;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	private static bool IsHrSetupInput(EntityEntry entry) => entry.Entity switch
	{
		AcademicYear or IdentityRole or EmployeeWorkShift or WorkCalendarPolicy or
			PayrollPolicy or AttendancePolicy or LeavePolicy or LeavePolicyRule or EarningComponent or
			DeductionComponent or SalaryStructureTemplate or SalaryStructureTemplateVersion or
			ProvidentFund or ProfessionalTax or PfCoverageConfiguration or EsiPolicy or EsiPayrollConfiguration => true,
		StatutoryRuleVersion rule => rule.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance,
		_ => false
	};

	private static FormattableString InvalidateHrSetup(Guid tenantId) => $"""
		INSERT INTO "HR_SetupConfigurationRevisions" ("TenantId", "Revision") VALUES ({tenantId}, {Guid.NewGuid()})
		ON CONFLICT ("TenantId") DO UPDATE SET "Revision" = EXCLUDED."Revision"
		""";
}
