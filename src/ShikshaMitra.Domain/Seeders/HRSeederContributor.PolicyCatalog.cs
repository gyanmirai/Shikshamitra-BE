using Volo.Abp.Domain.Repositories;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task SeedLeavePoliciesAsync()
	{
		var academicYears = (await _academicYearRepository.GetListAsync())
			.OrderBy(x => x.StartDate)
			.ToList();
		if (academicYears.Count == 0)
		{
			return;
		}

		foreach (var academicYear in academicYears)
		{
			await SeedLeavePoliciesAsync(academicYear.Id);
		}
	}

	private async Task SeedLeavePoliciesAsync(Guid academicYearId)
	{
		if (await _leavePolicyRepository.AnyAsync(x => x.AcademicYearId == academicYearId))
			return;

		static LeavePolicyRuleInput Rule(
			LeavePaidType paidType,
			int? count,
			LeaveEntitlementMode entitlementMode = LeaveEntitlementMode.Limited,
			bool carryForward = false,
			int? carryForwardLimit = null,
			bool encashable = false,
			bool allowNegative = false)
			=> new(
				paidType,
				entitlementMode,
				LeaveAccrualPolicyType.Yearly,
				count,
				carryForward,
				carryForward ? carryForwardLimit : null,
				encashable,
				encashable ? LeaveEncashmentTiming.EndOfYear : null,
				true,
				2,
				30,
				true,
				null,
				allowNegative,
				true);

		var defaultLeavePolicyAssigned = await _leavePolicyRepository.HasActiveDefaultAsync(academicYearId);

		async Task SeedPolicyAsync(string policyName, string description, params LeavePolicyRuleInput[] rules)
		{
			var leavePolicy = await _leavePolicyManager.CreateAsync(
				academicYearId,
				policyName,
				DateTime.Today,
				null,
				HrPolicyStatus.Active,
				!defaultLeavePolicyAssigned,
				rules,
				description);
			if (defaultLeavePolicyAssigned && leavePolicy.IsDefault)
			{
				leavePolicy.SetDefault(false);
			}

			defaultLeavePolicyAssigned = defaultLeavePolicyAssigned || leavePolicy.IsDefault;
			await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: false);
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		await SeedPolicyAsync(
			"Teaching Staff Leave Policy",
			"Teaching staff yearly paid and unpaid leave entitlements.",
			Rule(LeavePaidType.Paid, 24, carryForward: true, carryForwardLimit: 12, encashable: true),
			Rule(LeavePaidType.Unpaid, null, LeaveEntitlementMode.Unlimited, allowNegative: true));

		await SeedPolicyAsync(
			"Admin Staff Leave Policy",
			"Admin staff yearly paid and unpaid leave entitlements.",
			Rule(LeavePaidType.Paid, 20, carryForward: true, carryForwardLimit: 10),
			Rule(LeavePaidType.Unpaid, null, LeaveEntitlementMode.Unlimited, allowNegative: true));

		await SeedPolicyAsync(
			"Contract Staff Leave Policy",
			"Contract staff yearly paid and unpaid leave entitlements.",
			Rule(LeavePaidType.Paid, 12),
			Rule(LeavePaidType.Unpaid, null, LeaveEntitlementMode.Unlimited, allowNegative: true));

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task SeedEarningComponentsAsync()
	{
		// Check if already seeded
		if (await _earningComponentRepository.AnyAsync())
			return;

		// Positive template amounts: employee payroll resolution rejects <= 0 when line amount is omitted (see EmployeePayrollAppService).
		var earningComponents = new[]
		{
			new { Name = "Basic Salary", Description = "Base salary component", ComponentType = EarningComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)30_000m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 1 },
			new { Name = "House Rent Allowance (HRA)", Description = "Housing allowance - typically 40% of Basic", ComponentType = EarningComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)12_000m, Percentage = (decimal?)null, DependOn = ComponentDependency.Basic, VisibleInPayslip = true, DisplayOrder = 2 },
			new { Name = "Dearness Allowance (DA)", Description = "Cost of living adjustment", ComponentType = EarningComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)6_000m, Percentage = (decimal?)null, DependOn = ComponentDependency.Basic, VisibleInPayslip = true, DisplayOrder = 3 },
			new { Name = "Transport Allowance", Description = "Conveyance allowance", ComponentType = EarningComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)1600m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 4 },
			new { Name = "Medical Allowance", Description = "Medical reimbursement", ComponentType = EarningComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)1250m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 5 },
			new { Name = "Special Allowance", Description = "Additional allowance", ComponentType = EarningComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)9_000m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 6 },
			new { Name = "Performance Bonus", Description = "Variable performance-based bonus", ComponentType = EarningComponentType.Variable, CalcType = CalculationType.Amount, Amount = (decimal?)5_000m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 7 },
			new { Name = "Incentive", Description = "One-time incentive payment", ComponentType = EarningComponentType.Variable, CalcType = CalculationType.Amount, Amount = (decimal?)3_000m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 8 }
		};

		foreach (var component in earningComponents)
		{
			var earningComponent = await _earningComponentManager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.CalcType,
				component.Amount,
				component.Percentage,
				null,
				component.DependOn,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await _earningComponentRepository.InsertAsync(earningComponent, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task SeedDeductionComponentsAsync()
	{
		// Check if already seeded
		if (await _deductionComponentRepository.AnyAsync())
			return;

		var deductionComponents = new[]
		{
			new { Name = "Provident Fund (PF)", Description = "Employee PF contribution - 12% of Basic", ComponentType = DeductionComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)12m, Percentage = (decimal?)null, DependOn = ComponentDependency.Basic, VisibleInPayslip = true, DisplayOrder = 1 },
			new { Name = "Professional Tax", Description = "State professional tax", ComponentType = DeductionComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)200m, Percentage = (decimal?)null, DependOn = ComponentDependency.Gross, VisibleInPayslip = true, DisplayOrder = 2 },
			new { Name = "Income Tax (TDS)", Description = "Tax Deducted at Source", ComponentType = DeductionComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)2_500m, Percentage = (decimal?)null, DependOn = ComponentDependency.Gross, VisibleInPayslip = true, DisplayOrder = 3 },
			new { Name = "Employee State Insurance (ESI)", Description = "ESI contribution - 0.75% of Gross", ComponentType = DeductionComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)0.75m, Percentage = (decimal?)null, DependOn = ComponentDependency.Gross, VisibleInPayslip = true, DisplayOrder = 4 },
			new { Name = "Loan Repayment", Description = "Employee loan deduction", ComponentType = DeductionComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)1_500m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 5 },
			new { Name = "Advance Salary Deduction", Description = "Salary advance recovery", ComponentType = DeductionComponentType.Variable, CalcType = CalculationType.Amount, Amount = (decimal?)500m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 6 },
			new { Name = "Loss of Pay (LOP)", Description = "Deduction for unauthorized absence", ComponentType = DeductionComponentType.Fixed, CalcType = CalculationType.Amount, Amount = (decimal?)100m, Percentage = (decimal?)null, DependOn = ComponentDependency.None, VisibleInPayslip = true, DisplayOrder = 7 }
		};

		foreach (var component in deductionComponents)
		{
			var deductionComponent = await _deductionComponentManager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.CalcType,
				component.Amount,
				component.Percentage,
				null,
				component.DependOn,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await _deductionComponentRepository.InsertAsync(deductionComponent, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
