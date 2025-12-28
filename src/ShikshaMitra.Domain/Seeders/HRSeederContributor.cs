using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
	private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
	private readonly LeaveTypeManager _leaveTypeManager;
	private readonly IRepository<EarningComponent, Guid> _earningComponentRepository;
	private readonly EarningComponentManager _earningComponentManager;
	private readonly IRepository<DeductionComponent, Guid> _deductionComponentRepository;
	private readonly DeductionComponentManager _deductionComponentManager;
	private readonly IRepository<LeavePolicy, Guid> _leavePolicyRepository;
	private readonly LeavePolicyManager _leavePolicyManager;
	private readonly ILeaveTypeRepository _leaveTypeQueryRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly EmployeeManager _employeeManager;
	private readonly IIdentityRoleRepository _roleRepository;

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
		LeavePolicyManager leavePolicyManager,
		ILeaveTypeRepository leaveTypeQueryRepository,
		IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
		IRepository<ShiftManagement, Guid> shiftRepository,
		IEmployeeRepository employeeRepository,
		EmployeeManager employeeManager,
		IIdentityRoleRepository roleRepository)
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
		_leaveTypeQueryRepository = leaveTypeQueryRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_shiftRepository = shiftRepository;
		_employeeRepository = employeeRepository;
		_employeeManager = employeeManager;
		_roleRepository = roleRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// Phase 1: Seed payroll components
		await SeedEarningComponentsAsync();
		await SeedDeductionComponentsAsync();

		// Phase 2: Seed shift management (required for employees)
		await SeedShiftManagementAsync();

		// Phase 3: Seed leave types and policies
		await SeedLeaveTypesAndPoliciesAsync();

		// Phase 4: Seed employees
		await SeedEmployeesAsync();
	}

	private async Task SeedLeaveTypesAndPoliciesAsync()
	{
		// Check if already seeded
		if (await _leaveTypeRepository.AnyAsync())
			return;

		// Get the first available EducationMediumStage
		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync())
			.FirstOrDefault();

		if (educationMediumStage == null)
		{
			// Skip seeding if no EducationMediumStage is available
			// This can happen if academic setup hasn't been seeded yet
			return;
		}

		var educationMediumStageId = educationMediumStage.Id;

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
			var leaveType = await _leaveTypeManager.CreateAsync(educationMediumStageId, type, color, description);
			await _leaveTypeRepository.InsertAsync(leaveType, autoSave: true);
		}

		// Seed leave policies
		await SeedLeavePoliciesAsync(educationMediumStageId);
	}

	private async Task SeedLeavePoliciesAsync(Guid educationMediumStageId)
	{
		// Check if already seeded
		if (await _leavePolicyRepository.AnyAsync())
			return;

		// Get the leave type IDs by name
		async Task<Guid?> GetLeaveTypeIdAsync(string typeName)
		{
			var leaveType = await _leaveTypeQueryRepository.FindByTypeAsync(educationMediumStageId, typeName);
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
				educationMediumStageId,
				policy.PolicyName,
				leaveTypeId.Value,
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
				null, // customComponentId
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
				null, // customComponentId
				component.Amount,
				component.VisibleInPayslip,
				component.DisplayOrder
			);
						await _deductionComponentRepository.InsertAsync(deductionComponent, autoSave: true);
					}
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

				private async Task SeedEmployeesAsync()
				{
					// Check if already seeded
					if (await _employeeRepository.AnyAsync())
						return;

					// Get required dependencies
					var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
					if (educationMediumStage == null)
						return; // Skip if no academic setup

					var fullDayShift = (await _shiftRepository.GetQueryableAsync())
						.FirstOrDefault(s => s.ShiftName == "Full Day Shift");

					// Seed teaching staff
					await SeedTeachingStaffAsync(educationMediumStage.Id, fullDayShift?.Id);

					// Seed non-teaching staff
					await SeedNonTeachingStaffAsync(educationMediumStage.Id, fullDayShift?.Id);
				}

				private async Task SeedTeachingStaffAsync(Guid educationMediumStageId, Guid? shiftId)
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
							RoleName = "Principal",
							Designation = "Principal",
							Department = "Administration",
							DateOfBirth = new DateOnly(1975, 5, 15),
							DateOfJoining = new DateOnly(2010, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.Ed, PhD in Education Management",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400001"
						},
						new
						{
							FirstName = "Priya",
							LastName = "Sharma",
							MiddleName = "Devi",
							Email = "priya.sharma@school.edu",
							Mobile = "9876543211",
							RoleName = "Vice Principal",
							Designation = "Vice Principal",
							Department = "Administration",
							DateOfBirth = new DateOnly(1978, 8, 22),
							DateOfJoining = new DateOnly(2012, 7, 15),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.Sc, M.Ed",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400002"
						},
						new
						{
							FirstName = "Amit",
							LastName = "Patel",
							MiddleName = (string?)null,
							Email = "amit.patel@school.edu",
							Mobile = "9876543212",
							RoleName = "Head of Department",
							Designation = "Head of Science Department",
							Department = "Science",
							DateOfBirth = new DateOnly(1980, 3, 10),
							DateOfJoining = new DateOnly(2013, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.Sc Physics, B.Ed",
							City = "Pune",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "411001"
						},
						new
						{
							FirstName = "Sunita",
							LastName = "Desai",
							MiddleName = (string?)null,
							Email = "sunita.desai@school.edu",
							Mobile = "9876543213",
							RoleName = "Head of Department",
							Designation = "Head of Mathematics Department",
							Department = "Mathematics",
							DateOfBirth = new DateOnly(1982, 11, 5),
							DateOfJoining = new DateOnly(2014, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							Qualification = "M.Sc Mathematics, B.Ed",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400003"
						},
						new
						{
							FirstName = "Vikram",
							LastName = "Singh",
							MiddleName = "Kumar",
							Email = "vikram.singh@school.edu",
							Mobile = "9876543214",
							RoleName = "Teacher",
							Designation = "Senior Teacher - English",
							Department = "Languages",
							DateOfBirth = new DateOnly(1985, 1, 20),
							DateOfJoining = new DateOnly(2015, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.A English, B.Ed",
							City = "Thane",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400601"
						},
						new
						{
							FirstName = "Meera",
							LastName = "Joshi",
							MiddleName = (string?)null,
							Email = "meera.joshi@school.edu",
							Mobile = "9876543215",
							RoleName = "Teacher",
							Designation = "Teacher - Chemistry",
							Department = "Science",
							DateOfBirth = new DateOnly(1987, 6, 12),
							DateOfJoining = new DateOnly(2016, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.Sc Chemistry, B.Ed",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400004"
						},
						new
						{
							FirstName = "Arjun",
							LastName = "Reddy",
							MiddleName = (string?)null,
							Email = "arjun.reddy@school.edu",
							Mobile = "9876543216",
							RoleName = "Teacher",
							Designation = "Teacher - Biology",
							Department = "Science",
							DateOfBirth = new DateOnly(1988, 9, 8),
							DateOfJoining = new DateOnly(2017, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							Qualification = "M.Sc Biology, B.Ed",
							City = "Navi Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400701"
						},
						new
						{
							FirstName = "Kavita",
							LastName = "Nair",
							MiddleName = (string?)null,
							Email = "kavita.nair@school.edu",
							Mobile = "9876543217",
							RoleName = "Teacher",
							Designation = "Teacher - History",
							Department = "Social Studies",
							DateOfBirth = new DateOnly(1986, 4, 25),
							DateOfJoining = new DateOnly(2016, 6, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.A History, B.Ed",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400005"
						},
						new
						{
							FirstName = "Deepak",
							LastName = "Mehta",
							MiddleName = (string?)null,
							Email = "deepak.mehta@school.edu",
							Mobile = "9876543218",
							RoleName = "Teacher",
							Designation = "Teacher - Computer Science",
							Department = "Computer Science",
							DateOfBirth = new DateOnly(1989, 7, 18),
							DateOfJoining = new DateOnly(2018, 7, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							Qualification = "B.Tech CS, B.Ed",
							City = "Pune",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "411002"
						},
						new
						{
							FirstName = "Anjali",
							LastName = "Rao",
							MiddleName = (string?)null,
							Email = "anjali.rao@school.edu",
							Mobile = "9876543219",
							RoleName = "Teacher",
							Designation = "Teacher - Hindi",
							Department = "Languages",
							DateOfBirth = new DateOnly(1990, 2, 14),
							DateOfJoining = new DateOnly(2019, 6, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							Qualification = "M.A Hindi, B.Ed",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400006"
						}
					};

					foreach (var emp in teachingEmployees)
					{
						var role = await _roleRepository.FindByNormalizedNameAsync(emp.RoleName.ToUpperInvariant());
						if (role == null) continue;

						var employee = await _employeeManager.CreateEmployeeAsync(
							roleId: role.Id,
							educationMediumStageId: educationMediumStageId,
							firstName: emp.FirstName,
							lastName: emp.LastName,
							middleName: emp.MiddleName,
							shortName: $"{emp.FirstName[0]}.{emp.LastName}",
							employeeStatusType: EmployeeStatusType.Active,
							dateOfBirth: emp.DateOfBirth,
							genderType: emp.Gender,
							maritalStatus: emp.MaritalStatus,
							mobileNo: emp.Mobile,
							email: emp.Email,
							designation: emp.Designation,
							department: emp.Department,
							dateOfJoining: emp.DateOfJoining,
							employeeGroup: EmployeeGroupType.Teaching,
							qualification: emp.Qualification,
							city: emp.City,
							state: emp.State,
							country: emp.Country,
							pinCode: emp.PinCode,
							shiftId: shiftId
						);

							await _employeeRepository.InsertAsync(employee, autoSave: false);
						}

						// Save all teaching staff at once
						await CurrentUnitOfWork.SaveChangesAsync();
				}

				private async Task SeedNonTeachingStaffAsync(Guid educationMediumStageId, Guid? shiftId)
				{
					var nonTeachingEmployees = new[]
					{
						new
						{
							FirstName = "Ramesh",
							LastName = "Gupta",
							MiddleName = (string?)null,
							Email = "ramesh.gupta@school.edu",
							Mobile = "9876543220",
							RoleName = "Administrative Manager",
							Designation = "Administrative Manager",
							Department = "Administration",
							DateOfBirth = new DateOnly(1977, 10, 5),
							DateOfJoining = new DateOnly(2011, 4, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "MBA in Administration",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400007"
						},
						new
						{
							FirstName = "Neha",
							LastName = "Kulkarni",
							MiddleName = (string?)null,
							Email = "neha.kulkarni@school.edu",
							Mobile = "9876543221",
							RoleName = "HR Manager",
							Designation = "HR Manager",
							Department = "Human Resources",
							DateOfBirth = new DateOnly(1983, 3, 18),
							DateOfJoining = new DateOnly(2014, 5, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "MBA in HR Management",
							City = "Pune",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "411003"
						},
						new
						{
							FirstName = "Suresh",
							LastName = "Patil",
							MiddleName = (string?)null,
							Email = "suresh.patil@school.edu",
							Mobile = "9876543222",
							RoleName = "Finance Officer",
							Designation = "Finance Officer",
							Department = "Finance",
							DateOfBirth = new DateOnly(1979, 12, 10),
							DateOfJoining = new DateOnly(2012, 6, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Married,
							Qualification = "M.Com, CA",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400008"
						},
						new
						{
							FirstName = "Rahul",
							LastName = "Verma",
							MiddleName = (string?)null,
							Email = "rahul.verma@school.edu",
							Mobile = "9876543223",
							RoleName = "IT Administrator",
							Designation = "IT Administrator",
							Department = "Information Technology",
							DateOfBirth = new DateOnly(1991, 5, 22),
							DateOfJoining = new DateOnly(2019, 8, 1),
							Gender = GenderType.Male,
							MaritalStatus = MaritalStatusType.Unmarried,
							Qualification = "B.Tech IT",
							City = "Mumbai",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400009"
						},
						new
						{
							FirstName = "Pooja",
							LastName = "Shah",
							MiddleName = (string?)null,
							Email = "pooja.shah@school.edu",
							Mobile = "9876543224",
							RoleName = "Office Assistant",
							Designation = "Office Assistant",
							Department = "Administration",
							DateOfBirth = new DateOnly(1992, 8, 30),
							DateOfJoining = new DateOnly(2020, 7, 1),
							Gender = GenderType.Female,
							MaritalStatus = MaritalStatusType.Unmarried,
							Qualification = "B.Com",
							City = "Thane",
							State = new State("Maharashtra", "MH"),
							Country = new Country("India", "IN"),
							PinCode = "400602"
						}
					};

					foreach (var emp in nonTeachingEmployees)
					{
						var role = await _roleRepository.FindByNormalizedNameAsync(emp.RoleName.ToUpperInvariant());
						if (role == null) continue;

						var employee = await _employeeManager.CreateEmployeeAsync(
							roleId: role.Id,
							educationMediumStageId: educationMediumStageId,
							firstName: emp.FirstName,
							lastName: emp.LastName,
							middleName: emp.MiddleName,
							shortName: $"{emp.FirstName[0]}.{emp.LastName}",
							employeeStatusType: EmployeeStatusType.Active,
							dateOfBirth: emp.DateOfBirth,
							genderType: emp.Gender,
							maritalStatus: emp.MaritalStatus,
							mobileNo: emp.Mobile,
							email: emp.Email,
							designation: emp.Designation,
							department: emp.Department,
							dateOfJoining: emp.DateOfJoining,
							employeeGroup: EmployeeGroupType.NonTeaching,
							qualification: emp.Qualification,
							city: emp.City,
							state: emp.State,
							country: emp.Country,
							pinCode: emp.PinCode,
							shiftId: shiftId
						);

							await _employeeRepository.InsertAsync(employee, autoSave: false);
						}

						// Save all non-teaching staff at once
						await CurrentUnitOfWork.SaveChangesAsync();
				}
			}
