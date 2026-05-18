using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
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
using Microsoft.Extensions.Configuration;
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
	private const int ActiveEmployeeSeedPercentage = 90;
	private const int SampleAttendanceMonthWindow = 12;
	private const string TempPassword = "Temp@123";

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
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly IConfiguration _configuration;
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
			IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
			IConfiguration configuration,
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
		_payrollRunItemSnapshotRepository = payrollRunItemSnapshotRepository;
		_configuration = configuration;
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

		// Phase 5: Keep seeded/demo employees realistic: most active, some inactive.
		var activeEmployeeIds = await EnsureEmployeeStatusDistributionAsync(seededEmployeeIds);
		_logger.LogInformation("[HRSeeder] Phase 5 complete (active employees={Count})", activeEmployeeIds.Count);

		// Phase 6: Payroll-ready active employees with 12 months of attendance.
		var samplePayrollEmployeeIds = await SeedSampleEmployeePayrollAndAttendanceAsync(activeEmployeeIds);
		if (ShouldSeedSampleLeaveScenarios())
		{
			await SeedSampleLeaveScenariosAsync(samplePayrollEmployeeIds);
		}
		_logger.LogInformation("[HRSeeder] Phase 6 complete (sample payroll/attendance/leaves)");
		_logger.LogInformation("[HRSeeder] Finished seeding for TenantId={TenantId}", CurrentTenant.Id);
	}

	private bool ShouldSeedSampleLeaveScenarios() =>
		_configuration.GetValue(
			"ShikshaMitra:Seeders:HR:SeedSampleLeaveScenarios",
			_configuration.GetValue("ShikshaMitra:Seeders:HR:SeedSampleApprovedLeaves", true));

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
		{
			await EnsureSeededReportingChainAsync();
			return seededEmployeeIds;
		}

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

		await EnsureSeededReportingChainAsync();

		return seededEmployeeIds;
	}

	private async Task EnsureSeededReportingChainAsync()
	{
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var seededEmployees = employeeQuery
			.AsEnumerable()
			.Where(IsSeededDemoEmployee)
			.ToDictionary(e => e.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);

		await LinkSeededEmployeeToLoginAsync(seededEmployees, "hiren.vadher@ddvs.com", "principal-gseb@ddvs.com");
		await LinkSeededEmployeeToLoginAsync(seededEmployees, "sweta.badgujar@ddvs.com", "teacher-gseb@ddvs.com");
		await LinkSeededEmployeeToLoginAsync(seededEmployees, "punam.badgujar@ddvs.com", "finance-officer@ddvs.com");
		await LinkSeededEmployeeToLoginAsync(seededEmployees, "raj.baku@ddvs.com", "clerk-office@ddvs.com");

		await AssignSeededReportingManagerAsync(seededEmployees, "jignesh.jinjala@ddvs.com", "hiren.vadher@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "akshay.patel@ddvs.com", "jignesh.jinjala@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "parth.ghusar@ddvs.com", "jignesh.jinjala@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "sweta.badgujar@ddvs.com", "akshay.patel@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "jeet.ram@ddvs.com", "akshay.patel@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "dharmik.jinjala@ddvs.com", "akshay.patel@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "jay.dalia@ddvs.com", "parth.ghusar@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "hiren.akbari@ddvs.com", "parth.ghusar@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "prerna.atre@ddvs.com", "parth.ghusar@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "vishal.makwana@ddvs.com", "jignesh.jinjala@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "mayur.solanki@ddvs.com", "vishal.makwana@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "punam.badgujar@ddvs.com", "vishal.makwana@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "milind.kumar@ddvs.com", "vishal.makwana@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "nikhil.baku@ddvs.com", "mayur.solanki@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "raj.baku@ddvs.com", "mayur.solanki@ddvs.com");

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task LinkSeededEmployeeToLoginAsync(
		IReadOnlyDictionary<string, Employee> seededEmployees,
		string employeeEmail,
		string loginEmail)
	{
		if (!seededEmployees.TryGetValue(employeeEmail, out var employee))
		{
			return;
		}

		var user = await _userRepository.FindByNormalizedEmailAsync(_lookupNormalizer.NormalizeEmail(loginEmail));
		if (user == null || employee.UserId == user.Id)
		{
			return;
		}

		employee.UpdateUserId(user.Id);
		await _employeeRepository.UpdateAsync(employee, autoSave: false);
	}

	private async Task AssignSeededReportingManagerAsync(
		IReadOnlyDictionary<string, Employee> seededEmployees,
		string employeeEmail,
		string managerEmail)
	{
		if (!seededEmployees.TryGetValue(employeeEmail, out var employee) ||
		    !seededEmployees.TryGetValue(managerEmail, out var manager) ||
		    employee.ReportingManagerId == manager.Id)
		{
			return;
		}

		employee.AssignReportingManager(manager.Id);
		await _employeeRepository.UpdateAsync(employee, autoSave: false);
	}

	private async Task<List<Guid>> EnsureEmployeeStatusDistributionAsync(List<Guid> seededEmployeeIds)
	{
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		if (seededEmployeeIds.Count > 0)
		{
			employeeQuery = employeeQuery.Where(e => seededEmployeeIds.Contains(e.Id));
		}

		var employees = employeeQuery
			.Where(e =>
				!e.EmployeeStatusType.HasValue ||
				e.EmployeeStatusType == EmployeeStatusType.Active ||
				e.EmployeeStatusType == EmployeeStatusType.Inactive)
			.AsEnumerable()
			.Where(IsSeededDemoEmployee)
			.OrderBy(e => e.EmployeeCode)
			.ToList();
		if (employees.Count == 0)
		{
			return new List<Guid>();
		}

		var inactiveCount = employees.Count <= 1
			? 0
			: (int)Math.Round(
				employees.Count * ((100m - ActiveEmployeeSeedPercentage) / 100m),
				MidpointRounding.AwayFromZero);
		inactiveCount = Math.Clamp(inactiveCount, 0, employees.Count - 1);

		var inactiveEmployeeIds = employees
			.OrderBy(e => StableSeedHash($"{CurrentTenant.Id:N}:{e.EmployeeCode}:{e.Email}:{e.Id:N}"))
			.Take(inactiveCount)
			.Select(e => e.Id)
			.ToHashSet();

		var activeEmployeeIds = new List<Guid>();
		var updatedCount = 0;
		foreach (var employee in employees)
		{
			var targetStatus = inactiveEmployeeIds.Contains(employee.Id)
				? EmployeeStatusType.Inactive
				: EmployeeStatusType.Active;
			if (targetStatus == EmployeeStatusType.Active)
			{
				activeEmployeeIds.Add(employee.Id);
			}

			if (employee.EmployeeStatusType == targetStatus)
			{
				continue;
			}

			var updated = await _employeeManager.UpdateEmployeeStatusAsync(
				employee.Id,
				targetStatus,
				$"Set by HR seeder to maintain {ActiveEmployeeSeedPercentage}% active demo employee distribution");
			await _employeeRepository.UpdateAsync(updated, autoSave: false);
			updatedCount++;
		}

		if (updatedCount > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation(
			"[HRSeeder] Employee status distribution applied active={ActiveCount}, inactive={InactiveCount}, updated={UpdatedCount}",
			activeEmployeeIds.Count,
			inactiveCount,
			updatedCount);

		return activeEmployeeIds;
	}

	private static bool IsSeededDemoEmployee(Employee employee) =>
		employee.Email?.EndsWith("@ddvs.com", StringComparison.OrdinalIgnoreCase) == true;

	private static uint StableSeedHash(string value)
	{
		unchecked
		{
			var hash = 2166136261u;
			foreach (var character in value.ToUpperInvariant())
			{
				hash ^= character;
				hash *= 16777619u;
			}

			return hash;
		}
	}

	/// <summary>
	/// Demo payroll lines + attendance for active employees. Skips employees that already have payroll components.
	/// Attendance: rolling 12 months, weekdays only; idempotent per employee per day.
	/// </summary>
	private async Task<List<Guid>> SeedSampleEmployeePayrollAndAttendanceAsync(List<Guid> activeEmployeeIds)
	{
		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
		if (educationMediumStage == null)
		{
			return new List<Guid>();
		}

		if (!await _earningComponentRepository.AnyAsync() || !await _deductionComponentRepository.AnyAsync())
		{
			return new List<Guid>();
		}

		var employeeIds = await ResolveActivePayrollEmployeeIdsAsync(activeEmployeeIds);
		if (employeeIds.Count == 0)
		{
			return employeeIds;
		}

		foreach (var employeeId in employeeIds)
		{
			await SeedSamplePayrollForEmployeeIfNeededAsync(employeeId);
		}

		await SeedSampleAttendanceForEmployeesAsync(
			employeeIds,
			educationMediumStage.Id);

		return employeeIds;
	}

	private async Task<List<Guid>> ResolveActivePayrollEmployeeIdsAsync(List<Guid> activeEmployeeIds)
	{
		var employeeIds = activeEmployeeIds
			.Distinct()
			.ToList();

		if (employeeIds.Count > 0)
		{
			return employeeIds;
		}

		var existingEmployeeQuery = await _employeeRepository.GetQueryableAsync();
		return existingEmployeeQuery
			.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
			.OrderBy(e => e.EmployeeCode)
			.Select(e => e.Id)
			.ToList();
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
		var employee = await _employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			return;
		}

		var forceRepairSeededDemoPayroll = IsSeededDemoEmployee(employee);
		var existingComponents = await _employeePayrollComponentRepository.GetByEmployeeIdAsync(employeeId);
		var existingComponentsById = existingComponents
			.Where(x => x.ComponentId.HasValue)
			.GroupBy(x => (x.ComponentId!.Value, x.IsEarning))
			.ToDictionary(x => x.Key, x => x.First());
		var seededSalary = BuildSeededSalary(employee);

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
			var amount = seededSalary.GetEarningAmount(master.Name);
			var isFixed = master.CalculationType == CalculationType.Amount;
			if (existingComponentsById.TryGetValue((master.Id, true), out var existing))
			{
				if (amount <= 0m)
				{
					await _employeePayrollComponentRepository.DeleteAsync(existing, autoSave: false);
					hasPendingChanges = true;
					continue;
				}

				if (forceRepairSeededDemoPayroll || existing.UsesMasterValue || existing.Amount == master.Amount)
				{
					existing.UpdateComponent(
						master.Name,
						master.Description,
						amount,
						isFixed,
						isTemp: false,
						master.CalculationType == CalculationType.Amount
							? SalaryComponentType.Fixed
							: SalaryComponentType.Percentage,
						MapSalaryDependency(master.DependOn),
						usesMasterValue: false);
					await _employeePayrollComponentRepository.UpdateAsync(existing, autoSave: false);
					hasPendingChanges = true;
				}

				continue;
			}

			if (amount <= 0m)
			{
				continue;
			}

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
				isEarning: true,
				usesMasterValue: false);
			await _employeePayrollComponentRepository.InsertAsync(row, autoSave: false);
			hasPendingChanges = true;
		}

		foreach (var master in deductionMasters)
		{
			var amount = seededSalary.GetDeductionAmount(master.Name);
			var isFixed = master.CalculationType == CalculationType.Amount;
			if (existingComponentsById.TryGetValue((master.Id, false), out var existing))
			{
				if (amount <= 0m)
				{
					await _employeePayrollComponentRepository.DeleteAsync(existing, autoSave: false);
					hasPendingChanges = true;
					continue;
				}

				if (forceRepairSeededDemoPayroll || existing.UsesMasterValue || existing.Amount == master.Amount)
				{
					existing.UpdateComponent(
						master.Name,
						master.Description,
						amount,
						isFixed,
						isTemp: false,
						master.CalculationType == CalculationType.Amount
							? SalaryComponentType.Fixed
							: SalaryComponentType.Percentage,
						MapSalaryDependency(master.DependOn),
						usesMasterValue: false);
					await _employeePayrollComponentRepository.UpdateAsync(existing, autoSave: false);
					hasPendingChanges = true;
				}

				continue;
			}

			if (amount <= 0m)
			{
				continue;
			}

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
				isEarning: false,
				usesMasterValue: false);
			await _employeePayrollComponentRepository.InsertAsync(row, autoSave: false);
			hasPendingChanges = true;
		}

		if (hasPendingChanges)
		{
			await InvalidateUnlockedPayrollSnapshotsAsync(employeeId);
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

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
		var grossSalary = ResolveSeededGrossSalary(employee);
		var basic = RoundToNearest(grossSalary * 0.50m, 100m);
		var hra = RoundToNearest(grossSalary * 0.20m, 100m);
		var da = RoundToNearest(grossSalary * 0.10m, 100m);
		var transport = grossSalary >= 90_000m ? 5_000m : grossSalary >= 55_000m ? 2_500m : 1_600m;
		var medical = grossSalary >= 55_000m ? 2_000m : 1_250m;
		var seed = StableSeedHash($"{CurrentTenant.Id:N}:{employee.EmployeeCode}:{employee.Email}:{employee.Id:N}");
		var performanceBonus = grossSalary switch
		{
			>= 100_000m => 8_000m + (seed % 3) * 1_000m,
			>= 70_000m => 4_000m + (seed % 3) * 1_000m,
			>= 45_000m => 2_000m + (seed % 2) * 1_000m,
			_ => seed % 2 == 0 ? 1_000m : 0m
		};
		var incentive = seed % 4 == 0 ? 2_000m : seed % 5 == 0 ? 1_000m : 0m;
		var specialAllowance = grossSalary - basic - hra - da - transport - medical - performanceBonus - incentive;
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
				["Employee State Insurance (ESI)"] = 0m,
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

	private static decimal RoundToNearest(decimal amount, decimal nearest)
	{
		return Math.Round(amount / nearest, MidpointRounding.AwayFromZero) * nearest;
	}

	private sealed record SeededSalaryProfile(
		decimal GrossSalary,
		IReadOnlyDictionary<string, decimal> Earnings,
		IReadOnlyDictionary<string, decimal> Deductions)
	{
		public decimal GetEarningAmount(string name) =>
			Earnings.TryGetValue(name, out var amount) ? amount : 0m;

		public decimal GetDeductionAmount(string name) =>
			Deductions.TryGetValue(name, out var amount) ? amount : 0m;
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
		var windowStartMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
			.AddMonths(-(SampleAttendanceMonthWindow - 1));
		var windowStart = DateOnly.FromDateTime(windowStartMonth);

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

	private async Task SeedSampleLeaveScenariosAsync(List<Guid> employeeIds)
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
		var lossOfPay = leavePolicies.FirstOrDefault(x =>
			x.PolicyName.Contains("Loss", StringComparison.OrdinalIgnoreCase) ||
			x.PolicyName.Contains("LOP", StringComparison.OrdinalIgnoreCase));
		if (casual == null || sick == null)
		{
			return;
		}

		var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
		var candidates = employeeIds.Take(Math.Min(4, employeeIds.Count)).ToList();
		var insertedCount = 0;
		var scenarios = new List<LeaveSeedScenario>
		{
			new("HR seed approved sick leave", EmployeeLeaveStatusType.Approved, sick, 2),
			new("HR seed pending casual leave", EmployeeLeaveStatusType.Pending, casual, 5),
			new("HR seed rejected casual leave", EmployeeLeaveStatusType.Rejected, casual, 8, "Seeded rejection scenario."),
			new("HR seed approved loss of pay leave", EmployeeLeaveStatusType.Approved, lossOfPay ?? casual, 11)
		};

		for (var i = 0; i < candidates.Count && i < scenarios.Count; i++)
		{
			var employeeId = candidates[i];
			var scenario = scenarios[i];
			var leaveDate = GetCurrentMonthBusinessDayNotFuture(currentMonthStart, scenario.BusinessDay);
			if (await _employeeLeaveRepository.AnyAsync(x =>
				    x.EmployeeId == employeeId &&
				    x.Description == scenario.Description))
			{
				continue;
			}

			var leave = await _employeeLeaveManager.CreateAsync(
				employeeId,
				scenario.Policy.Id,
				new DateTimeOffset(leaveDate, TimeSpan.FromHours(5.5)),
				new DateTimeOffset(leaveDate, TimeSpan.FromHours(5.5)),
				scenario.Description);
			await _employeeLeaveRepository.InsertAsync(leave, autoSave: true);
			if (scenario.Status != EmployeeLeaveStatusType.Pending)
			{
				await _employeeLeaveManager.UpdateLeaveRequestStatusAsync(
					leave,
					scenario.Status,
					scenario.RejectionReason);
			}
			insertedCount++;
		}

		_logger.LogInformation(
			"[HRSeeder] Leave scenario sample seeded rows={InsertedCount}",
			insertedCount);
	}

	private sealed record LeaveSeedScenario(
		string Description,
		EmployeeLeaveStatusType Status,
		LeavePolicy Policy,
		int BusinessDay,
		string? RejectionReason = null);

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
							FirstName = "Hiren",
							LastName = "Vadher",
							MiddleName = (string?)null,
							Email = "hiren.vadher@ddvs.com",
							Mobile = "9876543210",
							SecondaryMobile = "9876543211",
							RoleName = "Principal",
							Designation = "Principal",
							Department = "Administration",
							DateOfBirth = new DateOnly(1984, 1, 1),
							DateOfJoining = new DateOnly(2010, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Vadher",
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
							FirstName = "Jignesh",
							LastName = "Jinjala",
							MiddleName = (string?)null,
							Email = "jignesh.jinjala@ddvs.com",
							Mobile = "9876543212",
							SecondaryMobile = "9876543213",
							RoleName = "Vice Principal",
							Designation = "Vice Principal",
							Department = "Administration",
							DateOfBirth = new DateOnly(2001, 4, 16),
							DateOfJoining = new DateOnly(2022, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Jinjala",
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
							FirstName = "Akshay",
							LastName = "Patel",
							MiddleName = (string?)null,
							Email = "akshay.patel@ddvs.com",
							Mobile = "9876543214",
							SecondaryMobile = (string?)null,
							RoleName = "HOD",
							Designation = "Head of Science Department",
							Department = "Science",
							DateOfBirth = new DateOnly(1988, 1, 1),
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
							FirstName = "Parth",
							LastName = "Ghusar",
							MiddleName = (string?)null,
							Email = "parth.ghusar@ddvs.com",
							Mobile = "9876543215",
							SecondaryMobile = (string?)null,
							RoleName = "HOD",
							Designation = "Head of Mathematics Department",
							Department = "Mathematics",
							DateOfBirth = new DateOnly(2000, 5, 14),
							DateOfJoining = new DateOnly(2020, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Ghusar",
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
							FirstName = "Sweta",
							LastName = "Badgujar",
							MiddleName = (string?)null,
							Email = "sweta.badgujar@ddvs.com",
							Mobile = "9876543216",
							SecondaryMobile = "9876543217",
							RoleName = "Teacher",
							Designation = "Senior Teacher - English",
							Department = "Languages",
							DateOfBirth = new DateOnly(2003, 7, 2),
							DateOfJoining = new DateOnly(2023, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Badgujar",
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
							FirstName = "Jeet",
							LastName = "Ram",
							MiddleName = (string?)null,
							Email = "jeet.ram@ddvs.com",
							Mobile = "9876543218",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Chemistry",
							Department = "Science",
							DateOfBirth = new DateOnly(1992, 1, 1),
							DateOfJoining = new DateOnly(2016, 7, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Ram",
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
							FirstName = "Dharmik",
							LastName = "Jinjala",
							MiddleName = (string?)null,
							Email = "dharmik.jinjala@ddvs.com",
							Mobile = "9876543219",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Biology",
							Department = "Science",
							DateOfBirth = new DateOnly(2003, 11, 27),
							DateOfJoining = new DateOnly(2023, 12, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Jinjala",
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
							FirstName = "Jay",
							LastName = "Dalia",
							MiddleName = (string?)null,
							Email = "jay.dalia@ddvs.com",
							Mobile = "9876543220",
							SecondaryMobile = "9876543221",
							RoleName = "Teacher",
							Designation = "Teacher - History",
							Department = "Social Studies",
							DateOfBirth = new DateOnly(1997, 10, 12),
							DateOfJoining = new DateOnly(2016, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Dalia",
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
							FirstName = "Hiren",
							LastName = "Akbari",
							MiddleName = (string?)null,
							Email = "hiren.akbari@ddvs.com",
							Mobile = "9876543222",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Computer Science",
							Department = "Computer Science",
							DateOfBirth = new DateOnly(1996, 1, 7),
							DateOfJoining = new DateOnly(2018, 7, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Akbari",
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
							FirstName = "Prerna",
							LastName = "Atre",
							MiddleName = (string?)null,
							Email = "prerna.atre@ddvs.com",
							Mobile = "9876543223",
							SecondaryMobile = (string?)null,
							RoleName = "Teacher",
							Designation = "Teacher - Hindi",
							Department = "Languages",
							DateOfBirth = new DateOnly(2002, 12, 1),
							DateOfJoining = new DateOnly(2022, 12, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Atre",
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
				employeeStatusType: EmployeeStatusType.Active,
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
							FirstName = "Vishal",
							LastName = "Makwana",
							MiddleName = (string?)null,
							Email = "vishal.makwana@ddvs.com",
							Mobile = "9876543224",
							SecondaryMobile = "9876543225",
							RoleName = "Administrative Manager",
							Designation = "Administrative Manager",
							Department = "Administration",
							DateOfBirth = new DateOnly(1999, 5, 10),
							DateOfJoining = new DateOnly(2019, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Makwana",
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
							FirstName = "Mayur",
							LastName = "Solanki",
							MiddleName = (string?)null,
							Email = "mayur.solanki@ddvs.com",
							Mobile = "9876543226",
							SecondaryMobile = (string?)null,
							RoleName = "HR Manager",
							Designation = "HR Manager",
							Department = "Human Resources",
							DateOfBirth = new DateOnly(2003, 3, 31),
							DateOfJoining = new DateOnly(2023, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Solanki",
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
							FirstName = "Punam",
							LastName = "Badgujar",
							MiddleName = (string?)null,
							Email = "punam.badgujar@ddvs.com",
							Mobile = "9876543227",
							SecondaryMobile = "9876543228",
							RoleName = "Finance Officer",
							Designation = "Finance Officer",
							Department = "Finance",
							DateOfBirth = new DateOnly(1998, 11, 15),
							DateOfJoining = new DateOnly(2019, 6, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Badgujar",
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
							FirstName = "Milind",
							LastName = "Kumar",
							MiddleName = (string?)null,
							Email = "milind.kumar@ddvs.com",
							Mobile = "9876543229",
							SecondaryMobile = (string?)null,
							RoleName = "IT Administrator",
							Designation = "IT Administrator",
							Department = "Information Technology",
							DateOfBirth = new DateOnly(1995, 2, 12),
							DateOfJoining = new DateOnly(2016, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							FamilyCast = "Kumar",
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
							FirstName = "Nikhil",
							LastName = "Baku",
							MiddleName = (string?)null,
							Email = "nikhil.baku@ddvs.com",
							Mobile = "9876543230",
							SecondaryMobile = (string?)null,
							RoleName = "Office Assistant",
							Designation = "Office Assistant",
							Department = "Administration",
							DateOfBirth = new DateOnly(2001, 10, 21),
							DateOfJoining = new DateOnly(2021, 11, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Baku",
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
							FirstName = "Raj",
							LastName = "Baku",
							MiddleName = (string?)null,
							Email = "raj.baku@ddvs.com",
							Mobile = "9876543231",
							SecondaryMobile = (string?)null,
							RoleName = "Clerk",
							Designation = "Clerk",
							Department = "Administration",
							DateOfBirth = new DateOnly(2000, 7, 10),
							DateOfJoining = new DateOnly(2020, 7, 15),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							FamilyCast = "Baku",
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
				employeeStatusType: EmployeeStatusType.Active,
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

		// Check if email already exists. Also recognize legacy @school.edu seed emails
		// so rerunning the migrator repairs old local databases instead of duplicating users.
		var existingUserByEmail = await FindUserByEmailOrLegacyAliasAsync(email);
		if (existingUserByEmail != null)
		{
			await UpdateSeededEmployeeUserAsync(existingUserByEmail, firstName, lastName, email, mobileNo);
			await EnsureUserInRoleAsync(existingUserByEmail, roleName);
			return existingUserByEmail.Id;
		}

		// Generate unique username
		var username = await GenerateUniqueUsernameAsync(firstName, lastName);

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

		// Create user with the same deterministic password as other seeded login users.
		var userCreateResult = await _userManager.CreateAsync(user, TempPassword);
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

	private async Task<IdentityUser?> FindUserByEmailOrLegacyAliasAsync(string email)
	{
		var emails = new List<string> { email };
		if (email.EndsWith("@ddvs.com", StringComparison.OrdinalIgnoreCase))
		{
			emails.Add(email[..^"@ddvs.com".Length] + "@school.edu");
		}

		if (string.Equals(email, "nilesh.joshi@ddvs.com", StringComparison.OrdinalIgnoreCase))
		{
			emails.Add("clerk@school.edu");
		}

		foreach (var candidateEmail in emails.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var normalizedEmail = _lookupNormalizer.NormalizeEmail(candidateEmail);
			var user = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
			if (user != null)
			{
				return user;
			}
		}

		return null;
	}

	private async Task UpdateSeededEmployeeUserAsync(
		IdentityUser user,
		string firstName,
		string lastName,
		string email,
		string mobileNo)
	{
		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;

		if (!string.Equals(managedUser.Email, email, StringComparison.OrdinalIgnoreCase))
		{
			var setEmailResult = await _userManager.SetEmailAsync(managedUser, email);
			if (!setEmailResult.Succeeded)
			{
				throw new Exception(
					$"Failed to update seeded HR user email '{email}': " +
					string.Join(", ", setEmailResult.Errors.Select(e => e.Description)));
			}
		}

		managedUser.Name = firstName;
		managedUser.Surname = lastName;
		managedUser.SetEmailConfirmed(true);
		managedUser.SetPhoneNumber(mobileNo, false);
		await _userManager.UpdateAsync(managedUser);
		await ResetPasswordAsync(managedUser, TempPassword);
	}

	private async Task ResetPasswordAsync(IdentityUser user, string password)
	{
		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
		IdentityResult result;
		if (string.IsNullOrWhiteSpace(managedUser.PasswordHash))
		{
			result = await _userManager.AddPasswordAsync(managedUser, password);
		}
		else
		{
			var removeResult = await _userManager.RemovePasswordAsync(managedUser);
			if (!removeResult.Succeeded)
			{
				throw new Exception(
					$"Failed to remove existing password for seeded HR user '{managedUser.Email}': " +
					string.Join(", ", removeResult.Errors.Select(e => e.Description)));
			}

			result = await _userManager.AddPasswordAsync(managedUser, password);
		}

		if (!result.Succeeded)
		{
			throw new Exception(
				$"Failed to set password for seeded HR user '{managedUser.Email}': " +
				string.Join(", ", result.Errors.Select(e => e.Description)));
		}
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

