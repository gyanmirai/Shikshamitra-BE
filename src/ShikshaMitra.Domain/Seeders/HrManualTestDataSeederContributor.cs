using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Development-only HR data pack for repeatable manual testing of Employee, Leave, Attendance, and Payroll.
/// Enable with SeedHrManualTestData=true.
/// </summary>
public class HrManualTestDataSeederContributor : BaseSeederContributor
{
	private const string SeedFlagKey = "SeedHrManualTestData";
	private const string TempPassword = "Temp@123";
	private static readonly Guid DdvsTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);

	private readonly IConfiguration _configuration;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly ILookupNormalizer _lookupNormalizer;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly EmployeeManager _employeeManager;
	private readonly IEmployeeFamilyMemberRepository _familyMemberRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _userScopeRepository;
	private readonly IEmployeePayrollDetailsRepository _payrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IEmployeeAttendanceRepository _attendanceRepository;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly IEmployeeLeaveRepository _leaveRepository;
	private readonly EmployeeLeaveManager _leaveManager;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<PayrollRunReview, Guid> _payrollRunReviewRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly ILogger<HrManualTestDataSeederContributor> _logger;

	public HrManualTestDataSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IConfiguration configuration,
		IIdentityRoleRepository roleRepository,
		IdentityUserManager userManager,
		IIdentityUserRepository userRepository,
		ILookupNormalizer lookupNormalizer,
		IAcademicYearRepository academicYearRepository,
		IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
		IRepository<ShiftManagement, Guid> shiftRepository,
		IEmployeeRepository employeeRepository,
		EmployeeManager employeeManager,
		IEmployeeFamilyMemberRepository familyMemberRepository,
		IRepository<UserScopeAssignment, Guid> userScopeRepository,
		IEmployeePayrollDetailsRepository payrollDetailsRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		IEmployeeAttendanceRepository attendanceRepository,
		ILeavePolicyRepository leavePolicyRepository,
		IEmployeeLeaveRepository leaveRepository,
		EmployeeLeaveManager leaveManager,
		SalarySlipManager salarySlipManager,
		ISalarySlipRepository salarySlipRepository,
		IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
		IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
		ILogger<HrManualTestDataSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_configuration = configuration;
		_roleRepository = roleRepository;
		_userManager = userManager;
		_userRepository = userRepository;
		_lookupNormalizer = lookupNormalizer;
		_academicYearRepository = academicYearRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_shiftRepository = shiftRepository;
		_employeeRepository = employeeRepository;
		_employeeManager = employeeManager;
		_familyMemberRepository = familyMemberRepository;
		_userScopeRepository = userScopeRepository;
		_payrollDetailsRepository = payrollDetailsRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_attendanceRepository = attendanceRepository;
		_leavePolicyRepository = leavePolicyRepository;
		_leaveRepository = leaveRepository;
		_leaveManager = leaveManager;
		_salarySlipManager = salarySlipManager;
		_salarySlipRepository = salarySlipRepository;
		_payrollRunReviewRepository = payrollRunReviewRepository;
		_payrollRunItemSnapshotRepository = payrollRunItemSnapshotRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (!IsEnabled(context) || CurrentTenant.Id != DdvsTenantId)
		{
			return;
		}

		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
		var academicYear = await ResolveAcademicYearAsync();
		var fullDayShift = (await _shiftRepository.GetQueryableAsync())
			.FirstOrDefault(s => s.ShiftName == "Full Day Shift");
		if (educationMediumStage == null || academicYear == null)
		{
			_logger.LogWarning(
				"[HrManualTestDataSeeder] Skipped because academic setup is incomplete. TenantId={TenantId}",
				CurrentTenant.Id);
			return;
		}

		var specs = BuildEmployeeSpecs();
		var employees = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
		foreach (var spec in specs)
		{
			var employee = await EnsureEmployeeAsync(spec, fullDayShift?.Id);
			employees[spec.Key] = employee;
			await EnsureUserScopeAsync(employee, academicYear.Id, educationMediumStage.Id, spec);
			await EnsureFamilyMembersAsync(employee, spec);
			await EnsurePayrollAsync(employee, spec);
		}

		await EnsureReportingChainAsync(employees);

		var seedMonth = DateTime.UtcNow;
		await SeedAttendanceAsync(employees, educationMediumStage.Id, seedMonth);
		await SeedPreviousMonthAttendanceAsync(employees, seedMonth.AddMonths(-1), educationMediumStage.Id);
		await SeedLeavesAsync(employees, academicYear, seedMonth);
		await SeedPayrollRunStateAsync(employees, seedMonth);

		await CurrentUnitOfWork.SaveChangesAsync();
		_logger.LogInformation(
			"[HrManualTestDataSeeder] Seeded HR manual test data for {MonthYear}. Employees={EmployeeCount}",
			seedMonth.ToString("yyyy-MM"),
			employees.Count);
	}

	private bool IsEnabled(DataSeedContext context)
	{
		if (context.Properties.TryGetValue(SeedFlagKey, out var value) && TryConvertToBool(value, out var enabled))
		{
			return enabled;
		}

		return _configuration.GetValue<bool>(SeedFlagKey, false);
	}

	private static bool TryConvertToBool(object? value, out bool result)
	{
		switch (value)
		{
			case bool boolValue:
				result = boolValue;
				return true;
			case string stringValue when bool.TryParse(stringValue, out var parsed):
				result = parsed;
				return true;
			default:
				result = false;
				return false;
		}
	}

	private async Task<AcademicYear?> ResolveAcademicYearAsync()
	{
		var today = DateTime.UtcNow.Date;
		return await _academicYearRepository.FirstOrDefaultAsync(y =>
				y.StartDate <= today && y.EndDate >= today)
			?? await _academicYearRepository.FirstOrDefaultAsync();
	}

	private static List<HrManualEmployeeSpec> BuildEmployeeSpecs() =>
	[
		new("yagnik1", "Yagnik", "Vadher 1", "yagnik.vadher1@ddvs.com", "9100001001", "Principal", "Principal", "Administration", EmployeeGroupType.Teaching, GenderType.Male, MaritalStatusType.Married, 120_000m, AttendanceScenario.Normal, PayrollState.Locked),
		new("yagnik2", "Yagnik", "Vadher 2", "yagnik.vadher2@ddvs.com", "9100001002", "Vice Principal", "Vice Principal", "Administration", EmployeeGroupType.Teaching, GenderType.Male, MaritalStatusType.Married, 105_000m, AttendanceScenario.Normal, PayrollState.Locked),
		new("prerna", "Prerna", "Atre", "prerna.atre@ddvs.com", "9100001003", "HR Manager", "HR Manager", "Human Resources", EmployeeGroupType.NonTeaching, GenderType.Female, MaritalStatusType.Married, 72_000m, AttendanceScenario.Normal, PayrollState.Pending),
		new("parth", "Parth", "Ghusar", "parth.ghusar@ddvs.com", "9100001004", "Finance Officer", "Finance Officer", "Finance", EmployeeGroupType.NonTeaching, GenderType.Male, MaritalStatusType.Unmarried, 78_000m, AttendanceScenario.Normal, PayrollState.Locked),
		new("raj", "Raj", "Baku", "raj.baku@ddvs.com", "9100001005", "Teacher", "Senior Teacher - Mathematics", "Mathematics", EmployeeGroupType.Teaching, GenderType.Male, MaritalStatusType.Married, 82_000m, AttendanceScenario.Clean, PayrollState.Reviewed),
		new("jignesh", "Jignesh", "Jinjala", "jignesh.jinjala@ddvs.com", "9100001006", "Teacher", "Teacher - Science", "Science", EmployeeGroupType.Teaching, GenderType.Male, MaritalStatusType.Married, 58_000m, AttendanceScenario.TwoHalfDays, PayrollState.Pending),
		new("nikhil", "Nikhil", "Baku", "nikhil.baku@ddvs.com", "9100001007", "Teacher", "Teacher - English", "English", EmployeeGroupType.Teaching, GenderType.Male, MaritalStatusType.Unmarried, 54_000m, AttendanceScenario.ApprovedLeave, PayrollState.Reviewed),
		new("keyur", "Keyur", "Sidhupura", "keyur.sidhupura@ddvs.com", "9100001008", "Teacher", "Teacher - Computer Science", "Computer Science", EmployeeGroupType.Teaching, GenderType.Male, MaritalStatusType.Unmarried, 60_000m, AttendanceScenario.LowAttendance, PayrollState.Pending),
		new("mayur", "Mayur", "Solanki", "mayur.solanki@ddvs.com", "9100001009", "Administrative Manager", "Administrative Manager", "Administration", EmployeeGroupType.NonTeaching, GenderType.Male, MaritalStatusType.Married, 68_000m, AttendanceScenario.Normal, PayrollState.Pending),
		new("jagruti", "Jagruti", "Vadher", "jagruti.vadher@ddvs.com", "9100001010", "Office Assistant", "Office Assistant", "Administration", EmployeeGroupType.NonTeaching, GenderType.Female, MaritalStatusType.Married, 30_000m, AttendanceScenario.OneLopDay, PayrollState.Reviewed),
		new("vishal", "Vishal", "Makwana", "vishal.makwana@ddvs.com", "9100001011", "IT Administrator", "IT Administrator", "Information Technology", EmployeeGroupType.NonTeaching, GenderType.Male, MaritalStatusType.Unmarried, 64_000m, AttendanceScenario.Normal, PayrollState.Pending)
	];

	private async Task<Employee> EnsureEmployeeAsync(HrManualEmployeeSpec spec, Guid? shiftId)
	{
		var user = await EnsureUserAsync(spec);
		var existingEmployee = await _employeeRepository.GetEmployeeByUserIdAsync(user.Id);
		if (existingEmployee != null)
		{
			if (existingEmployee.EmployeeStatusType != EmployeeStatusType.Active)
			{
				existingEmployee.UpdateEmployeeStatus(EmployeeStatusType.Active, "HR manual test data reset");
				await _employeeRepository.UpdateAsync(existingEmployee, autoSave: false);
			}

			return existingEmployee;
		}

		var role = await FindRoleAsync(spec.RoleName);
		if (role == null)
		{
			throw new InvalidOperationException($"Required role '{spec.RoleName}' was not seeded.");
		}

		var dobYear = spec.Gender == GenderType.Female ? 1990 : 1987;
		var employee = await _employeeManager.CreateEmployeeAsync(
			userId: user.Id,
			roleId: role.Id,
			firstName: spec.FirstName,
			lastName: spec.LastName,
			middleName: null,
			shortName: $"{spec.FirstName[0]}.{spec.LastName}",
			employeeStatusType: EmployeeStatusType.Active,
			dateOfBirth: new DateOnly(dobYear, 4, Math.Min(25, 5 + spec.Key.Length)),
			familyCast: spec.LastName.Split(' ')[0],
			castType: CastType.General,
			genderType: spec.Gender,
			religion: "Hindu",
			maritalStatus: spec.MaritalStatus,
			bloodGroup: spec.Gender == GenderType.Female ? "B+" : "O+",
			mobileNo: spec.MobileNo,
			secondaryMobileNo: null,
			email: spec.Email,
			address1: "DDVS School Campus",
			address2: "Parvat Patiya",
			address3: null,
			designation: spec.Designation,
			department: spec.Department,
			dateOfJoining: new DateOnly(2021, 6, 1),
			employeeGroup: spec.EmployeeGroup,
			qualification: spec.EmployeeGroup == EmployeeGroupType.Teaching ? "B.Ed" : "Graduate",
			identificationMark1: "HR manual test employee",
			city: "Surat",
			state: new State("Gujarat", "GJ"),
			country: new Country("India", "IN"),
			pinCode: "395010",
			shiftId: shiftId);

		await _employeeRepository.InsertAsync(employee, autoSave: true);
		return employee;
	}

	private async Task<IdentityUser> EnsureUserAsync(HrManualEmployeeSpec spec)
	{
		var normalizedEmail = _lookupNormalizer.NormalizeEmail(spec.Email);
		var user = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (user == null)
		{
			user = new IdentityUser(
				GuidGenerator.Create(),
				spec.Key,
				spec.Email,
				CurrentTenant.Id);
			user.Name = spec.FirstName;
			user.Surname = spec.LastName;
			user.SetEmailConfirmed(true);
			user.SetPhoneNumber(spec.MobileNo, false);

			var createResult = await _userManager.CreateAsync(user, TempPassword);
			if (!createResult.Succeeded)
			{
				throw new Exception(
					$"Failed to create HR manual test user '{spec.Email}': " +
					string.Join(", ", createResult.Errors.Select(e => e.Description)));
			}
		}
		else
		{
			var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
			managedUser.Name = spec.FirstName;
			managedUser.Surname = spec.LastName;
			managedUser.SetEmailConfirmed(true);
			managedUser.SetPhoneNumber(spec.MobileNo, false);
			await _userManager.UpdateAsync(managedUser);
			user = managedUser;
		}

		await EnsureRoleMembershipAsync(user, spec.RoleName);
		return user;
	}

	private async Task EnsureRoleMembershipAsync(IdentityUser user, string roleName)
	{
		var role = await FindRoleAsync(roleName);
		if (role == null || string.IsNullOrWhiteSpace(role.Name))
		{
			return;
		}

		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
		if (await _userManager.IsInRoleAsync(managedUser, role.Name))
		{
			return;
		}

		var result = await _userManager.AddToRoleAsync(managedUser, role.Name);
		if (!result.Succeeded)
		{
			throw new Exception(
				$"Failed to assign role '{role.Name}' to HR manual test user '{managedUser.Email}': " +
				string.Join(", ", result.Errors.Select(e => e.Description)));
		}
	}

	private async Task<IdentityRole?> FindRoleAsync(string roleName)
	{
		var normalizedRoleName = _lookupNormalizer.NormalizeName(roleName);
		return await _roleRepository.FindByNormalizedNameAsync(normalizedRoleName);
	}

	private async Task EnsureUserScopeAsync(
		Employee employee,
		Guid academicYearId,
		Guid educationMediumStageId,
		HrManualEmployeeSpec spec)
	{
		if (!employee.UserId.HasValue)
		{
			return;
		}

		var existingQuery = await _userScopeRepository.GetQueryableAsync();
		var exists = existingQuery.Any(x =>
			x.TenantId == CurrentTenant.Id &&
			x.UserId == employee.UserId &&
			x.AcademicYearId == academicYearId &&
			x.EducationMediumStageId == educationMediumStageId &&
			x.IsActive &&
			!x.IsDeleted);
		if (exists)
		{
			return;
		}

		await _userScopeRepository.InsertAsync(
			new UserScopeAssignment(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				userId: employee.UserId,
				academicYearId: academicYearId,
				educationMediumStageId: educationMediumStageId,
				description: $"HR manual test scope for {spec.DisplayName}"),
			autoSave: false);
	}

	private async Task EnsureFamilyMembersAsync(Employee employee, HrManualEmployeeSpec spec)
	{
		var existing = await _familyMemberRepository.GetByEmployeeIdAsync(employee.Id);
		if (existing.Count > 0)
		{
			return;
		}

		await _familyMemberRepository.InsertAsync(
			new EmployeeFamilyMember(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employee.Id,
				FamilyRelationType.Father,
				$"{spec.FirstName}'s Father",
				"Retired",
				"9800002001",
				"DDVS School Campus",
				"Parvat Patiya",
				null,
				"Surat",
				"Gujarat",
				"GJ",
				"India",
				"IN",
				"395010"),
			autoSave: false);

		await _familyMemberRepository.InsertAsync(
			new EmployeeFamilyMember(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employee.Id,
				FamilyRelationType.Mother,
				$"{spec.FirstName}'s Mother",
				"Homemaker",
				"9800002002",
				"DDVS School Campus",
				"Parvat Patiya",
				null,
				"Surat",
				"Gujarat",
				"GJ",
				"India",
				"IN",
				"395010"),
			autoSave: false);
	}

	private async Task EnsureReportingChainAsync(Dictionary<string, Employee> employees)
	{
		void Assign(string employeeKey, string managerKey)
		{
			if (employees.TryGetValue(employeeKey, out var employee) &&
			    employees.TryGetValue(managerKey, out var manager) &&
			    employee.ReportingManagerId != manager.Id)
			{
				employee.AssignReportingManager(manager.Id);
			}
		}

		Assign("yagnik2", "yagnik1");
		Assign("prerna", "yagnik2");
		Assign("parth", "yagnik2");
		Assign("mayur", "prerna");
		Assign("raj", "yagnik2");
		Assign("jignesh", "raj");
		Assign("nikhil", "raj");
		Assign("keyur", "raj");
		Assign("jagruti", "mayur");
		Assign("vishal", "mayur");

		foreach (var employee in employees.Values)
		{
			await _employeeRepository.UpdateAsync(employee, autoSave: false);
		}
	}

	private async Task EnsurePayrollAsync(Employee employee, HrManualEmployeeSpec spec)
	{
		var details = await _payrollDetailsRepository.GetByEmployeeIdAsync(employee.Id);
		if (details == null)
		{
			details = new EmployeePayrollDetails(GuidGenerator.Create(), employee.Id);
			await _payrollDetailsRepository.InsertAsync(details, autoSave: false);
		}

		details.UpdateDetails(
			bankName: "State Bank of India",
			branchName: "Surat Main",
			accountHolderName: spec.DisplayName,
			accountNumber: $"20{spec.MobileNo[^8..]}",
			ifscCode: "SBIN0001234",
			pfRule: "Standard PF",
			epfNumber: $"EPF{spec.MobileNo[^6..]}",
			uanNumber: $"100200{spec.MobileNo[^6..]}",
			punchId: $"HR-{spec.Key.ToUpperInvariant()}",
			totalWorkingHourPerMonth: 176m);
		await _payrollDetailsRepository.UpdateAsync(details, autoSave: false);

		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employee.Id, isEarning: true);
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employee.Id, isEarning: false);

		var components = BuildPayrollComponents(employee.Id, spec);
		foreach (var component in components)
		{
			await _payrollComponentRepository.InsertAsync(component, autoSave: false);
		}
	}

	private List<EmployeePayrollComponent> BuildPayrollComponents(Guid employeeId, HrManualEmployeeSpec spec)
	{
		var basic = Math.Round(spec.MonthlyGross * 0.45m, 0);
		var hra = Math.Round(basic * 0.40m, 0);
		var da = Math.Round(basic * 0.12m, 0);
		var transport = spec.MonthlyGross >= 50_000m ? 2_000m : 1_600m;
		var medical = 1_250m;
		var special = spec.MonthlyGross - basic - hra - da - transport - medical;

		var deductions = new List<(string Name, string Description, decimal Amount, bool IsTemp)>
		{
			("Provident Fund (PF)", "Employee PF contribution", Math.Min(1_800m, Math.Round(basic * 0.12m, 0)), false),
			("Professional Tax", "State professional tax", spec.MonthlyGross >= 20_000m ? 200m : 0m, false)
		};
		if (spec.MonthlyGross >= 75_000m)
		{
			deductions.Add(("Income Tax (TDS)", "Monthly TDS estimate", spec.MonthlyGross >= 100_000m ? 5_000m : 2_500m, false));
		}
		if (spec.Key == "jagruti")
		{
			deductions.Add(("Employee State Insurance (ESI)", "Lower salary ESI-like deduction", 225m, false));
		}
		if (spec.Key == "vishal")
		{
			deductions.Add(("Loan Repayment", "Manual test loan deduction", 1_500m, false));
			deductions.Add(("Advance Salary Deduction", "One-time manual test advance recovery", 750m, true));
		}

		var rows = new List<EmployeePayrollComponent>
		{
			NewPayrollComponent(employeeId, "Basic", "Base salary component", basic, isEarning: true),
			NewPayrollComponent(employeeId, "House Rent Allowance (HRA)", "Housing allowance", hra, isEarning: true),
			NewPayrollComponent(employeeId, "Dearness Allowance (DA)", "Cost-of-living allowance", da, isEarning: true),
			NewPayrollComponent(employeeId, "Transport Allowance", "Conveyance allowance", transport, isEarning: true),
			NewPayrollComponent(employeeId, "Medical Allowance", "Medical reimbursement", medical, isEarning: true),
			NewPayrollComponent(employeeId, "Special Allowance", "School-specific balancing allowance", special, isEarning: true)
		};

		if (spec.Key == "raj")
		{
			rows.Add(NewPayrollComponent(employeeId, "Performance Bonus", "Clean row performance bonus", 2_500m, isEarning: true, isTemp: true));
		}

		rows.AddRange(deductions
			.Where(x => x.Amount > 0)
			.Select(x => NewPayrollComponent(employeeId, x.Name, x.Description, x.Amount, isEarning: false, x.IsTemp)));

		return rows;
	}

	private EmployeePayrollComponent NewPayrollComponent(
		Guid employeeId,
		string name,
		string description,
		decimal amount,
		bool isEarning,
		bool isTemp = false) =>
		new(
			GuidGenerator.Create(),
			employeeId,
			name,
			componentId: null,
			description,
			amount,
			isFixed: true,
			isTemp,
			applicableMonth: null,
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning);

	private async Task SeedAttendanceAsync(
		Dictionary<string, Employee> employees,
		Guid educationMediumStageId,
		DateTime month)
	{
		var firstDay = new DateOnly(month.Year, month.Month, 1);
		var lastDay = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
		await DeleteAttendanceForRangeAsync(employees.Values.Select(e => e.Id).ToList(), firstDay, lastDay);

		foreach (var (key, employee) in employees)
		{
			var spec = BuildEmployeeSpecs().First(x => x.Key == key);
			var workingDayIndex = 0;
			for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
			{
				if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
				{
					continue;
				}

				workingDayIndex++;
				var status = ResolveAttendanceStatus(spec.AttendanceScenario, workingDayIndex);
				if (status == null)
				{
					continue;
				}

				await _attendanceRepository.InsertAsync(
					BuildAttendance(employee.Id, date, status.Value, educationMediumStageId),
					autoSave: false);
			}
		}
	}

	private async Task SeedPreviousMonthAttendanceAsync(
		Dictionary<string, Employee> employees,
		DateTime previousMonth,
		Guid educationMediumStageId)
	{
		var firstDay = new DateOnly(previousMonth.Year, previousMonth.Month, 1);
		var lastDay = new DateOnly(previousMonth.Year, previousMonth.Month, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));
		await DeleteAttendanceForRangeAsync(employees.Values.Select(e => e.Id).ToList(), firstDay, lastDay);

		foreach (var employee in employees.Values)
		{
			for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
			{
				if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
				{
					continue;
				}

				await _attendanceRepository.InsertAsync(
					BuildAttendance(employee.Id, date, EmployeeAttendanceStatusType.Present, educationMediumStageId),
					autoSave: false);
			}
		}
	}

	private async Task DeleteAttendanceForRangeAsync(List<Guid> employeeIds, DateOnly start, DateOnly end)
	{
		var existing = await _attendanceRepository.GetListAsync(a =>
			employeeIds.Contains(a.EmployeeId) &&
			a.AttendanceDate >= start &&
			a.AttendanceDate <= end);

		foreach (var row in existing)
		{
			await _attendanceRepository.DeleteAsync(row, autoSave: false);
		}
	}

	private static EmployeeAttendanceStatusType? ResolveAttendanceStatus(AttendanceScenario scenario, int workingDayIndex)
	{
		return scenario switch
		{
			AttendanceScenario.Clean => EmployeeAttendanceStatusType.Present,
			AttendanceScenario.TwoHalfDays when workingDayIndex is 5 or 13 => EmployeeAttendanceStatusType.HalfDayLeave,
			AttendanceScenario.ApprovedLeave when workingDayIndex is 7 or 8 => EmployeeAttendanceStatusType.FullDayLeave,
			AttendanceScenario.LowAttendance when workingDayIndex <= 3 => EmployeeAttendanceStatusType.Present,
			AttendanceScenario.LowAttendance => null,
			AttendanceScenario.OneLopDay when workingDayIndex == 10 => EmployeeAttendanceStatusType.FullDayLeave,
			_ => EmployeeAttendanceStatusType.Present
		};
	}

	private EmployeeAttendance BuildAttendance(
		Guid employeeId,
		DateOnly date,
		EmployeeAttendanceStatusType status,
		Guid educationMediumStageId)
	{
		DateTimeOffset? timeIn = null;
		DateTimeOffset? timeOut = null;
		if (status == EmployeeAttendanceStatusType.Present)
		{
			timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 8, 45, 0, IndiaOffset);
			timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, 16, 15, 0, IndiaOffset);
		}
		else if (status == EmployeeAttendanceStatusType.HalfDayLeave)
		{
			timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 8, 45, 0, IndiaOffset);
			timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, 12, 45, 0, IndiaOffset);
		}

		return new EmployeeAttendance
		{
			TenantId = CurrentTenant.Id,
			EmployeeId = employeeId,
			AttendanceDate = date,
			TimeIn = timeIn,
			TimeOut = timeOut,
			StatusType = status,
			EducationMediumStageId = educationMediumStageId
		};
	}

	private async Task SeedLeavesAsync(
		Dictionary<string, Employee> employees,
		AcademicYear academicYear,
		DateTime seedMonth)
	{
		var policies = await _leavePolicyRepository.GetListByAcademicYearIdAsync(academicYear.Id, includeInactive: false);
		var casual = policies.FirstOrDefault(p => string.Equals(p.LeaveType?.Type, "Casual Leave", StringComparison.OrdinalIgnoreCase));
		var sick = policies.FirstOrDefault(p => string.Equals(p.LeaveType?.Type, "Sick Leave", StringComparison.OrdinalIgnoreCase));
		var lop = policies.FirstOrDefault(p => string.Equals(p.LeaveType?.Type, "Loss of Pay", StringComparison.OrdinalIgnoreCase));
		if (casual == null || sick == null)
		{
			_logger.LogWarning("[HrManualTestDataSeeder] Skipped leave requests because leave policies are missing.");
			return;
		}

		var monthStart = new DateTime(seedMonth.Year, seedMonth.Month, 1);
		await EnsureLeaveAsync(employees["jignesh"].Id, casual.Id, GetBusinessDay(monthStart, 4), GetBusinessDay(monthStart, 4), "HR manual seed pending casual leave", EmployeeLeaveStatusType.Pending);
		await EnsureLeaveAsync(employees["nikhil"].Id, sick.Id, GetBusinessDay(monthStart, 7), GetBusinessDay(monthStart, 8), "HR manual seed approved sick leave", EmployeeLeaveStatusType.Approved);
		await EnsureLeaveAsync(employees["keyur"].Id, casual.Id, GetBusinessDay(monthStart, 11), GetBusinessDay(monthStart, 11), "HR manual seed rejected casual leave", EmployeeLeaveStatusType.Rejected, "Insufficient handover details.");
		await EnsureLeaveAsync(employees["vishal"].Id, casual.Id, GetBusinessDay(monthStart, 14), GetBusinessDay(monthStart, 14), "HR manual seed cancelled self leave", EmployeeLeaveStatusType.Cancelled);
		if (lop != null)
		{
			await EnsureLeaveAsync(employees["jagruti"].Id, lop.Id, GetBusinessDay(monthStart, 10), GetBusinessDay(monthStart, 10), "HR manual seed loss of pay leave", EmployeeLeaveStatusType.Approved);
		}
	}

	private async Task EnsureLeaveAsync(
		Guid employeeId,
		Guid policyId,
		DateTime start,
		DateTime end,
		string description,
		EmployeeLeaveStatusType status,
		string? rejectionReason = null)
	{
		var existing = await _leaveRepository.GetListAsync(l =>
			l.EmployeeId == employeeId &&
			l.LeaveTypeId == policyId &&
			l.Description == description);
		if (existing.Count > 0)
		{
			return;
		}

		var leave = await _leaveManager.CreateAsync(
			employeeId,
			policyId,
			new DateTimeOffset(start, IndiaOffset),
			new DateTimeOffset(end, IndiaOffset),
			description);
		await _leaveRepository.InsertAsync(leave, autoSave: true);
		if (status != EmployeeLeaveStatusType.Pending)
		{
			await _leaveManager.UpdateLeaveRequestStatusAsync(leave, status, rejectionReason);
		}
	}

	private static DateTime GetBusinessDay(DateTime monthStart, int businessDayNumber)
	{
		var count = 0;
		for (var date = monthStart.Date; date.Month == monthStart.Month; date = date.AddDays(1))
		{
			if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
			{
				continue;
			}

			count++;
			if (count == businessDayNumber)
			{
				return date;
			}
		}

		return monthStart.Date;
	}

	private async Task SeedPayrollRunStateAsync(Dictionary<string, Employee> employees, DateTime seedMonth)
	{
		var monthYear = seedMonth.ToString("yyyy-MM");
		var previousMonthYear = seedMonth.AddMonths(-1).ToString("yyyy-MM");
		var reviewerUserId = employees["parth"].UserId ?? employees["yagnik1"].UserId ?? Guid.Empty;
		var previousNetByEmployee = new Dictionary<Guid, decimal>();

		foreach (var employee in employees.Values)
		{
			var previousSlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employee.Id, previousMonthYear);
			if (previousSlip == null)
			{
				previousSlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(employee.Id, previousMonthYear, forceRegenerate: false);
				previousSlip.Finalize();
				await _salarySlipRepository.UpdateAsync(previousSlip, autoSave: false);
			}

			previousNetByEmployee[employee.Id] = previousSlip.NetSalary;
		}

		foreach (var key in new[] { "yagnik1", "yagnik2", "parth" })
		{
			var employee = employees[key];
			var slip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employee.Id, monthYear);
			if (slip == null)
			{
				slip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(employee.Id, monthYear, forceRegenerate: false);
				slip.Finalize();
				await _salarySlipRepository.UpdateAsync(slip, autoSave: false);
			}
		}

		var firstDay = new DateOnly(seedMonth.Year, seedMonth.Month, 1);
		var lastDay = new DateOnly(seedMonth.Year, seedMonth.Month, DateTime.DaysInMonth(seedMonth.Year, seedMonth.Month));
		var basisDays = CountWeekdays(firstDay, lastDay);
		var currentAttendance = await _attendanceRepository.GetListAsync(a =>
			employees.Values.Select(e => e.Id).Contains(a.EmployeeId) &&
			a.AttendanceDate >= firstDay &&
			a.AttendanceDate <= lastDay);

		foreach (var (key, employee) in employees)
		{
			var spec = BuildEmployeeSpecs().First(x => x.Key == key);
			var rows = currentAttendance.Where(a => a.EmployeeId == employee.Id).ToList();
			var components = await _payrollComponentRepository.GetByEmployeeIdAsync(employee.Id);
			var snapshot = await FindOrCreateSnapshotAsync(monthYear, employee.Id);
			ApplySnapshot(snapshot, employee, components, rows, basisDays, spec.PayrollState, reviewerUserId, previousNetByEmployee.GetValueOrDefault(employee.Id));
			await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);

			if (spec.PayrollState is PayrollState.Reviewed or PayrollState.Locked)
			{
				await EnsurePayrollReviewAsync(monthYear, employee.Id, reviewerUserId);
			}
		}
	}

	private async Task<PayrollRunItemSnapshot> FindOrCreateSnapshotAsync(string monthYear, Guid employeeId)
	{
		var query = await _payrollRunItemSnapshotRepository.GetQueryableAsync();
		var existing = query.FirstOrDefault(x =>
			x.TenantId == CurrentTenant.Id &&
			x.MonthYear == monthYear &&
			x.EmployeeId == employeeId);
		if (existing != null)
		{
			return existing;
		}

		var snapshot = new PayrollRunItemSnapshot(GuidGenerator.Create(), CurrentTenant.Id, monthYear, employeeId);
		await _payrollRunItemSnapshotRepository.InsertAsync(snapshot, autoSave: false);
		return snapshot;
	}

	private void ApplySnapshot(
		PayrollRunItemSnapshot snapshot,
		Employee employee,
		List<EmployeePayrollComponent> components,
		List<EmployeeAttendance> attendance,
		int basisDays,
		PayrollState state,
		Guid reviewerUserId,
		decimal previousNetPay)
	{
		var presentDays = attendance.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
		var halfDays = attendance.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
		var payableDays = presentDays + 0.5m * halfDays;
		var factor = basisDays <= 0 ? 1m : Math.Min(1m, payableDays / basisDays);
		var earnings = components
			.Where(c => c.IsEarning)
			.Select(c => new PayrollComponentPreview(c.Name, Math.Round(c.IsTemp ? c.Amount : c.Amount * factor, 2)))
			.ToList();
		var deductions = components
			.Where(c => !c.IsEarning)
			.Select(c => new PayrollComponentPreview(c.Name, Math.Round(c.IsTemp ? c.Amount : c.Amount * factor, 2)))
			.ToList();
		var gross = earnings.Sum(x => x.Amount);
		var totalDeduction = deductions.Sum(x => x.Amount);
		var netPay = gross - totalDeduction;
		var delta = netPay - previousNetPay;
		var deltaPercent = previousNetPay == 0 ? 0 : delta / previousNetPay * 100m;
		var issues = BuildIssueCodes(components, basisDays, payableDays, netPay, deltaPercent);

		snapshot.EmployeeName = employee.EmployeeName;
		snapshot.EmployeeIdentifier = employee.EmployeeCode ?? string.Empty;
		snapshot.IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching;
		snapshot.BasicSalary = earnings.Where(x => string.Equals(x.Name, "Basic", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
		snapshot.GrossSalary = gross;
		snapshot.TotalDeduction = totalDeduction;
		snapshot.NetPay = netPay;
		snapshot.PreviousMonthNetPay = previousNetPay;
		snapshot.NetPayDelta = delta;
		snapshot.NetPayDeltaPercent = deltaPercent;
		snapshot.IssueCodesJson = JsonSerializer.Serialize(issues);
		snapshot.PresentDays = presentDays;
		snapshot.HalfWorkingDays = halfDays;
		snapshot.TotalWorkingDays = basisDays;
		snapshot.PayableDays = payableDays;
		snapshot.ProrationFactor = factor;
		snapshot.DayCalculationMode = "working-days";
		snapshot.UsedDefaultDayCalculation = true;
		snapshot.EarningComponentsJson = JsonSerializer.Serialize(earnings);
		snapshot.DeductionComponentsJson = JsonSerializer.Serialize(deductions);
		snapshot.IsLocked = state == PayrollState.Locked;
		snapshot.ReviewStatus = state switch
		{
			PayrollState.Locked => "LOCKED",
			PayrollState.Reviewed => "REVIEWED",
			_ => "PENDING"
		};
		snapshot.ReviewedByUserId = state == PayrollState.Pending ? null : reviewerUserId;
		snapshot.ReviewedAt = state == PayrollState.Pending ? null : DateTime.UtcNow;
	}

	private static List<string> BuildIssueCodes(
		List<EmployeePayrollComponent> components,
		int basisDays,
		decimal payableDays,
		decimal netPay,
		decimal deltaPercent)
	{
		var issues = new List<string>();
		if (components.Count == 0)
		{
			issues.Add("MISSING_SALARY_STRUCTURE");
		}
		if (basisDays > 0 && payableDays <= 0)
		{
			issues.Add("NO_ATTENDANCE");
		}
		else if (basisDays > 0 && payableDays < basisDays)
		{
			issues.Add("LOW_ATTENDANCE");
		}
		if (netPay < 0)
		{
			issues.Add("NEGATIVE_NET_PAY");
		}
		if (Math.Abs(deltaPercent) >= 10m)
		{
			issues.Add("LARGE_SALARY_VARIANCE");
		}

		return issues;
	}

	private async Task EnsurePayrollReviewAsync(string monthYear, Guid employeeId, Guid reviewerUserId)
	{
		var query = await _payrollRunReviewRepository.GetQueryableAsync();
		var existing = query.FirstOrDefault(x =>
			x.TenantId == CurrentTenant.Id &&
			x.MonthYear == monthYear &&
			x.EmployeeId == employeeId);
		if (existing != null)
		{
			existing.Refresh(reviewerUserId, DateTime.UtcNow);
			await _payrollRunReviewRepository.UpdateAsync(existing, autoSave: false);
			return;
		}

		await _payrollRunReviewRepository.InsertAsync(
			new PayrollRunReview(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				monthYear,
				employeeId,
				reviewerUserId,
				DateTime.UtcNow),
			autoSave: false);
	}

	private static int CountWeekdays(DateOnly start, DateOnly end)
	{
		var count = 0;
		for (var date = start; date <= end; date = date.AddDays(1))
		{
			if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
			{
				count++;
			}
		}

		return count;
	}

	private sealed record HrManualEmployeeSpec(
		string Key,
		string FirstName,
		string LastName,
		string Email,
		string MobileNo,
		string RoleName,
		string Designation,
		string Department,
		EmployeeGroupType EmployeeGroup,
		GenderType Gender,
		MaritalStatusType MaritalStatus,
		decimal MonthlyGross,
		AttendanceScenario AttendanceScenario,
		PayrollState PayrollState)
	{
		public string DisplayName => $"{FirstName} {LastName}";
	}

	private sealed record PayrollComponentPreview(string Name, decimal Amount);

	private enum AttendanceScenario
	{
		Normal,
		Clean,
		TwoHalfDays,
		ApprovedLeave,
		LowAttendance,
		OneLopDay
	}

	private enum PayrollState
	{
		Pending,
		Reviewed,
		Locked
	}
}
