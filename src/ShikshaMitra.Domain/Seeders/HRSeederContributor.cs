using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class HRSeederContributor(
ICurrentTenant currentTenant,
IGuidGenerator guidGenerator,
IUnitOfWork unitOfWork,
IAcademicYearRepository academicYearRepository,
IEducationBoardRepository educationBoardRepository,
IEducationMediumRepository educationMediumRepository,
IEducationMediumStageRepository educationMediumStageRepository,
IStandardRepository standardRepository,
IDivisionRepository divisionRepository,
IPayElementRepository payElementRepository,
IPayrollStructureRepository payrollStructureRepository,
IEarningComponentRepository earningComponentRepository,
EarningComponentManager earningComponentManager,
IDeductionComponentRepository deductionComponentRepository,
DeductionComponentManager deductionComponentManager,
IEmployeeRepository employeeRepository,
IEmployeeAttendanceRepository employeeAttendanceRepository,
ILeaveTypeRepository leaveTypeRepository,
ILeavePolicyRepository leavePolicyRepository,
ICustomAttendanceRuleRepository customAttendanceRuleRepository,
IIdentityUserRepository userRepository,
IdentityUserManager userManager,
IIdentityRoleRepository roleRepository,
ITenantRepository tenantRepository) : BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	public override async Task SeedAsync(DataSeedContext context)
	{
		Guid tenantId = CurrentTenant?.Id ?? throw new Exception("TenantId is not set.");

		// Create Academic Year.
		var academicYear = await academicYearRepository.InsertAsync(new AcademicYear(
			GuidGenerator.Create(),
			tenantId,
			"2024-2025",
			new DateTime(2024, 4, 1),
			new DateTime(2025, 3, 31)
		));

		// Create Education Board.
		var educationBoard = await educationBoardRepository.InsertAsync(new EducationBoard(
			GuidGenerator.Create(),
			tenantId,
			EducationBoardType.CBSE,
			academicYear.Id
		));

		// Create Education Medium.
		var educationMedium = await educationMediumRepository.InsertAsync(new EducationMedium(
			GuidGenerator.Create(),
			tenantId,
			"English",
			educationBoard.Id
		));

		// Create Education Medium Stage.
		var educationMediumStage = await educationMediumStageRepository.InsertAsync(new EducationMediumStage(
			GuidGenerator.Create(),
			tenantId,
			"Primary",
			null,
			educationMedium.Id
		));

		// Create Standard.
		var standard = await standardRepository.InsertAsync(new Standard(
			GuidGenerator.Create(),
			tenantId,
			"1",
			6,
			educationMediumStage.Id,
			shiftId: null
		));

		// Create Division.
		var division = await divisionRepository.InsertAsync(new Division(
			GuidGenerator.Create(),
			tenantId,
			"A",
			standard.Id,
			30,
			"112"
		));

		// Store academic-related IDs in the seed context for use by the student seeder.
		context.Properties["AcademicYearId"] = academicYear.Id;
		context.Properties["EducationBoardId"] = educationBoard.Id;
		context.Properties["EducationMediumId"] = educationMedium.Id;
		context.Properties["EducationMediumStageId"] = educationMediumStage.Id;
		context.Properties["StandardId"] = standard.Id;
		context.Properties["DivisionId"] = division.Id;

		// Create Pay Element.
		var payElement = new PayElement(
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			code: "BE001",
			name: "Basic",
			type: PayElementType.Credit,
			isEmployeeSpecific: false,
			isDependent: false,
			dependent: null,
			isVisibleInPaySlip: true
		);
		payElement = await payElementRepository.InsertAsync(payElement);

		// Create Payroll Structure.
		var payrollStructure = new PayrollStructure(
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			payElementId: payElement.Id,
			payElementType: PayElementType.Credit,
			amount: 10000
		);
		await payrollStructureRepository.InsertAsync(payrollStructure);

		// Seed Earning Components (if not already seeded)
		await SeedEarningComponentsAsync(tenantId);
		
		// Seed Deduction Components (if not already seeded)
		await SeedDeductionComponentsAsync(tenantId);

		await SeedEmployeeLeaveManagementAsync(tenantId);

		// Retrieve the tenant information to decide on the email domain.
		var tenant = await tenantRepository.GetAsync(tenantId);
		// Try to get a domain or institute name from the tenant's extra properties.
		string instituteName = tenant.ExtraProperties.ContainsKey("InstituteName")
			? tenant.ExtraProperties["InstituteName"].ToString()
			: "defaultdomain";
		// Build the email dynamically based on the tenant's institute name.
		var emailDomain = instituteName.Trim().ToLower();
		var employeeEmail = $"teacher@{emailDomain}.com";

		// Create Employee user with the dynamically generated email.
		var employeeUser = new IdentityUser(
			GuidGenerator.Create(),
			$"teacher{emailDomain.ToLower()}",
			employeeEmail,
			tenantId
		)
		{
			Name = "Testing",
			Surname = "Teacher"
		};

		var password = "Temp@123";
		var userCreationResult = await userManager.CreateAsync(employeeUser, password);
		if (!userCreationResult.Succeeded)
		{
			throw new Exception("Failed to create employee user: " + string.Join(", ", userCreationResult.Errors));
		}

		// Assign the "Teacher" role to the employee user.
		var roleAssignmentResult = await userManager.AddToRoleAsync(employeeUser, "Teacher");
		if (!roleAssignmentResult.Succeeded)
		{
			throw new Exception("Failed to assign role 'Teacher' to the employee: " + string.Join(", ", roleAssignmentResult.Errors));
		}

		// Fetch the Teacher role from the repository.
		var teacherRole = await roleRepository.FindByNormalizedNameAsync("TEACHER");
		if (teacherRole == null)
		{
			throw new Exception("Teacher role not found.");
		}


		// Find admin role
		var adminRole = await roleRepository.FindByNormalizedNameAsync("ADMIN");

		List<Employee> employees = new List<Employee>();

		// Get queryable to check for existing employees
		var employeeQueryable = await employeeRepository.GetQueryableAsync();

		#region Create admin employee
		// Check if admin employee already exists
		var existingAdminEmployee = employeeQueryable.FirstOrDefault(e => e.EmployeeCode == "EMP001");
		if (existingAdminEmployee == null)
		{
			// get all users
			var users = await userRepository.GetListAsync();

		// Get the user id with email address
		var user = users.Find(u => u.Email == $"{emailDomain}@gmail.com");
		
			// Validate that admin user exists before creating employee
			if (user == null)
			{
				throw new Exception($"Admin user with email '{emailDomain}@gmail.com' not found. Please ensure the admin user is created before seeding employee data.");
			}

			employees.Add(new Employee(
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			userId: user.Id,
			educationMediumStageId: educationMediumStage.Id,
			profilePictureBlobName: null,
			employeeCode: "EMP001",
			lastName: "LastAdmin",
			firstName: "FirstAdmin",
			middleName: "Dineshbhai",
			shortName: "HVD",
			employeeStatusType: EmployeeStatusType.Active,
			dateOfBirth: DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
			familyCast: "Ahir",
			castType: CastType.General,
			genderType: GenderType.Male,
			religion: "Hindu",
			maritalStatus: MaritalStatusType.Married,
			bloodGroup: "O+",
			mobileNo: "+91-9876543210",
			secondaryMobileNo: "+91-9876543210",
			email: "ddvs@gmail.com",
			address1: "123 Main Stree",
			address2: "Mumbai",
			address3: "India",
			department: "IT",
			designation: "Software Engineer",
			dateOfJoining: DateOnly.FromDateTime(new DateTime(2021, 6, 1)),
			employeeGroup: EmployeeGroupType.Teaching,
			qualification: "Group A",
			roleId: adminRole.Id, // Store the Admin role ID here.
			identificationMark1: "Wear glasses",
			city: "Gandhinagar",
			state: new State("Gujarat", "GJ"),
			country: new Country("India", "In"),
			pinCode: "382421",
			fatherDetails: new ContactDetails("Robert Smith", "Engineer", "+91-9876543211", "456, Elm Street",null,null, "Mumbai", stateName: "Maharashtra", stateIsoCode: "MH", countryName: "India", countryIsoCode: "In", "400001"),
			motherDetails: new ContactDetails("Mary Smith", "Teacher", "+91-9876543212", "Parvat Patiya", null, null , "Surat", stateName: "Gujarat", stateIsoCode: "GJ", countryName: "India", countryIsoCode: "In", "395010"),
			spouseDetails: new ContactDetails("Jane Doe", "Doctor", "+91-9876543213", "ATPL, Trimandir Road",null,null, "Adalaj", stateName: "Gujarat", stateIsoCode: "GJ", countryName: "India", countryIsoCode: "In", "382421"),
			bankDetails: new BankDetails("ABC Bank", "Mumbai Main Branch", "John Doe", "123456789012", "ABC123456"),
			shiftId: null,
			workExperiences: new List<WorkExperience>
			{
				new WorkExperience(
					"XYZ Technologies",
					"789, Tech Park, Bangalore",
					DateOnly.FromDateTime(new DateTime(2015, 6, 1)),
					DateOnly.FromDateTime(new DateTime(2019, 12, 31)),
					"Development",
					"Software Developer",
					"Developed web applications and maintained databases",
					"Jane Doe, Senior Manager"
					)
				}
			));
		}


		#endregion

		#region create teacheremployee
		// Check if teacher employee already exists
		var existingTeacherEmployee = employeeQueryable.FirstOrDefault(e => e.EmployeeCode == "EMP002");
		if (existingTeacherEmployee == null)
		{
			// Create Employee record and store the Teacher role's ID.
			var teacherEmployee = new Employee(
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			userId: employeeUser.Id,
			educationMediumStageId: educationMediumStage.Id,
			profilePictureBlobName: null,
			employeeCode: "EMP002",
			lastName: "Vadher",
			firstName: "Hiren",
			middleName: "Dineshbhai",
			shortName: "HVD",
			employeeStatusType: EmployeeStatusType.Active,
			dateOfBirth: DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
			familyCast: "Ahir",
			castType: CastType.General,
			genderType: GenderType.Male,
			religion: "Hindu",
			maritalStatus: MaritalStatusType.Married,
			bloodGroup: "O+",
			mobileNo: "+91-9876543210",
			secondaryMobileNo: "+91-9876543210",
			email: employeeEmail,
			address1: "123 Main Stree",
			address2: "Mumbai",
			address3: "India", 
			department: "IT",
			designation: "Software Engineer",
			dateOfJoining: DateOnly.FromDateTime(new DateTime(2021, 6, 1)),
			employeeGroup: EmployeeGroupType.Teaching,
			qualification: "Group A",
			roleId: teacherRole.Id, // Store the Teacher role ID here.
			identificationMark1: "Wear glasses",
			city: "Gandhinagar",
			state: new State("Gujarat", "GJ"),
			country: new Country("India", "In"),
			pinCode: "382421",
			fatherDetails: new ContactDetails("Robert Smith", "Engineer", "+91-9876543211", "456, Elm Street",null,null, "Mumbai", stateName: "Maharashtra", stateIsoCode: "MH", countryName: "India", countryIsoCode: "In", "400001"),
			motherDetails: new ContactDetails("Mary Smith", "Teacher", "+91-9876543212", "Parvat Patiya", null, null, "Surat", stateName: "Gujarat", stateIsoCode: "GJ", countryName: "India", countryIsoCode: "In", "395010"),
			spouseDetails: new ContactDetails("Jane Doe", "Doctor", "+91-9876543213", "ATPL, Trimandir Road", null, null, "Adalaj", stateName: "Gujarat", stateIsoCode: "GJ", countryName: "India", countryIsoCode: "In", "382421"),
			bankDetails: new BankDetails("ABC Bank", "Mumbai Main Branch", "John Doe", "123456789012", "ABC123456"),
			workExperiences: new List<WorkExperience>
			{
				new WorkExperience(
					"XYZ Technologies",
					"789, Tech Park, Bangalore",
					DateOnly.FromDateTime(new DateTime(2015, 6, 1)),
					DateOnly.FromDateTime(new DateTime(2019, 12, 31)),
					"Development",
					"Software Developer",
					"Developed web applications and maintained databases",
					"Jane Doe, Senior Manager"
					)
				},
				shiftId: null
			);
			employees.Add(teacherEmployee);
		}
		#endregion

		// Only insert if there are new employees to add
		if (employees.Any())
		{
			await employeeRepository.InsertManyAsync(employees);
		}



		//// Create Employee Attendance.
		//var employeeAttendance = new EmployeeAttendance
		//{
		//	TenantId = tenantId,
		//	EmployeeId = adminRole.Id,
		//	TimeIn = new DateTimeOffset(new DateTime(2024, 6, 1, 9, 0, 0)),
		//	TimeOut = new DateTimeOffset(new DateTime(2024, 6, 1, 18, 0, 0)),
		//	StatusType = EmployeeAttendanceStatusType.Present,
		//};
		//await employeeAttendanceRepository.InsertAsync(employeeAttendance);

		//// Create Custom Attendance Rule.
		//var customAttendanceRule = new CustomAttendanceRule(
		//	id: GuidGenerator.Create(),
		//	tenantId: tenantId,
		//	name: "Custom Rule 1",
		//	type: EmployeeAttendanceStatusType.FullDayLeave,
		//	lateThreshold: 20,
		//	lateThresholdUnit: TimeUnitType.Minutes,
		//	halfDayThreshold: 20,
		//	halfDayThresholdUnit: TimeUnitType.Minutes,
		//	enableNotificationForUpperAuthority: true
		//);
		//await customAttendanceRuleRepository.InsertAsync(customAttendanceRule);

		//await CurrentUnitOfWork.SaveChangesAsync();
	}

	/// <summary>
	/// Seeds sample earning components for payroll management
	/// </summary>
	private async Task SeedEarningComponentsAsync(Guid tenantId)
	{
		// Check if earning components already exist to avoid duplicates
		var existingCount = await earningComponentRepository.GetCountAsync();
		if (existingCount > 0)
		{
			// Already seeded, skip
			return;
		}

		var earningComponents = new List<EarningComponent>();

		// 1. Basic Salary - Fixed component with no dependency
		var basicSalary = await earningComponentManager.CreateAsync(
			name: "Basic Salary",
			description: "Base salary component for all employees",
			componentType: EarningComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "30000",
			visibleInPayslip: true,
			displayOrder: 1
		);
		earningComponents.Add(basicSalary);

		// 2. House Rent Allowance (HRA) - Variable component, 40% of Basic
		var hra = await earningComponentManager.CreateAsync(
			name: "House Rent Allowance",
			description: "HRA - 40% of Basic Salary",
			componentType: EarningComponentType.Variable,
			dependOn: ComponentDependency.Basic,
			amount: "40%",
			visibleInPayslip: true,
			displayOrder: 2
		);
		earningComponents.Add(hra);

		// 3. Transport Allowance - Fixed component
		var transport = await earningComponentManager.CreateAsync(
			name: "Transport Allowance",
			description: "Monthly transportation allowance",
			componentType: EarningComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "3000",
			visibleInPayslip: true,
			displayOrder: 3
		);
		earningComponents.Add(transport);

		// 4. Medical Allowance - Fixed component
		var medical = await earningComponentManager.CreateAsync(
			name: "Medical Allowance",
			description: "Medical expenses reimbursement",
			componentType: EarningComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "2500",
			visibleInPayslip: true,
			displayOrder: 4
		);
		earningComponents.Add(medical);

		// 5. Dearness Allowance (DA) - Variable component, 50% of Basic
		var da = await earningComponentManager.CreateAsync(
			name: "Dearness Allowance",
			description: "DA - 50% of Basic Salary",
			componentType: EarningComponentType.Variable,
			dependOn: ComponentDependency.Basic,
			amount: "50%",
			visibleInPayslip: true,
			displayOrder: 5
		);
		earningComponents.Add(da);

		// 6. Special Allowance - Variable component, 10% of Gross
		var special = await earningComponentManager.CreateAsync(
			name: "Special Allowance",
			description: "Special allowance - 10% of Gross Salary",
			componentType: EarningComponentType.Variable,
			dependOn: ComponentDependency.Gross,
			amount: "10%",
			visibleInPayslip: false,
			displayOrder: 6
		);
		earningComponents.Add(special);

		// 7. Performance Bonus - Fixed component (given based on performance)
		var bonus = await earningComponentManager.CreateAsync(
			name: "Performance Bonus",
			description: "Annual performance-based bonus",
			componentType: EarningComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "5000",
			visibleInPayslip: true,
			displayOrder: 7
		);
		earningComponents.Add(bonus);

		// 8. Overtime Allowance - Fixed component
		var overtime = await earningComponentManager.CreateAsync(
			name: "Overtime Allowance",
			description: "Extra hours compensation",
			componentType: EarningComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "1500",
			visibleInPayslip: true,
			displayOrder: 8
		);
		earningComponents.Add(overtime);

		// Insert all earning components
		await earningComponentRepository.InsertManyAsync(earningComponents);
	}
	
	/// <summary>
	/// Seeds sample deduction components for payroll management
	/// </summary>
	private async Task SeedDeductionComponentsAsync(Guid tenantId)
	{
		// Check if deduction components already exist to avoid duplicates
		var existingCount = await deductionComponentRepository.GetCountAsync();
		if (existingCount > 0)
		{
			// Already seeded, skip
			return;
		}

		var deductionComponents = new List<DeductionComponent>();

		// 1. Provident Fund (PF) - Fixed component
		var pf = await deductionComponentManager.CreateAsync(
			name: "Provident Fund (PF)",
			description: "Mandatory employee provident fund contribution",
			componentType: DeductionComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "1800",
			visibleInPayslip: true,
			displayOrder: 1
		);
		deductionComponents.Add(pf);

		// 2. Professional Tax - Fixed component
		var pt = await deductionComponentManager.CreateAsync(
			name: "Professional Tax",
			description: "State professional tax deduction",
			componentType: DeductionComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "200",
			visibleInPayslip: true,
			displayOrder: 2
		);
		deductionComponents.Add(pt);

		// 3. Income Tax (TDS) - Variable component, percentage of Gross
		var tds = await deductionComponentManager.CreateAsync(
			name: "Income Tax (TDS)",
			description: "Tax Deducted at Source - 10% of Gross",
			componentType: DeductionComponentType.Variable,
			dependOn: ComponentDependency.Gross,
			amount: "10%",
			visibleInPayslip: true,
			displayOrder: 3
		);
		deductionComponents.Add(tds);

		// 4. Health Insurance - Fixed component
		var insurance = await deductionComponentManager.CreateAsync(
			name: "Health Insurance",
			description: "Monthly health insurance premium",
			componentType: DeductionComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "500",
			visibleInPayslip: true,
			displayOrder: 4
		);
		deductionComponents.Add(insurance);

		// 5. Loan Recovery - Variable component
		var loan = await deductionComponentManager.CreateAsync(
			name: "Loan Recovery",
			description: "Employee loan repayment deduction",
			componentType: DeductionComponentType.Variable,
			dependOn: ComponentDependency.Gross,
			amount: "5%",
			visibleInPayslip: true,
			displayOrder: 5
		);
		deductionComponents.Add(loan);

		// 6. ESI (Employee State Insurance) - Variable component, percentage of Gross
		var esi = await deductionComponentManager.CreateAsync(
			name: "ESI (Employee State Insurance)",
			description: "Employee State Insurance - 0.75% of Gross",
			componentType: DeductionComponentType.Variable,
			dependOn: ComponentDependency.Gross,
			amount: "0.75%",
			visibleInPayslip: true,
			displayOrder: 6
		);
		deductionComponents.Add(esi);

		// 7. Canteen Charges - Fixed component
		var canteen = await deductionComponentManager.CreateAsync(
			name: "Canteen Charges",
			description: "Monthly canteen facility charges",
			componentType: DeductionComponentType.Fixed,
			dependOn: ComponentDependency.None,
			amount: "300",
			visibleInPayslip: false,
			displayOrder: 7
		);
		deductionComponents.Add(canteen);

		// Insert all deduction components
		await deductionComponentRepository.InsertManyAsync(deductionComponents);
	}

	/// <summary>
	/// Seeds sample leave types and policies for employee leave management
	/// </summary>
	private async Task SeedEmployeeLeaveManagementAsync(Guid tenantId)
	{
		// Check if leave types already exist
		var leaveTypeCount = await leaveTypeRepository.GetCountAsync();
		if (leaveTypeCount > 0)
		{
			return; // Already seeded
		}

		// Seed Leave Types
		var leaveTypes = new List<LeaveType>();

		var paidLeave = new LeaveType(
			guidGenerator.Create(),
			tenantId,
			"Paid Leave",
			"#1E90FF"
		);
		leaveTypes.Add(paidLeave);

		var sickLeave = new LeaveType(
			guidGenerator.Create(),
			tenantId,
			"Sick Leave",
			"#FF4C4C"
		);
		leaveTypes.Add(sickLeave);

		var casualLeave = new LeaveType(
			guidGenerator.Create(),
			tenantId,
			"Casual Leave",
			"#FFB300"
		);
		leaveTypes.Add(casualLeave);

		var earnedLeave = new LeaveType(
			guidGenerator.Create(),
			tenantId,
			"Earned Leave",
			"#27AE60"
		);
		leaveTypes.Add(earnedLeave);

		await leaveTypeRepository.InsertManyAsync(leaveTypes);

		// Seed Leave Policies
		var policies = new List<LeavePolicy>();

		var sickPolicy = new LeavePolicy(
			guidGenerator.Create(),
			tenantId,
			"Sick Leave Policy",
			"Sick Leave",
			12,
			LeavePaidType.Paid,
			true,
			false,
			false,
			"12 days paid sick leave per year, can be carried forward"
		);
		policies.Add(sickPolicy);

		var casualPolicy = new LeavePolicy(
			guidGenerator.Create(),
			tenantId,
			"Casual Leave Policy",
			"Casual Leave",
			8,
			LeavePaidType.Paid,
			false,
			false,
			true,
			"8 days casual leave, requires supervisor approval"
		);
		policies.Add(casualPolicy);

		var earnedPolicy = new LeavePolicy(
			guidGenerator.Create(),
			tenantId,
			"Earned Leave Policy",
			"Earned Leave",
			15,
			LeavePaidType.Paid,
			true,
			true,
			false,
			"15 days earned leave, can be carried forward and encashed"
		);
		policies.Add(earnedPolicy);

		await leavePolicyRepository.InsertManyAsync(policies);
	}
}