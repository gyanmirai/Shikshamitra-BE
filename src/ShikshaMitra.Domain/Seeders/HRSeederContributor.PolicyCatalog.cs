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
		var earningComponents = BuildSeedEarningComponentSpecs();
		var existingComponents = await _earningComponentRepository.GetListAsync();
		var existingByName = existingComponents
			.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
		var hasPendingChanges = false;

		foreach (var component in earningComponents)
		{
			if (existingByName.TryGetValue(component.Name, out var existingComponent))
			{
				await _earningComponentManager.UpdateAsync(
					existingComponent,
					component.Name,
					component.Description,
					component.ComponentType,
					component.CalculationType,
					component.Amount,
					component.Percentage,
					null,
					component.DependOn,
					component.VisibleInPayslip,
					component.DisplayOrder);
				if (!existingComponent.IsActive)
				{
					existingComponent.Activate();
				}

				await _earningComponentRepository.UpdateAsync(existingComponent, autoSave: false);
				hasPendingChanges = true;
				continue;
			}

			var newComponent = await _earningComponentManager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.CalculationType,
				component.Amount,
				component.Percentage,
				null,
				component.DependOn,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await _earningComponentRepository.InsertAsync(newComponent, autoSave: false);
			hasPendingChanges = true;
		}

		if (hasPendingChanges)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	private async Task SeedDeductionComponentsAsync()
	{
		var deductionComponents = BuildSeedDeductionComponentSpecs();
		var existingComponents = await _deductionComponentRepository.GetListAsync();
		var existingByName = existingComponents
			.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
		var hasPendingChanges = false;

		foreach (var component in deductionComponents)
		{
			if (existingByName.TryGetValue(component.Name, out var existingComponent))
			{
				await _deductionComponentManager.UpdateAsync(
					existingComponent,
					component.Name,
					component.Description,
					component.ComponentType,
					component.CalculationType,
					component.Amount,
					component.Percentage,
					null,
					component.DependOn,
					component.VisibleInPayslip,
					component.DisplayOrder);
				if (!existingComponent.IsActive)
				{
					existingComponent.Activate();
				}

				await _deductionComponentRepository.UpdateAsync(existingComponent, autoSave: false);
				hasPendingChanges = true;
				continue;
			}

			var newComponent = await _deductionComponentManager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.CalculationType,
				component.Amount,
				component.Percentage,
				null,
				component.DependOn,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await _deductionComponentRepository.InsertAsync(newComponent, autoSave: false);
			hasPendingChanges = true;
		}

		if (hasPendingChanges)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	internal static IReadOnlyList<SeedEarningComponentSpec> BuildSeedEarningComponentSpecs() =>
	[
		// Positive template amounts: employee payroll resolution rejects <= 0 when line amount is omitted (see EmployeePayrollAppService).
		new("Basic Salary", "Percentage of full-month gross, adjusted once for attendance", EarningComponentType.Fixed, CalculationType.Percentage, null, 60m, ComponentDependency.Gross, true, 1),
		new("House Rent Allowance (HRA)", "Housing allowance - typically 40% of Basic", EarningComponentType.Fixed, CalculationType.Amount, 12_000m, null, ComponentDependency.Basic, true, 2),
		new("Dearness Allowance (DA)", "Cost of living adjustment", EarningComponentType.Fixed, CalculationType.Amount, 6_000m, null, ComponentDependency.Basic, true, 3),
		new("Transport Allowance", "Conveyance allowance", EarningComponentType.Fixed, CalculationType.Amount, 1600m, null, ComponentDependency.None, true, 4),
		new("Medical Allowance", "Medical reimbursement", EarningComponentType.Fixed, CalculationType.Amount, 1250m, null, ComponentDependency.None, true, 5),
		new("Special Allowance", "Additional allowance", EarningComponentType.Fixed, CalculationType.Amount, 9_000m, null, ComponentDependency.None, true, 6),
		new("Performance Bonus", "Variable performance-based bonus", EarningComponentType.Variable, CalculationType.Amount, 5_000m, null, ComponentDependency.None, true, 7),
		new("Incentive", "One-time incentive payment", EarningComponentType.Variable, CalculationType.Amount, 3_000m, null, ComponentDependency.None, true, 8)
	];

	internal static IReadOnlyList<SeedDeductionComponentSpec> BuildSeedDeductionComponentSpecs() =>
	[
		new("Provident Fund (PF)", "Employee PF contribution - 12% of Basic", DeductionComponentType.Fixed, CalculationType.Amount, 12m, null, ComponentDependency.Basic, true, 1),
		new("Professional Tax", "State professional tax", DeductionComponentType.Fixed, CalculationType.Amount, 200m, null, ComponentDependency.Gross, true, 2),
		new("Income Tax (TDS)", "Tax Deducted at Source", DeductionComponentType.Fixed, CalculationType.Amount, 2_500m, null, ComponentDependency.Gross, true, 3),
		new("Loan Repayment", "Employee loan deduction", DeductionComponentType.Fixed, CalculationType.Amount, 1_500m, null, ComponentDependency.None, true, 5),
		new("Advance Salary Deduction", "Salary advance recovery", DeductionComponentType.Variable, CalculationType.Amount, 500m, null, ComponentDependency.None, true, 6),
		new("Loss of Pay (LOP)", "Deduction for unauthorized absence", DeductionComponentType.Fixed, CalculationType.Amount, 100m, null, ComponentDependency.None, true, 7)
	];

	internal sealed record SeedEarningComponentSpec(
		string Name,
		string Description,
		EarningComponentType ComponentType,
		CalculationType CalculationType,
		decimal? Amount,
		decimal? Percentage,
		ComponentDependency DependOn,
		bool VisibleInPayslip,
		int DisplayOrder);

	internal sealed record SeedDeductionComponentSpec(
		string Name,
		string Description,
		DeductionComponentType ComponentType,
		CalculationType CalculationType,
		decimal? Amount,
		decimal? Percentage,
		ComponentDependency DependOn,
		bool VisibleInPayslip,
		int DisplayOrder);
}
