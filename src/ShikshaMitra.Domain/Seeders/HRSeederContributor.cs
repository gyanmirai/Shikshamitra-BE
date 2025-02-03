using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
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
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra.Seeders;

namespace ShikshaMitra.Seeders;

public class HRSeederContributor : BaseSeederContributor
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IEducationBoardRepository _educationBoardRepository;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly IEducationMediumStageRepository _educationMediumStageRepository;
	private readonly IStandardRepository _standardRepository;
	private readonly IDivisionRepository _divisionRepository;
	private readonly IPayElementRepository _payElementRepository;
	private readonly IPayrollStructureRepository _payrollStructureRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly ICustomAttendanceRuleRepository _customAttendanceRuleRepository;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityRoleRepository _roleRepository;

	public HRSeederContributor(
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
		IEmployeeRepository employeeRepository,
		IEmployeeAttendanceRepository employeeAttendanceRepository,
		ICustomAttendanceRuleRepository customAttendanceRuleRepository,
		IIdentityUserRepository userRepository,
		IdentityUserManager userManager,
		IIdentityRoleRepository roleRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_academicYearRepository = academicYearRepository;
		_educationBoardRepository = educationBoardRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_payElementRepository = payElementRepository;
		_payrollStructureRepository = payrollStructureRepository;
		_employeeRepository = employeeRepository;
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_customAttendanceRuleRepository = customAttendanceRuleRepository;
		_userManager = userManager;
		_roleRepository = roleRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		Guid tenantId = CurrentTenant?.Id ?? throw new Exception("TenantId is not set.");

		// Create Academic Year.
		var academicYear = await _academicYearRepository.InsertAsync(new AcademicYear(
			GuidGenerator.Create(),
			tenantId,
			"2024-2025",
			new DateTime(2024, 4, 1),
			new DateTime(2025, 3, 31)
		));

		// Create Education Board.
		var educationBoard = await _educationBoardRepository.InsertAsync(new EducationBoard(
			GuidGenerator.Create(),
			tenantId,
			EducationBoardType.CBSE,
			academicYear.Id
		));

		// Create Education Medium.
		var educationMedium = await _educationMediumRepository.InsertAsync(new EducationMedium(
			GuidGenerator.Create(),
			tenantId,
			"English",
			educationBoard.Id
		));

		// Create Education Medium Stage.
		var educationMediumStage = await _educationMediumStageRepository.InsertAsync(new EducationMediumStage(
			GuidGenerator.Create(),
			tenantId,
			"Primary",
			educationMedium.Id
		));

		// Create Standard.
		var standard = await _standardRepository.InsertAsync(new Standard(
			GuidGenerator.Create(),
			tenantId,
			"1",
			educationMediumStage.Id
		));

		// Create Division.
		var division = await _divisionRepository.InsertAsync(new Division(
			GuidGenerator.Create(),
			tenantId,
			"A",
			standard.Id,
			30
		));

		// Store academic-related IDs in the seed context for use by the student seeder.
		context.Properties["AcademicYearId"] = academicYear.Id;
		context.Properties["EducationBoardId"] = educationBoard.Id;
		context.Properties["EducationMediumId"] = educationMedium.Id;
		context.Properties["StandardId"] = standard.Id;

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
		payElement = await _payElementRepository.InsertAsync(payElement);

		// Create Payroll Structure.
		var payrollStructure = new PayrollStructure(
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			payElementId: payElement.Id,
			payElementType: PayElementType.Credit,
			amount: 10000
		);
		await _payrollStructureRepository.InsertAsync(payrollStructure);

		// Create Employee user.
		var employeeId = GuidGenerator.Create();
		var employeeUser = new IdentityUser(
			GuidGenerator.Create(),
			"hvadher",
			"Hiren.vadher@ddvs.com",
			tenantId
		)
		{
			Name = "Hiren",
			Surname = "Vadher"
		};

		var password = "Temp@123";
		var userCreationResult = await _userManager.CreateAsync(employeeUser, password);
		if (!userCreationResult.Succeeded)
		{
			throw new Exception("Failed to create employee user: " + string.Join(", ", userCreationResult.Errors));
		}

		// Assign the "Teacher" role to the employee user.
		var roleAssignmentResult = await _userManager.AddToRoleAsync(employeeUser, "Teacher");
		if (!roleAssignmentResult.Succeeded)
		{
			throw new Exception("Failed to assign role 'Teacher' to the employee: " + string.Join(", ", roleAssignmentResult.Errors));
		}

		// Fetch the Teacher role from the repository.
		var teacherRole = await _roleRepository.FindByNormalizedNameAsync("TEACHER");
		if (teacherRole == null)
		{
			throw new Exception("Teacher role not found.");
		}

		// Create Employee record and store the Teacher role's ID.
		var employee = new Employee(
			id: employeeId,
			tenantId: tenantId,
			userId: employeeUser.Id,
			educationMediumStageId: educationMediumStage.Id,
			profilePicture: Convert.FromBase64String("Qk1CAAAAAAAAADYAAAAoAAAAAQAAAAD4AAABABgAAAAAACgAAAEDAAEAAAAAAAAAAADEDgAAxA4AAAAAAAAAAAAA//8A/wAA//8A/wD/"),
			employeeCode: "EMP001",
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
			email: "hvadher@ddvs.com",
			address: "123 Main Street, Mumbai",
			department: "IT",
			designation: "Software Engineer",
			dateOfJoining: DateOnly.FromDateTime(new DateTime(2021, 6, 1)),
			employeeGroup: EmployeeGroupType.Teaching,
			qualification: "Group A",
			roleId: teacherRole.Id, // Store the Teacher role ID here.
			identificationMark1: "Wear glasses",
			city: "Gandhinagar",
			state: "Gujarat",
			pinCode: "382421",
			fatherDetails: new ContactDetails("Robert Smith", "Engineer", "+91-9876543211", "456, Elm Street", "Mumbai", "Maharashtra", "400001"),
			motherDetails: new ContactDetails("Mary Smith", "Teacher", "+91-9876543212", "Parvat Patiya", "Surat", "Gujarat", "395010"),
			spouseDetails: new ContactDetails("Jane Doe", "Doctor", "+91-9876543213", "ATPL, Trimandir Road", "Adalaj", "Gujarat", "382421"),
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
			}
		);

		await _employeeRepository.InsertAsync(employee);

		// Create Employee Attendance.
		var employeeAttendance = new EmployeeAttendance
		{
			TenantId = tenantId,
			EmployeeId = employeeId,
			TimeIn = new DateTimeOffset(new DateTime(2024, 6, 1, 9, 0, 0)),
			TimeOut = new DateTimeOffset(new DateTime(2024, 6, 1, 18, 0, 0)),
			StatusType = EmployeeAttendanceStatusType.Present,
		};
		await _employeeAttendanceRepository.InsertAsync(employeeAttendance);

		// Create Custom Attendance Rule.
		var customAttendanceRule = new CustomAttendanceRule(
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			name: "Custom Rule 1",
			type: EmployeeAttendanceStatusType.FullDayLeave,
			lateThreshold: 20,
			lateThresholdUnit: TimeUnitType.Minutes,
			halfDayThreshold: 20,
			halfDayThresholdUnit: TimeUnitType.Minutes,
			enableNotificationForUpperAuthority: true
		);
		await _customAttendanceRuleRepository.InsertAsync(customAttendanceRule);

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
