using Volo.Abp.Domain.Repositories;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task SeedLeaveTypesAndPoliciesAsync()
	{
		// Check if already seeded
		if (await _leaveTypeRepository.AnyAsync())
			return;

		var currentYear = _clock.Now.Year;
		var academicYear = await _academicYearRepository
			.FirstOrDefaultAsync(y => y.Name.StartsWith(currentYear.ToString()))
			?? await _academicYearRepository.FirstOrDefaultAsync();

		if (academicYear == null)
		{
			// Skip seeding if no academic year is available.
			return;
		}

		var academicYearId = academicYear.Id;

		// Seed leave types
		var leaveTypes = new[]
		{
			("Casual Leave", "#4CAF50", (string?)null),
			("Sick Leave", "#FF9800", (string?)null),
			("Earned Leave", "#2196F3", (string?)null),
			("Maternity Leave", "#E91E63", (string?)null),
			("Paternity Leave", "#9C27B0", (string?)null),
			("Compensatory Off", "#00BCD4", (string?)null),
			("Loss of Pay", "#F44336", (string?)null)
		};

		foreach (var (type, color, description) in leaveTypes)
		{
			var leaveType = await _leaveTypeManager.CreateAsync(academicYearId, type, color, description);
			await _leaveTypeRepository.InsertAsync(leaveType, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();

		// Seed leave policies
		await SeedLeavePoliciesAsync(academicYearId);
	}

	private async Task SeedLeavePoliciesAsync(Guid academicYearId)
	{
		// Check if already seeded
		if (await _leavePolicyRepository.AnyAsync())
			return;

		// Get the leave type IDs by name
		async Task<Guid?> GetLeaveTypeIdAsync(string typeName)
		{
			var leaveType = await _leaveTypeQueryRepository.FindByTypeAsync(academicYearId, typeName);
			return leaveType?.Id;
		}

		var leavePolicies = new[]
		{
			new { PolicyName = "Standard Casual Leave Policy", LeaveTypeName = "Casual Leave", MaximumDaysPerYear = 12, PaidType = LeavePaidType.Paid, CarryForward = true, Encashable = false, RequiresApproval = true, Description = "Standard casual leave - 12 days per year, carry forward allowed" },
			new { PolicyName = "Standard Sick Leave Policy", LeaveTypeName = "Sick Leave", MaximumDaysPerYear = 7, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Sick leave - 7 days per year, no carry forward" },
			new { PolicyName = "Standard Earned Leave Policy", LeaveTypeName = "Earned Leave", MaximumDaysPerYear = 15, PaidType = LeavePaidType.Paid, CarryForward = true, Encashable = true, RequiresApproval = true, Description = "Earned/Privilege leave - 15 days per year, carry forward and encashable" },
			new { PolicyName = "Maternity Leave Policy", LeaveTypeName = "Maternity Leave", MaximumDaysPerYear = 180, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Maternity leave as per statutory requirements" },
			new { PolicyName = "Paternity Leave Policy", LeaveTypeName = "Paternity Leave", MaximumDaysPerYear = 15, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Paternity leave - 15 days" },
			new { PolicyName = "Compensatory Off Policy", LeaveTypeName = "Compensatory Off", MaximumDaysPerYear = 24, PaidType = LeavePaidType.Paid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Comp-off for working on holidays - maximum 24 days per year" },
			new { PolicyName = "Loss of Pay Policy", LeaveTypeName = "Loss of Pay", MaximumDaysPerYear = 365, PaidType = LeavePaidType.Unpaid, CarryForward = false, Encashable = false, RequiresApproval = true, Description = "Unpaid leave when all leave quota is exhausted" }
		};

		foreach (var policy in leavePolicies)
		{
			var leaveTypeId = await GetLeaveTypeIdAsync(policy.LeaveTypeName);
			if (leaveTypeId == null)
				continue;

			var leavePolicy = await _leavePolicyManager.CreateAsync(
				academicYearId,
				policy.PolicyName,
				leaveTypeId.Value,
				LeaveAccrualPolicyType.Monthly,
				policy.MaximumDaysPerYear,
				policy.PaidType,
				policy.CarryForward,
				policy.Encashable,
				policy.Encashable ? LeaveEncashmentTiming.EndOfYear : null,
				policy.RequiresApproval,
				policy.Description
			);
			await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: false);
		}

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
