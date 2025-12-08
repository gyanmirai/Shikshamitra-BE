using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds default HR module data for new tenants
/// </summary>
public class HRSeederContributor : IDataSeedContributor, ITransientDependency
{
	private readonly IServiceProvider _serviceProvider;

	public HRSeederContributor(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public async Task SeedAsync(DataSeedContext context)
	{
		using (var scope = _serviceProvider.CreateScope())
		{
			// Seed default leave types
			await SeedLeaveTypesAsync(scope.ServiceProvider);

			// Seed default earning components
			await SeedEarningComponentsAsync(scope.ServiceProvider);

			// Seed default deduction components
			await SeedDeductionComponentsAsync(scope.ServiceProvider);

			// Seed default leave policies
			await SeedLeavePoliciesAsync(scope.ServiceProvider);
		}
	}

	private async Task SeedLeaveTypesAsync(IServiceProvider serviceProvider)
	{
		var repository = serviceProvider.GetRequiredService<IRepository<LeaveType, Guid>>();
		var manager = serviceProvider.GetRequiredService<LeaveTypeManager>();

		// Check if already seeded
		if (await repository.AnyAsync())
			return;

		var leaveTypes = new[]
		{
			("Casual Leave", "#4CAF50"),
			("Sick Leave", "#FF9800"),
			("Earned Leave", "#2196F3"),
			("Maternity Leave", "#E91E63"),
			("Paternity Leave", "#9C27B0"),
			("Compensatory Off", "#00BCD4"),
			("Loss of Pay", "#F44336")
		};

		foreach (var (type, color) in leaveTypes)
		{
			var leaveType = await manager.CreateAsync(type, color);
			await repository.InsertAsync(leaveType, autoSave: true);
		}
	}

	private async Task SeedEarningComponentsAsync(IServiceProvider serviceProvider)
	{
		var repository = serviceProvider.GetRequiredService<IRepository<EarningComponent, Guid>>();
		var manager = serviceProvider.GetRequiredService<EarningComponentManager>();

		// Check if already seeded
		if (await repository.AnyAsync())
			return;

		var earningComponents = new[]
		{
			new { Name = "Basic Salary", Description = "Base salary component", ComponentType = EarningComponentType.Fixed, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 1 },
			new { Name = "House Rent Allowance (HRA)", Description = "Housing allowance - typically 40% of Basic", ComponentType = EarningComponentType.Fixed, DependOn = ComponentDependency.Basic, Amount = "40", VisibleInPayslip = true, DisplayOrder = 2 },
			new { Name = "Dearness Allowance (DA)", Description = "Cost of living adjustment", ComponentType = EarningComponentType.Fixed, DependOn = ComponentDependency.Basic, Amount = "0", VisibleInPayslip = true, DisplayOrder = 3 },
			new { Name = "Transport Allowance", Description = "Conveyance allowance", ComponentType = EarningComponentType.Fixed, DependOn = ComponentDependency.None, Amount = "1600", VisibleInPayslip = true, DisplayOrder = 4 },
			new { Name = "Medical Allowance", Description = "Medical reimbursement", ComponentType = EarningComponentType.Fixed, DependOn = ComponentDependency.None, Amount = "1250", VisibleInPayslip = true, DisplayOrder = 5 },
			new { Name = "Special Allowance", Description = "Additional allowance", ComponentType = EarningComponentType.Fixed, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 6 },
			new { Name = "Performance Bonus", Description = "Variable performance-based bonus", ComponentType = EarningComponentType.Variable, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 7 },
			new { Name = "Incentive", Description = "One-time incentive payment", ComponentType = EarningComponentType.Variable, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 8 }
		};

		foreach (var component in earningComponents)
		{
			var earningComponent = await manager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.DependOn,
				component.Amount,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await repository.InsertAsync(earningComponent, autoSave: true);
		}
	}

	private async Task SeedDeductionComponentsAsync(IServiceProvider serviceProvider)
	{
		var repository = serviceProvider.GetRequiredService<IRepository<DeductionComponent, Guid>>();
		var manager = serviceProvider.GetRequiredService<DeductionComponentManager>();

		// Check if already seeded
		if (await repository.AnyAsync())
			return;

		var deductionComponents = new[]
		{
			new { Name = "Provident Fund (PF)", Description = "Employee PF contribution - 12% of Basic", ComponentType = DeductionComponentType.Fixed, DependOn = ComponentDependency.Basic, Amount = "12", VisibleInPayslip = true, DisplayOrder = 1 },
			new { Name = "Professional Tax", Description = "State professional tax", ComponentType = DeductionComponentType.Fixed, DependOn = ComponentDependency.Gross, Amount = "200", VisibleInPayslip = true, DisplayOrder = 2 },
			new { Name = "Income Tax (TDS)", Description = "Tax Deducted at Source", ComponentType = DeductionComponentType.Fixed, DependOn = ComponentDependency.Gross, Amount = "0", VisibleInPayslip = true, DisplayOrder = 3 },
			new { Name = "Employee State Insurance (ESI)", Description = "ESI contribution - 0.75% of Gross", ComponentType = DeductionComponentType.Fixed, DependOn = ComponentDependency.Gross, Amount = "0.75", VisibleInPayslip = true, DisplayOrder = 4 },
			new { Name = "Loan Repayment", Description = "Employee loan deduction", ComponentType = DeductionComponentType.Fixed, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 5 },
			new { Name = "Advance Salary Deduction", Description = "Salary advance recovery", ComponentType = DeductionComponentType.Variable, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 6 },
			new { Name = "Loss of Pay (LOP)", Description = "Deduction for unauthorized absence", ComponentType = DeductionComponentType.Fixed, DependOn = ComponentDependency.None, Amount = "0", VisibleInPayslip = true, DisplayOrder = 7 }
		};

		foreach (var component in deductionComponents)
		{
			var deductionComponent = await manager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.DependOn,
				component.Amount,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await repository.InsertAsync(deductionComponent, autoSave: true);
		}
	}

	private async Task SeedLeavePoliciesAsync(IServiceProvider serviceProvider)
	{
		var repository = serviceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var manager = serviceProvider.GetRequiredService<LeavePolicyManager>();

		// Check if already seeded
		if (await repository.AnyAsync())
			return;

		var leavePolicies = new[]
		{
			new { PolicyName = "Standard Casual Leave Policy", LeaveType = "Casual Leave", MaximumDaysPerYear = 12, PaidType = LeavePaidType.Paid, CarryForward = true, Encashable = false, RequiresApproval = true, Description = "Standard casual leave - 12 days per year, carry forward allowed" },
			new { PolicyName = "Standard Sick Leave Policy", LeaveType = "Sick Leave", MaximumDaysPerYear = 7, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Sick leave - 7 days per year, no carry forward" },
			new { PolicyName = "Standard Earned Leave Policy", LeaveType = "Earned Leave", MaximumDaysPerYear = 15, PaidType = LeavePaidType.Paid, CarryForward = true, Encashable = true, RequiresApproval = true, Description = "Earned/Privilege leave - 15 days per year, carry forward and encashable" },
			new { PolicyName = "Maternity Leave Policy", LeaveType = "Maternity Leave", MaximumDaysPerYear = 180, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Maternity leave as per statutory requirements" },
			new { PolicyName = "Paternity Leave Policy", LeaveType = "Paternity Leave", MaximumDaysPerYear = 15, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Paternity leave - 15 days" },
			new { PolicyName = "Compensatory Off Policy", LeaveType = "Compensatory Off", MaximumDaysPerYear = 24, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Comp-off for working on holidays - maximum 24 days per year" },
			new { PolicyName = "Loss of Pay Policy", LeaveType = "Loss of Pay", MaximumDaysPerYear = 365, PaidType = LeavePaidType.Unpaid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Unpaid leave when all leave quota is exhausted" }
		};

		foreach (var policy in leavePolicies)
		{
			var leavePolicy = await manager.CreateAsync(
				policy.PolicyName,
				policy.LeaveType,
				policy.MaximumDaysPerYear,
				policy.PaidType,
				policy.CarryForward,
				policy.Encashable,
				policy.RequiresApproval,
				policy.Description
			);
			await repository.InsertAsync(leavePolicy, autoSave: true);
		}
	}
}
