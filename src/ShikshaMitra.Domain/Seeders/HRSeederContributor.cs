using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds default HR module data for new tenants.
/// Uses constructor injection to preserve tenant context from parent seeder.
/// </summary>
public class HRSeederContributor : BaseSeederContributor
{
	private const int ProfileSeedImageCount = 8;
	private const int SampleEmployeePayrollAttendanceCount = 5;
	private const int SampleAttendanceDayWindow = 14;

	private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
	private readonly LeaveTypeManager _leaveTypeManager;
	private readonly IRepository<EarningComponent, Guid> _earningComponentRepository;
	private readonly EarningComponentManager _earningComponentManager;
	private readonly IRepository<DeductionComponent, Guid> _deductionComponentRepository;
	private readonly DeductionComponentManager _deductionComponentManager;
	private readonly IProvidentFundRepository _providentFundRepository;
	private readonly IProfessionalTaxRepository _professionalTaxRepository;
	private readonly IDayCalculationSettingRepository _dayCalculationSettingRepository;
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly LeavePolicyManager _leavePolicyManager;
	private readonly ILeaveTypeRepository _leaveTypeQueryRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeeFamilyMemberRepository _familyMemberRepository;
	private readonly EmployeeManager _employeeManager;
	private readonly EmployeeDocumentManager _employeeDocumentManager;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly ILookupNormalizer _lookupNormalizer;
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly EmployeeLeaveManager _employeeLeaveManager;
	private readonly IEmployeePayrollDetailsRepository _employeePayrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _employeePayrollComponentRepository;
	private readonly ILogger<HRSeederContributor> _logger;

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
		IProvidentFundRepository providentFundRepository,
		IProfessionalTaxRepository professionalTaxRepository,
		IDayCalculationSettingRepository dayCalculationSettingRepository,
		IRepository<LeavePolicy, Guid> leavePolicyRepository,
		LeavePolicyManager leavePolicyManager,
		ILeaveTypeRepository leaveTypeQueryRepository,
		IAcademicYearRepository academicYearRepository,
		IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
			IRepository<ShiftManagement, Guid> shiftRepository,
			IEmployeeRepository employeeRepository,
			IEmployeeFamilyMemberRepository familyMemberRepository,
			EmployeeManager employeeManager,
			EmployeeDocumentManager employeeDocumentManager,
			IIdentityRoleRepository roleRepository,
			IdentityUserManager userManager,
			IIdentityUserRepository userRepository,
			ILookupNormalizer lookupNormalizer,
			IEmployeeAttendanceRepository employeeAttendanceRepository,
			IEmployeeLeaveRepository employeeLeaveRepository,
			EmployeeLeaveManager employeeLeaveManager,
			IEmployeePayrollDetailsRepository employeePayrollDetailsRepository,
			IEmployeePayrollComponentRepository employeePayrollComponentRepository,
			ILogger<HRSeederContributor> logger)
			: base(currentTenant, guidGenerator, unitOfWork)
	{
		_leaveTypeRepository = leaveTypeRepository;
		_leaveTypeManager = leaveTypeManager;
		_earningComponentRepository = earningComponentRepository;
		_earningComponentManager = earningComponentManager;
		_deductionComponentRepository = deductionComponentRepository;
		_deductionComponentManager = deductionComponentManager;
		_providentFundRepository = providentFundRepository;
		_professionalTaxRepository = professionalTaxRepository;
		_dayCalculationSettingRepository = dayCalculationSettingRepository;
		_leavePolicyRepository = leavePolicyRepository;
		_leavePolicyManager = leavePolicyManager;
		_leaveTypeQueryRepository = leaveTypeQueryRepository;
		_academicYearRepository = academicYearRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_shiftRepository = shiftRepository;
		_employeeRepository = employeeRepository;
		_familyMemberRepository = familyMemberRepository;
		_employeeManager = employeeManager;
		_employeeDocumentManager = employeeDocumentManager;
		_roleRepository = roleRepository;
		_userManager = userManager;
		_userRepository = userRepository;
		_lookupNormalizer = lookupNormalizer;
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_employeeLeaveRepository = employeeLeaveRepository;
		_employeeLeaveManager = employeeLeaveManager;
		_employeePayrollDetailsRepository = employeePayrollDetailsRepository;
		_employeePayrollComponentRepository = employeePayrollComponentRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		_logger.LogInformation("[HRSeeder] Start seeding for TenantId={TenantId}", CurrentTenant.Id);

		// Phase 1: Seed payroll components
		await SeedEarningComponentsAsync();
		await SeedDeductionComponentsAsync();
		await SeedPayrollSettingsAsync();
		_logger.LogInformation("[HRSeeder] Phase 1 complete (payroll components)");

		// Phase 2: Seed shift management (required for employees)
		await SeedShiftManagementAsync();
		_logger.LogInformation("[HRSeeder] Phase 2 complete (shift management)");

		// Phase 3: Seed leave types and policies
		await SeedLeaveTypesAndPoliciesAsync();
		_logger.LogInformation("[HRSeeder] Phase 3 complete (leave types/policies)");

		// Phase 4: Seed employees
		var seededEmployeeIds = await SeedEmployeesAsync();
		_logger.LogInformation("[HRSeeder] Phase 4 complete (employees seeded={Count})", seededEmployeeIds.Count);

		// Phase 5: Sample per-employee payroll + attendance for employees seeded in this run only.
		await SeedSampleEmployeePayrollAndAttendanceAsync(seededEmployeeIds);
		await SeedSampleApprovedLeavesAsync(seededEmployeeIds);
		_logger.LogInformation("[HRSeeder] Phase 5 complete (sample payroll/attendance)");
		_logger.LogInformation("[HRSeeder] Finished seeding for TenantId={TenantId}", CurrentTenant.Id);
	}

	private async Task SeedLeaveTypesAndPoliciesAsync()
	{
		// Check if already seeded
		if (await _leaveTypeRepository.AnyAsync())
			return;

		var currentYear = DateTime.Now.Year;
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
			await _leaveTypeRepository.InsertAsync(leaveType, autoSave: true);
		}

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
			await _leavePolicyRepository.InsertAsync(leavePolicy, autoSave: true);
		}
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
			await _deductionComponentRepository.InsertAsync(deductionComponent, autoSave: true);
		}
	}

	private async Task SeedPayrollSettingsAsync()
	{
		if (!await _providentFundRepository.IsCodeExistsAsync("STD-PF"))
		{
			await _providentFundRepository.InsertAsync(
				new ProvidentFund(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					"STD-PF",
					"Standard Provident Fund",
					employeePercentage: 12,
					employerPercentage: 12),
				autoSave: true);
		}

		var professionalTaxSlabs = new[]
		{
			new { Gender = GenderType.Male, From = 0d, To = 5_999.99d, Amount = 0d },
			new { Gender = GenderType.Male, From = 6_000d, To = 8_999.99d, Amount = 80d },
			new { Gender = GenderType.Male, From = 9_000d, To = 11_999.99d, Amount = 150d },
			new { Gender = GenderType.Male, From = 12_000d, To = 999_999d, Amount = 200d },
			new { Gender = GenderType.Female, From = 0d, To = 5_999.99d, Amount = 0d },
			new { Gender = GenderType.Female, From = 6_000d, To = 8_999.99d, Amount = 80d },
			new { Gender = GenderType.Female, From = 9_000d, To = 11_999.99d, Amount = 150d },
			new { Gender = GenderType.Female, From = 12_000d, To = 999_999d, Amount = 200d }
		};

		foreach (var slab in professionalTaxSlabs)
		{
			if (await _professionalTaxRepository.AnyAsync(x =>
				    x.GenderType == slab.Gender &&
				    x.FromSalary == slab.From &&
				    x.ToSalary == slab.To))
			{
				continue;
			}

			await _professionalTaxRepository.InsertAsync(
				new ProfessionalTax(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					slab.Gender,
					slab.From,
					slab.To,
					slab.Amount),
				autoSave: false);
		}

		if (await _dayCalculationSettingRepository.FindByTenantAsync(CurrentTenant.Id) == null)
		{
			await _dayCalculationSettingRepository.InsertAsync(
				new DayCalculationSetting(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					DayCalculationType.WorkingDays,
					manualDays: null),
				autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task SeedShiftManagementAsync()
	{
		// Check if already seeded
		if (await _shiftRepository.AnyAsync())
			return;

		var shifts = new[]
		{
						new { Name = "Morning Shift", StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(12, 30) },
						new { Name = "Afternoon Shift", StartTime = new TimeOnly(12, 30), EndTime = new TimeOnly(17, 30) },
						new { Name = "Full Day Shift", StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(17, 30) }
					};

		foreach (var shift in shifts)
		{
			var id = GuidGenerator.Create();
			var shiftEntity = new ShiftManagement
			{
				ShiftName = shift.Name,
				StartTime = shift.StartTime,
				EndTime = shift.EndTime
			};
			// Set the Id using reflection since there's no constructor
			typeof(ShiftManagement).GetProperty("Id").SetValue(shiftEntity, id);
			await _shiftRepository.InsertAsync(shiftEntity, autoSave: true);
		}
	}

	private async Task<List<Guid>> SeedEmployeesAsync()
	{
		var seededEmployeeIds = new List<Guid>();

		// Check if already seeded
		if (await _employeeRepository.AnyAsync())
			return seededEmployeeIds;

		// Get required dependencies
		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
		if (educationMediumStage == null)
			return seededEmployeeIds; // Skip if no academic setup

		var fullDayShift = (await _shiftRepository.GetQueryableAsync())
			.FirstOrDefault(s => s.ShiftName == "Full Day Shift");

		// Seed teaching staff
		await SeedTeachingStaffAsync(educationMediumStage.Id, fullDayShift?.Id, seededEmployeeIds);

		// Seed non-teaching staff
		await SeedNonTeachingStaffAsync(educationMediumStage.Id, fullDayShift?.Id, seededEmployeeIds);

		return seededEmployeeIds;
	}

	/// <summary>
	/// Demo payroll lines + attendance for the first N employees (by employee code). Skips employees that already have payroll components.
	/// Attendance: last ~14 calendar days, weekdays only; idempotent per employee per day.
	/// </summary>
	private async Task SeedSampleEmployeePayrollAndAttendanceAsync(List<Guid> seededEmployeeIds)
	{
		if (seededEmployeeIds.Count == 0)
		{
			return;
		}

		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
		if (educationMediumStage == null)
		{
			return;
		}

		if (!await _earningComponentRepository.AnyAsync() || !await _deductionComponentRepository.AnyAsync())
		{
			return;
		}

		var employeeIds = seededEmployeeIds
			.Take(SampleEmployeePayrollAttendanceCount)
			.ToList();
		if (employeeIds.Count == 0)
		{
			return;
		}

		foreach (var employeeId in employeeIds)
		{
			await SeedSamplePayrollForEmployeeIfNeededAsync(employeeId);
		}

		await ActivateEmployeesWithPayrollStructureAsync(employeeIds);

		await SeedSampleAttendanceForEmployeesAsync(
			employeeIds,
			educationMediumStage.Id);
	}

	private async Task ActivateEmployeesWithPayrollStructureAsync(List<Guid> employeeIds)
	{
		foreach (var employeeId in employeeIds)
		{
			var employee = await _employeeRepository.FindAsync(employeeId);
			if (employee == null || employee.EmployeeStatusType == EmployeeStatusType.Active)
			{
				continue;
			}

			var payrollComponents = await _employeePayrollComponentRepository.GetByEmployeeIdAsync(employeeId);
			if (payrollComponents.Count == 0)
			{
				continue;
			}

			var updated = await _employeeManager.UpdateEmployeeStatusAsync(
				employeeId,
				EmployeeStatusType.Active,
				"Auto-activated by HR seeder after payroll structure seed");

			await _employeeRepository.UpdateAsync(updated, autoSave: true);
		}
	}

	private async Task SeedSamplePayrollForEmployeeIfNeededAsync(Guid employeeId)
	{
		var hasPendingChanges = false;
		var existingComponents = await _employeePayrollComponentRepository.GetByEmployeeIdAsync(employeeId);
		if (existingComponents.Count > 0)
		{
			_logger.LogDebug("[HRSeeder] Skip payroll seed for EmployeeId={EmployeeId} (already exists)", employeeId);
			return;
		}

		var payrollDetails = await _employeePayrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (payrollDetails == null)
		{
			var details = new EmployeePayrollDetails(GuidGenerator.Create(), employeeId);
			details.UpdateDetails(
				bankName: "State Bank of India",
				branchName: "Mumbai Main",
				accountHolderName: "Demo Account Holder",
				accountNumber: "12345678",
				ifscCode: "SBIN0001234",
				pfRule: null,
				epfNumber: null,
				uanNumber: null,
				punchId: null,
				totalWorkingHourPerMonth: 160m);
			await _employeePayrollDetailsRepository.InsertAsync(details, autoSave: false);
			hasPendingChanges = true;
		}

		var earningMasters = (await _earningComponentRepository.GetListAsync())
			.Where(e => e.IsActive)
			.OrderBy(e => e.DisplayOrder)
			.ToList();
		var deductionMasters = (await _deductionComponentRepository.GetListAsync())
			.Where(d => d.IsActive)
			.OrderBy(d => d.DisplayOrder)
			.ToList();

		foreach (var master in earningMasters)
		{
			var amount = master.Amount ?? 0m;
			if (amount <= 0m)
			{
				continue;
			}

			var isFixed = master.CalculationType == CalculationType.Amount;
			var row = new EmployeePayrollComponent(
				GuidGenerator.Create(),
				employeeId,
				master.Name,
				master.Id,
				master.Description,
				amount,
				isFixed,
				isTemp: false,
				applicableMonth: null,
				master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapSalaryDependency(master.DependOn),
				isEarning: true);
			await _employeePayrollComponentRepository.InsertAsync(row, autoSave: false);
			hasPendingChanges = true;
		}

		foreach (var master in deductionMasters)
		{
			var amount = master.Amount ?? 0m;
			if (amount <= 0m)
			{
				continue;
			}

			var isFixed = master.CalculationType == CalculationType.Amount;
			var row = new EmployeePayrollComponent(
				GuidGenerator.Create(),
				employeeId,
				master.Name,
				master.Id,
				master.Description,
				amount,
				isFixed,
				isTemp: false,
				applicableMonth: null,
				master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapSalaryDependency(master.DependOn),
				isEarning: false);
			await _employeePayrollComponentRepository.InsertAsync(row, autoSave: false);
			hasPendingChanges = true;
		}

		if (hasPendingChanges)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	private static SalaryComponentDependency? MapSalaryDependency(ComponentDependency dependOn)
	{
		return dependOn switch
		{
			ComponentDependency.None => SalaryComponentDependency.None,
			ComponentDependency.Basic => SalaryComponentDependency.Basic,
			ComponentDependency.Gross => SalaryComponentDependency.Gross,
			_ => null
		};
	}

	private async Task SeedSampleAttendanceForEmployeesAsync(List<Guid> employeeIds, Guid educationMediumStageId)
	{
		if (employeeIds.Count == 0)
		{
			return;
		}

		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var windowStart = today.AddDays(-SampleAttendanceDayWindow + 1);

		var existingRows = await _employeeAttendanceRepository.GetListAsync(a =>
			employeeIds.Contains(a.EmployeeId) &&
			a.AttendanceDate >= windowStart &&
			a.AttendanceDate <= today);
		var existingKeys = existingRows
			.Select(a => (a.EmployeeId, a.AttendanceDate))
			.ToHashSet();

		var indiaOffset = TimeSpan.FromHours(5.5);
		var workingDayIndex = 0;
		var insertedCount = 0;

		for (var date = windowStart; date <= today; date = date.AddDays(1))
		{
			if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
			{
				continue;
			}

			workingDayIndex++;
			foreach (var employeeId in employeeIds)
			{
				if (existingKeys.Contains((employeeId, date)))
				{
					continue;
				}

				var mix = (employeeId.GetHashCode() ^ date.DayOfYear) & 0x7;
				EmployeeAttendanceStatusType status;
				DateTimeOffset? timeIn = null;
				DateTimeOffset? timeOut = null;

				if (workingDayIndex % 9 == 0)
				{
					status = EmployeeAttendanceStatusType.FullDayLeave;
				}
				else if (workingDayIndex % 5 == 0 || mix == 0)
				{
					status = EmployeeAttendanceStatusType.HalfDayLeave;
					timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, indiaOffset);
					timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, 13, 30, 0, indiaOffset);
				}
				else
				{
					status = EmployeeAttendanceStatusType.Present;
					timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, indiaOffset);
					timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, 17, 30, 0, indiaOffset);
				}

				await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
				{
					TenantId = CurrentTenant.Id,
					EmployeeId = employeeId,
					AttendanceDate = date,
					TimeIn = timeIn,
					TimeOut = timeOut,
					StatusType = status,
					EducationMediumStageId = educationMediumStageId
				}, autoSave: false);
				insertedCount++;
			}
		}

		if (insertedCount > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation(
			"[HRSeeder] Attendance sample seeded rows={InsertedCount} for employees={EmployeeCount}",
			insertedCount,
			employeeIds.Count);
	}

	private async Task SeedSampleApprovedLeavesAsync(List<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return;
		}

		var currentYear = DateTime.Now.Year;
		var academicYear = await _academicYearRepository
			.FirstOrDefaultAsync(y => y.Name.StartsWith(currentYear.ToString()))
			?? await _academicYearRepository.FirstOrDefaultAsync();
		if (academicYear == null)
		{
			return;
		}

		var leavePolicies = await _leavePolicyRepository.GetListAsync(x => x.AcademicYearId == academicYear.Id);
		var casual = leavePolicies.FirstOrDefault(x => x.PolicyName.Contains("Casual", StringComparison.OrdinalIgnoreCase));
		var sick = leavePolicies.FirstOrDefault(x => x.PolicyName.Contains("Sick", StringComparison.OrdinalIgnoreCase));
		var policies = new[] { casual, sick }.Where(x => x != null).ToList();
		if (policies.Count == 0)
		{
			return;
		}

		var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
		var candidates = employeeIds.Take(Math.Min(3, employeeIds.Count)).ToList();
		var insertedCount = 0;
		for (var i = 0; i < candidates.Count; i++)
		{
			var employeeId = candidates[i];
			var policy = policies[i % policies.Count]!;
			var leaveDate = GetCurrentMonthBusinessDayNotFuture(currentMonthStart, 2 + i * 3);
			var description = $"HR base seed approved leave {i + 1}";
			if (await _employeeLeaveRepository.AnyAsync(x =>
				    x.EmployeeId == employeeId &&
				    x.Description == description))
			{
				continue;
			}

			var leave = await _employeeLeaveManager.CreateAsync(
				employeeId,
				policy.Id,
				new DateTimeOffset(leaveDate, TimeSpan.FromHours(5.5)),
				new DateTimeOffset(leaveDate, TimeSpan.FromHours(5.5)),
				description);
			await _employeeLeaveRepository.InsertAsync(leave, autoSave: true);
			await _employeeLeaveManager.UpdateLeaveRequestStatusAsync(
				leave,
				EmployeeLeaveStatusType.Approved,
				rejectionReason: null);
			insertedCount++;
		}

		_logger.LogInformation(
			"[HRSeeder] Approved leave sample seeded rows={InsertedCount}",
			insertedCount);
	}

	private static DateTime GetCurrentMonthBusinessDayNotFuture(DateTime monthStart, int preferredBusinessDayNumber)
	{
		var today = DateTime.UtcNow.Date;
		var count = 0;
		var selected = monthStart.Date;
		for (var date = monthStart.Date; date <= today; date = date.AddDays(1))
		{
			if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
			{
				continue;
			}

			count++;
			selected = date;
			if (count == preferredBusinessDayNumber)
			{
				return date;
			}
		}

		return selected;
	}

	private async Task SeedTeachingStaffAsync(Guid educationMediumStageId, Guid? shiftId, List<Guid> seededEmployeeIds)
	{
		var teachingEmployees = new[]
		{
						new
						{
							FirstName = "Rajesh",
							LastName = "Kumar",
							MiddleName = "Kumar",
							Email = "rajesh.kumar@school.edu",
							Mobile = "9876543210",
							SecondaryMobile = "9876543211",
							RoleName = "Principal",
							Designation = "Principal",
							Department = "Administration",
							DateOfBirth = new DateOnly(1975, 5, 15),
							DateOfJoining = new DateOnly(2010, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Kumar",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "B+",
							Qualification = "M.Ed, PhD in Education Management",
							Address1 = "Flat 301, Shanti Apartments",
							Address2 = "Near City Mall",
							Address3 = "Andheri West",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400001",
							IdentificationMark = "Mole on left cheek"
						},
						new
						{
							FirstName = "Priya",
							LastName = "Sharma",
							MiddleName = "Devi",
							Email = "priya.sharma@school.edu",
							Mobile = "9876543212",
							SecondaryMobile = "9876543213",
							RoleName = "Vice Principal",
							Designation = "Vice Principal",
							Department = "Administration",
							DateOfBirth = new DateOnly(1978, 8, 22),
							DateOfJoining = new DateOnly(2012, 7, 15),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Sharma",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "A+",
							Qualification = "M.Sc, M.Ed",
							Address1 = "B-204, Sunrise Heights",
							Address2 = "SV Road",
							Address3 = "Borivali West",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400002",
							IdentificationMark = "Scar on right hand"
						},
						new
						{
							FirstName = "Amit",
							LastName = "Patel",
							MiddleName = (string?)null,
							Email = "amit.patel@school.edu",
							Mobile = "9876543214",
							SecondaryMobile = (string?)null,
							RoleName = "Head of Department",
							Designation = "Head of Science Department",
							Department = "Science",
							DateOfBirth = new DateOnly(1980, 3, 10),
							DateOfJoining = new DateOnly(2013, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Patel",
							CastType = CastType.OBC,
							Religion = "Hindu",
							BloodGroup = "O+",
							Qualification = "M.Sc Physics, B.Ed",
							Address1 = "Plot 15, Shivaji Nagar",
							Address2 = "Lane 3",
							Address3 = (string?)null,
							City = "Pune",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "411001",
							IdentificationMark = "Birthmark on neck"
						},
						new
						{
							FirstName = "Sunita",
							LastName = "Desai",
							MiddleName = (string?)null,
							Email = "sunita.desai@school.edu",
							Mobile = "9876543215",
							SecondaryMobile = (string?)null,
							RoleName = "Head of Department",
							Designation = "Head of Mathematics Department",
							Department = "Mathematics",
							DateOfBirth = new DateOnly(1982, 11, 5),
							DateOfJoining = new DateOnly(2014, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Desai",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "AB+",
							Qualification = "M.Sc Mathematics, B.Ed",
							Address1 = "C-101, Ocean View",
							Address2 = "Carter Road",
							Address3 = "Bandra West",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400003",
							IdentificationMark = "Mole on forehead"
						},
						new
						{
							FirstName = "Vikram",
							LastName = "Singh",
							MiddleName = "Kumar",
							Email = "vikram.singh@school.edu",
							Mobile = "9876543216",
							SecondaryMobile = "9876543217",
							RoleName = "Teacher",
							Designation = "Senior Teacher - English",
							Department = "Languages",
							DateOfBirth = new DateOnly(1985, 1, 20),
							DateOfJoining = new DateOnly(2015, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Rajput",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "B-",
							Qualification = "M.A English, B.Ed",
							Address1 = "D-502, Royal Residency",
							Address2 = "Ghodbunder Road",
							Address3 = (string?)null,
							City = "Thane",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400601",
							IdentificationMark = "Scar on left eyebrow"
						},
						new
						{
							FirstName = "Meera",
							LastName = "Joshi",
							MiddleName = (string?)null,
							Email = "meera.joshi@school.edu",
							Mobile = "9876543218",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Chemistry",
							Department = "Science",
							DateOfBirth = new DateOnly(1987, 6, 12),
							DateOfJoining = new DateOnly(2016, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Joshi",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "O-",
							Qualification = "M.Sc Chemistry, B.Ed",
							Address1 = "E-301, Green Park",
							Address2 = "LBS Marg",
							Address3 = "Mulund West",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400004",
							IdentificationMark = "Dimple on chin"
						},
						new
						{
							FirstName = "Arjun",
							LastName = "Reddy",
							MiddleName = (string?)null,
							Email = "arjun.reddy@school.edu",
							Mobile = "9876543219",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Biology",
							Department = "Science",
							DateOfBirth = new DateOnly(1988, 9, 8),
							DateOfJoining = new DateOnly(2017, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Reddy",
							CastType = CastType.OBC,
							Religion = "Hindu",
							BloodGroup = "A-",
							Qualification = "M.Sc Biology, B.Ed",
							Address1 = "F-102, Palm Gardens",
							Address2 = "Sector 15",
							Address3 = "Vashi",
							City = "Navi Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400701",
							IdentificationMark = "Mole on right shoulder"
						},
						new
						{
							FirstName = "Kavita",
							LastName = "Nair",
							MiddleName = (string?)null,
							Email = "kavita.nair@school.edu",
							Mobile = "9876543220",
							SecondaryMobile = "9876543221",
							RoleName = "Teacher",
							Designation = "Teacher - History",
							Department = "Social Studies",
							DateOfBirth = new DateOnly(1986, 4, 25),
							DateOfJoining = new DateOnly(2016, 6, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Nair",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "B+",
							Qualification = "M.A History, B.Ed",
							Address1 = "G-201, Heritage Complex",
							Address2 = "Linking Road",
							Address3 = "Santacruz West",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400005",
							IdentificationMark = "Beauty mark on neck"
						},
						new
						{
							FirstName = "Deepak",
							LastName = "Mehta",
							MiddleName = (string?)null,
							Email = "deepak.mehta@school.edu",
							Mobile = "9876543222",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Computer Science",
							Department = "Computer Science",
							DateOfBirth = new DateOnly(1989, 7, 18),
							DateOfJoining = new DateOnly(2018, 7, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Mehta",
							CastType = CastType.General,
							Religion = "Jain",
							BloodGroup = "AB-",
							Qualification = "B.Tech CS, B.Ed",
							Address1 = "H-405, Tech Plaza",
							Address2 = "Model Colony",
							Address3 = (string?)null,
							City = "Pune",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "411002",
							IdentificationMark = "Scar on left hand"
						},
						new
						{
							FirstName = "Anjali",
							LastName = "Rao",
							MiddleName = (string?)null,
							Email = "anjali.rao@school.edu",
							Mobile = "9876543223",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Hindi",
							Department = "Languages",
							DateOfBirth = new DateOnly(1990, 2, 14),
							DateOfJoining = new DateOnly(2019, 6, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Rao",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "O+",
							Qualification = "M.A Hindi, B.Ed",
							Address1 = "I-304, Sai Darshan",
							Address2 = "Western Express Highway",
							Address3 = "Goregaon East",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400006",
							IdentificationMark = "Mole on left ear"
						}
					};

		foreach (var emp in teachingEmployees)
		{
			var role = await _roleRepository.FindByNormalizedNameAsync(emp.RoleName.ToUpperInvariant());
			if (role == null) continue;

			// Create user first
			var userId = await CreateUserForEmployeeAsync(emp.FirstName, emp.LastName, emp.Email, emp.Mobile, emp.RoleName);

			var employee = await _employeeManager.CreateEmployeeAsync(
				userId: userId,
				roleId: role.Id,
				firstName: emp.FirstName,
				lastName: emp.LastName,
				middleName: emp.MiddleName,
				shortName: $"{emp.FirstName[0]}.{emp.LastName}",
				employeeStatusType: EmployeeStatusType.Inactive,
				dateOfBirth: emp.DateOfBirth,
				familyCast: emp.FamilyCast,
				castType: emp.CastType,
				genderType: emp.Gender,
				religion: emp.Religion,
				maritalStatus: emp.MaritalStatus,
				bloodGroup: emp.BloodGroup,
				mobileNo: emp.Mobile,
				secondaryMobileNo: emp.SecondaryMobile,
				email: emp.Email,
				address1: emp.Address1,
				address2: emp.Address2,
				address3: emp.Address3,
				designation: emp.Designation,
				department: emp.Department,
				dateOfJoining: emp.DateOfJoining,
				employeeGroup: EmployeeGroupType.Teaching,
				qualification: emp.Qualification,
											identificationMark1: emp.IdentificationMark,
												city: emp.City,
												state: emp.State,
												country: emp.Country,
												pinCode: emp.PinCode,
												shiftId: shiftId
											);

									await _employeeRepository.InsertAsync(employee, autoSave: true);
									seededEmployeeIds.Add(employee.Id);
									await SeedEmployeeProfilePictureAsync(employee.Id);

									// Seed family members for married employees
									if (emp.MaritalStatus == MaritalStatusType.Married)
									{
										await SeedFamilyMembersAsync(employee.Id, emp.FirstName, emp.Gender);
									}
								}
							}

							private async Task SeedNonTeachingStaffAsync(Guid educationMediumStageId, Guid? shiftId, List<Guid> seededEmployeeIds)
	{
		var nonTeachingEmployees = new[]
		{
									new
									{
							FirstName = "Ramesh",
							LastName = "Gupta",
							MiddleName = (string?)null,
							Email = "ramesh.gupta@school.edu",
							Mobile = "9876543224",
							SecondaryMobile = "9876543225",
							RoleName = "Administrative Manager",
							Designation = "Administrative Manager",
							Department = "Administration",
							DateOfBirth = new DateOnly(1977, 10, 5),
							DateOfJoining = new DateOnly(2011, 4, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Gupta",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "A+",
							Qualification = "MBA in Administration",
							Address1 = "J-501, Vista Towers",
							Address2 = "Yari Road",
							Address3 = "Versova",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400007",
							IdentificationMark = "Mole on right cheek"
						},
						new
						{
							FirstName = "Neha",
							LastName = "Kulkarni",
							MiddleName = (string?)null,
							Email = "neha.kulkarni@school.edu",
							Mobile = "9876543226",
							SecondaryMobile = (string?)null,
							RoleName = "HR Manager",
							Designation = "HR Manager",
							Department = "Human Resources",
							DateOfBirth = new DateOnly(1983, 3, 18),
							DateOfJoining = new DateOnly(2014, 5, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Maratha",
							CastType = CastType.OBC,
							Religion = "Hindu",
							BloodGroup = "B+",
							Qualification = "MBA in HR Management",
							Address1 = "K-201, University Campus",
							Address2 = "Karve Road",
							Address3 = (string?)null,
							City = "Pune",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "411003",
							IdentificationMark = "Birthmark on left arm"
						},
						new
						{
							FirstName = "Suresh",
							LastName = "Patil",
							MiddleName = (string?)null,
							Email = "suresh.patil@school.edu",
							Mobile = "9876543227",
							SecondaryMobile = "9876543228",
							RoleName = "Finance Officer",
							Designation = "Finance Officer",
							Department = "Finance",
							DateOfBirth = new DateOnly(1979, 12, 10),
							DateOfJoining = new DateOnly(2012, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Patil",
							CastType = CastType.OBC,
							Religion = "Hindu",
							BloodGroup = "O+",
							Qualification = "M.Com, CA",
							Address1 = "L-102, Finance Plaza",
							Address2 = "Nariman Point",
							Address3 = (string?)null,
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400008",
							IdentificationMark = "Scar on chin"
						},
						new
						{
							FirstName = "Rahul",
							LastName = "Verma",
							MiddleName = (string?)null,
							Email = "rahul.verma@school.edu",
							Mobile = "9876543229",
							SecondaryMobile = (string?)null,
							RoleName = "IT Administrator",
							Designation = "IT Administrator",
							Department = "Information Technology",
							DateOfBirth = new DateOnly(1991, 5, 22),
							DateOfJoining = new DateOnly(2019, 8, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Verma",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "AB+",
							Qualification = "B.Tech IT",
							Address1 = "M-403, Silicon Heights",
							Address2 = "Dahisar East",
							Address3 = (string?)null,
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400009",
							IdentificationMark = "Mole on left wrist"
						},
						new
						{
							FirstName = "Pooja",
							LastName = "Shah",
							MiddleName = (string?)null,
							Email = "pooja.shah@school.edu",
							Mobile = "9876543230",
							SecondaryMobile = (string?)null,
							RoleName = "Office Assistant",
							Designation = "Office Assistant",
							Department = "Administration",
							DateOfBirth = new DateOnly(1992, 8, 30),
							DateOfJoining = new DateOnly(2020, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Shah",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "A-",
							Qualification = "B.Com",
							Address1 = "N-502, Hiranandani Estate",
							Address2 = "Pokhran Road",
							Address3 = (string?)null,
							City = "Thane",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400602",
							IdentificationMark = "Dimple on right cheek"
						},
						new
						{
							FirstName = "Nilesh",
							LastName = "Joshi",
							MiddleName = (string?)null,
							Email = "clerk@school.edu",
							Mobile = "9876543231",
							SecondaryMobile = (string?)null,
							RoleName = "Clerk",
							Designation = "Clerk",
							Department = "Administration",
							DateOfBirth = new DateOnly(1990, 11, 11),
							DateOfJoining = new DateOnly(2021, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Joshi",
							CastType = CastType.General,
							Religion = "Hindu",
							BloodGroup = "B+",
							Qualification = "B.A.",
							Address1 = "O-108, Admin Arcade",
							Address2 = "School Campus Road",
							Address3 = (string?)null,
							City = "Surat",
							State = new State("Gujarat", "GJ"),
							Country = new Country("India", "IN"),
							PinCode = "395010",
							IdentificationMark = "Scar on right thumb"
						}
					};

		foreach (var emp in nonTeachingEmployees)
		{
			var role = await _roleRepository.FindByNormalizedNameAsync(emp.RoleName.ToUpperInvariant());
			if (role == null) continue;

			// Create user first
			var userId = await CreateUserForEmployeeAsync(emp.FirstName, emp.LastName, emp.Email, emp.Mobile, emp.RoleName);

			var employee = await _employeeManager.CreateEmployeeAsync(
				userId: userId,
				roleId: role.Id,
				firstName: emp.FirstName,
				lastName: emp.LastName,
				middleName: emp.MiddleName,
				shortName: $"{emp.FirstName[0]}.{emp.LastName}",
				employeeStatusType: EmployeeStatusType.Inactive,
				dateOfBirth: emp.DateOfBirth,
				familyCast: emp.FamilyCast,
				castType: emp.CastType,
				genderType: emp.Gender,
				religion: emp.Religion,
				maritalStatus: emp.MaritalStatus,
				bloodGroup: emp.BloodGroup,
				mobileNo: emp.Mobile,
				secondaryMobileNo: emp.SecondaryMobile,
				email: emp.Email,
				address1: emp.Address1,
				address2: emp.Address2,
				address3: emp.Address3,
				designation: emp.Designation,
				department: emp.Department,
				dateOfJoining: emp.DateOfJoining,
				employeeGroup: EmployeeGroupType.NonTeaching,
				qualification: emp.Qualification,
						identificationMark1: emp.IdentificationMark,
						city: emp.City,
									state: emp.State,
									country: emp.Country,
									pinCode: emp.PinCode,
									shiftId: shiftId
								);

						await _employeeRepository.InsertAsync(employee, autoSave: true);
						seededEmployeeIds.Add(employee.Id);
						await SeedEmployeeProfilePictureAsync(employee.Id);

						// Seed family members for married employees
						if (emp.MaritalStatus == MaritalStatusType.Married)
						{
							await SeedFamilyMembersAsync(employee.Id, emp.FirstName, emp.Gender);
						}
					}
				}

				private async Task SeedEmployeeProfilePictureAsync(Guid employeeId)
				{
					_logger.LogDebug("[HRSeeder] Upload profile picture start EmployeeId={EmployeeId}", employeeId);
					var idx = (employeeId.GetHashCode() & 0x7FFFFFFF) % ProfileSeedImageCount;
					var resourceName = $"ShikshaMitra.Seeders.Assets.profile-seed-{idx:D2}.png";
					await using var stream = typeof(HRSeederContributor).Assembly.GetManifestResourceStream(resourceName);
					if (stream == null)
					{
						throw new InvalidOperationException($"Missing embedded profile seed image: {resourceName}");
					}

					using var memoryStream = new MemoryStream();
					await stream.CopyToAsync(memoryStream);
					var bytes = memoryStream.ToArray();
					await _employeeDocumentManager.UploadProfilePictureAsync(employeeId, $"seed-avatar-{idx}.png", bytes);
					_logger.LogDebug("[HRSeeder] Upload profile picture done EmployeeId={EmployeeId}", employeeId);
				}

				/// <summary>
				/// Seeds sample family members for an employee (Father, Mother, and Spouse if married)
				/// </summary>
				private async Task SeedFamilyMembersAsync(Guid employeeId, string employeeFirstName, GenderType employeeGender)
				{
					var tenantId = CurrentTenant.Id;

					// Father
					var father = new EmployeeFamilyMember(
						id: GuidGenerator.Create(),
						tenantId: tenantId,
						employeeId: employeeId,
						relationType: FamilyRelationType.Father,
						name: $"{employeeFirstName}'s Father",
						occupation: "Retired",
						contactNo: "9800000001",
						address1: "Sample Address 1",
						address2: "Sample Address 2",
						address3: null,
						city: "Mumbai",
						stateName: "Maharashtra",
						stateIsoCode: "MH",
						countryName: "India",
						countryIsoCode: "IN",
						pinCode: "400001"
					);
					await _familyMemberRepository.InsertAsync(father, autoSave: true);

					// Mother
					var mother = new EmployeeFamilyMember(
						id: GuidGenerator.Create(),
						tenantId: tenantId,
						employeeId: employeeId,
						relationType: FamilyRelationType.Mother,
						name: $"{employeeFirstName}'s Mother",
						occupation: "Homemaker",
						contactNo: "9800000002",
						address1: "Sample Address 1",
						address2: "Sample Address 2",
						address3: null,
						city: "Mumbai",
						stateName: "Maharashtra",
						stateIsoCode: "MH",
						countryName: "India",
						countryIsoCode: "IN",
						pinCode: "400001"
					);
					await _familyMemberRepository.InsertAsync(mother, autoSave: true);

					// Spouse (for married employees)
					var spouseName = employeeGender == GenderType.Male ? "Wife" : "Husband";
					var spouse = new EmployeeFamilyMember(
						id: GuidGenerator.Create(),
						tenantId: tenantId,
						employeeId: employeeId,
						relationType: FamilyRelationType.Spouse,
						name: $"{employeeFirstName}'s {spouseName}",
						occupation: "Professional",
						contactNo: "9800000003",
						address1: "Same as employee",
						address2: null,
						address3: null,
						city: "Mumbai",
						stateName: "Maharashtra",
						stateIsoCode: "MH",
						countryName: "India",
						countryIsoCode: "IN",
						pinCode: "400001"
					);
					await _familyMemberRepository.InsertAsync(spouse, autoSave: true);
				}

				private async Task<Guid> CreateUserForEmployeeAsync(
		string firstName,
		string lastName,
		string email,
		string mobileNo,
		string roleName)
	{
		// Validate mobile number
		if (string.IsNullOrWhiteSpace(mobileNo) || mobileNo.Length < 4)
		{
			throw new Exception($"Invalid mobile number for employee {firstName} {lastName}");
		}

		// Check if email already exists
		var normalizedEmail = _lookupNormalizer.NormalizeEmail(email);
		var existingUserByEmail = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (existingUserByEmail != null)
		{
			await EnsureUserInRoleAsync(existingUserByEmail, roleName);
			return existingUserByEmail.Id;
		}

		// Generate unique username
		var username = await GenerateUniqueUsernameAsync(firstName, lastName);

		// Generate password (Username@last4digitsOfMobile - with uppercase first letter)
		var last4Digits = mobileNo.Substring(mobileNo.Length - 4);
		var capitalizedUsername = char.ToUpper(username[0]) + username.Substring(1);
		var password = $"{capitalizedUsername}@{last4Digits}";

		// Create user
		var user = new IdentityUser(
			GuidGenerator.Create(),
			username,
			email,
			CurrentTenant.Id
		);

		user.Name = firstName;
		user.Surname = lastName;
		user.SetEmailConfirmed(true);
		user.SetPhoneNumber(mobileNo, false);

		// Create user with password
		var userCreateResult = await _userManager.CreateAsync(user, password);
		if (!userCreateResult.Succeeded)
		{
			throw new Exception(
				"Failed to create user for seeding: " +
				string.Join(", ", userCreateResult.Errors.Select(e => e.Description))
			);
		}

		// Verify user was created successfully
		var createdUser = await _userRepository.FindAsync(user.Id);
		if (createdUser == null)
		{
			throw new Exception("User was not created successfully during seeding.");
		}

		await EnsureUserInRoleAsync(createdUser, roleName);

		return user.Id;
	}

	private async Task EnsureUserInRoleAsync(IdentityUser user, string roleName)
	{
		var normalizedRoleName = _lookupNormalizer.NormalizeName(roleName);
		var role = await _roleRepository.FindByNormalizedNameAsync(normalizedRoleName);
		if (role == null || string.IsNullOrWhiteSpace(role.Name))
		{
			return;
		}

		var isInRole = await _userManager.IsInRoleAsync(user, role.Name);
		if (isInRole)
		{
			return;
		}

		var addRoleResult = await _userManager.AddToRoleAsync(user, role.Name);
		if (!addRoleResult.Succeeded)
		{
			throw new Exception(
				$"Failed to assign role '{role.Name}' to seeded user '{user.UserName}': " +
				string.Join(", ", addRoleResult.Errors.Select(e => e.Description))
			);
		}
	}

	private async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName)
	{
		// Generate base username: firstInitial + lastName (e.g., jdoe)
		// Sanitize to remove special characters and non-ASCII characters
		var firstInitial = !string.IsNullOrWhiteSpace(firstName)
			? new string(firstName.Where(char.IsLetterOrDigit).ToArray()).ToLower().FirstOrDefault().ToString()
			: "x";

		if (string.IsNullOrWhiteSpace(firstInitial))
		{
			firstInitial = "x";
		}

		var lastNamePart = !string.IsNullOrWhiteSpace(lastName)
			? new string(lastName.Where(char.IsLetterOrDigit).ToArray()).ToLower()
			: "user";

		if (string.IsNullOrWhiteSpace(lastNamePart))
		{
			lastNamePart = "user";
		}

		var baseUsername = $"{firstInitial}{lastNamePart}";

		// Check if username exists
		var normalizedUsername = _lookupNormalizer.NormalizeName(baseUsername);
		var existingUser = await _userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

		if (existingUser == null)
		{
			return baseUsername;
		}

		// Username exists, append sequence number
		var sequence = 1;
		while (true)
		{
			var sequencedUsername = $"{baseUsername}{sequence:D2}";
			normalizedUsername = _lookupNormalizer.NormalizeName(sequencedUsername);
			existingUser = await _userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

			if (existingUser == null)
			{
				return sequencedUsername;
			}

			sequence++;
			if (sequence > 99)
			{
				throw new Exception("Unable to generate unique username for seeding.");
			}
		}
	}
}
