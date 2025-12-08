using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds default HR module data for new tenants.
/// Uses constructor injection to preserve tenant context from parent seeder.
/// </summary>
public class HRSeederContributor : BaseSeederContributor
{
	private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
	private readonly LeaveTypeManager _leaveTypeManager;
	private readonly IRepository<EarningComponent, Guid> _earningComponentRepository;
	private readonly EarningComponentManager _earningComponentManager;
	private readonly IRepository<DeductionComponent, Guid> _deductionComponentRepository;
	private readonly DeductionComponentManager _deductionComponentManager;
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly LeavePolicyManager _leavePolicyManager;

	public HRSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<LeaveType, Guid> leaveTypeRepository,
		LeaveTypeManager leaveTypeManager,
		IRepository<EarningComponent, Guid> earningComponentRepository,
		EarningComponentManager earningComponentManager,
		IRepository<DeductionComponent, Guid> deductionComponentRepository,
		DeductionComponentManager deductionComponentManager,
		IRepository<LeavePolicy, Guid> leavePolicyRepository,
		LeavePolicyManager leavePolicyManager)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_leaveTypeRepository = leaveTypeRepository;
		_leaveTypeManager = leaveTypeManager;
		_earningComponentRepository = earningComponentRepository;
		_earningComponentManager = earningComponentManager;
		_deductionComponentRepository = deductionComponentRepository;
		_deductionComponentManager = deductionComponentManager;
		_leavePolicyRepository = leavePolicyRepository;
		_leavePolicyManager = leavePolicyManager;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// Seed default leave types
		await SeedLeaveTypesAsync();

		// Seed default earning components
		await SeedEarningComponentsAsync();

		// Seed default deduction components
		await SeedDeductionComponentsAsync();

		// Seed default leave policies
		await SeedLeavePoliciesAsync();
	}

	private async Task SeedLeaveTypesAsync()
	{
		// Check if already seeded
		if (await _leaveTypeRepository.AnyAsync())
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
			var leaveType = await _leaveTypeManager.CreateAsync(type, color);
			await _leaveTypeRepository.InsertAsync(leaveType, autoSave: true);
		}
	}

	private async Task SeedEarningComponentsAsync()
	{
		// Check if already seeded
		if (await _earningComponentRepository.AnyAsync())
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
			var earningComponent = await _earningComponentManager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.DependOn,
				component.Amount,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await _earningComponentRepository.InsertAsync(earningComponent, autoSave: true);
		}
	}

	private async Task SeedDeductionComponentsAsync()
	{
		// Check if already seeded
		if (await _deductionComponentRepository.AnyAsync())
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
			var deductionComponent = await _deductionComponentManager.CreateAsync(
				component.Name,
				component.Description,
				component.ComponentType,
				component.DependOn,
				component.Amount,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
			await _deductionComponentRepository.InsertAsync(deductionComponent, autoSave: true);
		}
	}

	private async Task SeedLeavePoliciesAsync()
	{
		// Check if already seeded
		if (await _leavePolicyRepository.AnyAsync())
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
			var leavePolicy = await _leavePolicyManager.CreateAsync(
				policy.PolicyName,
				policy.LeaveType,
				policy.MaximumDaysPerYear,
				policy.PaidType,
				policy.CarryForward,
				policy.Encashable,
				policy.RequiresApproval,
				policy.Description
			);
			await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: true);
		}
	}
}
