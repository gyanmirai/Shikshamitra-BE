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
	private async Task InvalidateUnlockedPayrollSnapshotsAsync(Guid employeeId)
	{
		var snapshotQuery = await _payrollRunItemSnapshotRepository.GetQueryableAsync();
		var snapshots = snapshotQuery
			.Where(x => x.EmployeeId == employeeId && !x.IsLocked)
			.ToList();
		foreach (var snapshot in snapshots)
		{
			await _payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
		}
	}

	private SeededSalaryProfile BuildSeededSalary(Employee employee)
	{
		var salaryScenario = ShouldSeedEsiFullDemoSalaryScenarios()
			? EsiFullDemoSeedCatalog.FindSalaryScenario(employee.Email)
			: null;
		var grossSalary = salaryScenario?.GrossSalary ?? ResolveSeededGrossSalary(employee);
		var seed = StableSeedHash($"{CurrentTenant.Id:N}:{employee.EmployeeCode}:{employee.Email}:{employee.Id:N}");
		return BuildSeededSalaryProfile(grossSalary, seed, salaryScenario?.SuppressVariablePay == true);
	}

	internal static SeededSalaryProfile BuildSeededSalaryProfile(
		decimal grossSalary,
		uint seed,
		bool suppressVariablePay)
	{
		var basic = RoundToNearest(grossSalary * 0.50m, 100m);
		var hra = RoundToNearest(grossSalary * 0.20m, 100m);
		var da = RoundToNearest(grossSalary * 0.10m, 100m);
		var transport = grossSalary >= 90_000m ? 5_000m : grossSalary >= 55_000m ? 2_500m : 1_600m;
		var medical = grossSalary >= 55_000m ? 2_000m : 1_250m;
		var performanceBonus = suppressVariablePay ? 0m : grossSalary switch
		{
			>= 100_000m => 8_000m + (seed % 3) * 1_000m,
			>= 70_000m => 4_000m + (seed % 3) * 1_000m,
			>= 45_000m => 2_000m + (seed % 2) * 1_000m,
			_ => seed % 2 == 0 ? 1_000m : 0m
		};
		var incentive = suppressVariablePay
			? 0m
			: seed % 4 == 0 ? 2_000m : seed % 5 == 0 ? 1_000m : 0m;
		var specialAllowance = grossSalary - basic - hra - da - transport - medical;
		if (specialAllowance < 0)
		{
			specialAllowance = 0;
		}

		var pf = RoundToNearest(basic * 0.12m, 1m);
		var professionalTax = grossSalary >= 12_000m ? 200m : 0m;
		var tds = grossSalary switch
		{
			>= 120_000m => 12_000m,
			>= 100_000m => 8_000m,
			>= 80_000m => 5_000m,
			>= 60_000m => 2_500m,
			>= 50_000m => 1_000m,
			_ => 0m
		};
		var loan = seed % 7 == 0 ? 1_500m : 0m;
		var advance = seed % 11 == 0 ? 1_000m : 0m;

		return new SeededSalaryProfile(
			grossSalary,
			new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
			{
				["Basic Salary"] = basic,
				["House Rent Allowance (HRA)"] = hra,
				["Dearness Allowance (DA)"] = da,
				["Transport Allowance"] = transport,
				["Medical Allowance"] = medical,
				["Special Allowance"] = specialAllowance,
				["Performance Bonus"] = performanceBonus,
				["Incentive"] = incentive
			},
			new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
			{
				["Provident Fund (PF)"] = pf,
				["Professional Tax"] = professionalTax,
				["Income Tax (TDS)"] = tds,
				["Loan Repayment"] = loan,
				["Advance Salary Deduction"] = advance,
				["Loss of Pay (LOP)"] = 0m
			});
	}

	private decimal ResolveSeededGrossSalary(Employee employee)
	{
		var designation = employee.Designation ?? string.Empty;
		var department = employee.Department ?? string.Empty;
		var employeeCode = employee.EmployeeCode ?? string.Empty;
		var seed = StableSeedHash($"{CurrentTenant.Id:N}:{employeeCode}:{employee.Email}:{employee.Id:N}");

		var (min, max) = designation switch
		{
			var value when value.Contains("Principal", StringComparison.OrdinalIgnoreCase) &&
				!value.Contains("Vice", StringComparison.OrdinalIgnoreCase) => (130_000m, 150_000m),
			var value when value.Contains("Vice Principal", StringComparison.OrdinalIgnoreCase) => (95_000m, 115_000m),
			var value when value.Contains("Head", StringComparison.OrdinalIgnoreCase) => (75_000m, 95_000m),
			var value when value.Contains("Senior Teacher", StringComparison.OrdinalIgnoreCase) => (55_000m, 70_000m),
			var value when value.Contains("Teacher", StringComparison.OrdinalIgnoreCase) => (38_000m, 60_000m),
			var value when value.Contains("Administrative Manager", StringComparison.OrdinalIgnoreCase) => (70_000m, 85_000m),
			var value when value.Contains("HR Manager", StringComparison.OrdinalIgnoreCase) => (60_000m, 75_000m),
			var value when value.Contains("Finance Officer", StringComparison.OrdinalIgnoreCase) => (65_000m, 80_000m),
			var value when value.Contains("IT Administrator", StringComparison.OrdinalIgnoreCase) => (45_000m, 60_000m),
			var value when value.Contains("Office Assistant", StringComparison.OrdinalIgnoreCase) => (30_000m, 38_000m),
			var value when value.Contains("Clerk", StringComparison.OrdinalIgnoreCase) => (30_000m, 40_000m),
			_ when department.Contains("Administration", StringComparison.OrdinalIgnoreCase) => (40_000m, 60_000m),
			_ => (35_000m, 55_000m)
		};

		var steps = (int)((max - min) / 5_000m);
		return min + (seed % (uint)(steps + 1)) * 5_000m;
	}

	internal sealed record SeededSalaryProfile(
		decimal GrossSalary,
		IReadOnlyDictionary<string, decimal> Earnings,
		IReadOnlyDictionary<string, decimal> Deductions)
	{
		public decimal GetEarningAmount(string name) =>
			Earnings.TryGetValue(name, out var amount) ? amount : 0m;

		public decimal GetDeductionAmount(string name) =>
			Deductions.TryGetValue(name, out var amount) ? amount : 0m;
	}
}
