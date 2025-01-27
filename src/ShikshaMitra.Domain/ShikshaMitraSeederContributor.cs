using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.TenantManagement;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Uow;

namespace ShikshaMitra
{
	public class ShikshaMitraSeederContributor(
		IDistributedEventBus distributedEventBus,
		ICurrentTenant currentTenant,
		ITenantManager tenantManager,
		IIdentityRoleRepository roleRepository,
		ITenantRepository tenantRepository,
		IUnitOfWork currentUnitOfWork,
		IDataSeeder dataSeeder,
		IAcademicYearRepository academicYearRepository,
		IEducationBoardRepository educationBoardRepository,
		IEducationMediumRepository educationMediumRepository,
		IEducationMediumStageRepository educationMediumStageRepository,
		IStandardRepository standardRepository,
		IDivisionRepository divisionRepository,
		IIdentityUserRepository userRepository,
		IGuidGenerator guidGenerator,
		IEmployeeRepository employeeRepository,
		IEmployeeAttendanceRepository employeeAttendanceRepository,
		ICustomAttendanceRuleRepository customAttendanceRuleRepository,
		IPayElementRepository payElementRepository,
		IPayrollStructureRepository payrollStructureRepository,
		//IRepository<AutomatedNotificationsSettings, Guid> automatedNotificationsSettingsRepository,
		IdentityUserManager userManager) : IDataSeedContributor, ITransientDependency
	{
		public async Task SeedAsync(DataSeedContext context)
		{
			#region CheckTenantExistence

			if (await tenantRepository.GetCountAsync() > 0)
			{
				return;
			}

			#endregion

			#region CreateTenant

			TenantCreateDto input = new TenantCreateDto()
			{
				Name = "DDVS",
				AdminEmailAddress = "ddvs@gmail.com",
				AdminPassword = "Temp@123",
				ExtraProperties =
				{
					{ "InstituteName", "DDVS" },
					{ "Address", "Parvat Patiya" },
					{ "City", "Surat" },
					{ "State", "Gujarat" },
					{ "Country", "India" },
					{ "PostalCode", "395010" },
					{ "ContactNo", "+91-9876543210" },
				}
			};
			var tenant = await tenantManager.CreateAsync(input.Name);
			input.MapExtraPropertiesTo(tenant);

			await tenantRepository.InsertAsync(tenant);

			await currentUnitOfWork.SaveChangesAsync();

			await distributedEventBus.PublishAsync(
				new TenantCreatedEto
				{
					Id = tenant.Id,
					Name = tenant.Name,
					Properties =
					{
						{ "AdminEmail", input.AdminEmailAddress }, { "AdminPassword", input.AdminPassword }
					}
				});

			# endregion

			using (currentTenant.Change(tenant.Id))
			{
				await dataSeeder.SeedAsync(
					new DataSeedContext(tenant.Id)
						.WithProperty("AdminEmail", input.AdminEmailAddress)
						.WithProperty("AdminPassword", input.AdminPassword)
				);

				// get all users
				var users = await userRepository.GetListAsync();

				// Get the user id with email address
				var user = users.Find(u => u.Email == input.AdminEmailAddress);

				# region create roles

				List<IdentityRole> roles = [new IdentityRole(guidGenerator.Create(), "Teacher", tenant.Id)];

				// Add roles to the database
				foreach (var role in roles)
				{
					await roleRepository.InsertAsync(role);
				}

				# endregion

				#region TenantSettings

				var academicYear = await academicYearRepository.InsertAsync(new AcademicYear(
					guidGenerator.Create(),
					tenant.Id,
					"2024-2025",
					new DateTime(2024, 4, 1),
					new DateTime(2025, 3, 31)
				));


				var educationBoard = await educationBoardRepository.InsertAsync(new EducationBoard(
					guidGenerator.Create(),
					tenant.Id,
					EducationBoardType.CBSE,
					academicYear.Id
				));

				var educationMedium = await educationMediumRepository.InsertAsync(new EducationMedium(
					guidGenerator.Create(),
					tenant.Id,
					"English",
					educationBoard.Id
				));

				var educationMediumStage = await educationMediumStageRepository.InsertAsync(new EducationMediumStage(
					guidGenerator.Create(),
					tenant.Id,
					"Primary",
					educationMedium.Id
				));

				var standard = await standardRepository.InsertAsync(new Standard(
					guidGenerator.Create(),
					tenant.Id,
					"1",
					educationMediumStage.Id
				));

				var division = await divisionRepository.InsertAsync(new Division(
					guidGenerator.Create(),
					tenant.Id,
					"A",
					standard.Id,
					30
				));

				#endregion

				# region HR

				#region Create PayElement

				PayElement payElement = new PayElement(
					id: guidGenerator.Create(),
					tenantId: tenant.Id,
					code: "BE001",
					name: "Basic",
					type: PayElementType.Credit,
					isEmployeeSpecific: false,
					isDependent: false,
					dependent: null,
					isVisibleInPaySlip: true
				);

				payElement = await payElementRepository.InsertAsync(payElement);

				#endregion

				#region Create PayRollStructure

				var payrollStructure = new PayrollStructure(
					id: guidGenerator.Create(),
					tenantId: tenant.Id,
					educationMediumStageId: educationMediumStage.Id,
					payElementId: payElement.Id,
					payrollStructureType: PayrollStructureType.Earnings,
					amount: 10000
				);


				await payrollStructureRepository.InsertAsync(payrollStructure);

				#endregion

				#region Create Employee

				var employeeId = guidGenerator.Create();

				var employee = new Employee(
					id: employeeId,
					tenantId: tenant.Id,
					userId: user.Id,
					educationMediumStageId: educationMediumStage.Id,
					profilePicture: Convert.FromBase64String(
						"Qk1CAAAAAAAAADYAAAAoAAAAAQAAAAD4AAABABgAAAAAACgAAAEDAAEAAAAAAAAAAADEDgAAxA4AAAAAAAAAAAAA//8A/wAA//8A/wD/"),
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
					employeeGroup: null,
					qualification: "Group A",
					roleId: roles.Find(r => r.Name == "Teacher")?.Id,
					identificationMark1: "Wear glasses",
					city: "Gandhinagar",
					state: "Gujarat",
					pinCode: "382421",
					new ContactDetails("Robert Smith", "Engineer", "+91-9876543211", "456, Elm Street", city: "Mumbai",
						state: "Maharashtra", pinCode: "400001"),
					new ContactDetails("Mary Smith", "Teacher", "+91-9876543212", "Parvat Patiya", city: "Surat",
						state: "Gujarat", pinCode: "395010"),
					new ContactDetails("Jane Doe", "Doctor", "+91-9876543213", "ATPL, Trimandir Road", city: "Adalaj",
						state: "Gujarat", pinCode: "382421"),
					new BankDetails("ABC Bank", "Mumbai Main Branch", "John Doe", "123456789012", "ABC123456"),
					new List<WorkExperience>
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

				await employeeRepository.InsertAsync(employee);

				#endregion

				# region Create current month attendance for employee

				var startOfMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 3);

				var employeeAttendance = new EmployeeAttendance()
				{
					TenantId = tenant.Id,
					EmployeeId = employeeId,
					TimeIn = new DateTimeOffset(new DateTime(2024, 6, 1, 9, 0, 0)),
					TimeOut = new DateTimeOffset(new DateTime(2024, 6, 1, 18, 0, 0)),
					StatusType = EmployeeAttendanceStatusType.Present,
				};

				await employeeAttendanceRepository.InsertAsync(employeeAttendance);

				#endregion

				# region Create Custom Rules for Employee

				var customAttendanceRule = new CustomAttendanceRule(
					id: guidGenerator.Create(),
					tenantId: tenant.Id,
					name: "Custom Rule 1",
					type: EmployeeAttendanceStatusType.FullDayLeave,
					lateThreshold: 20,
					lateThresholdUnit: TimeUnitType.Minutes,
					halfDayThreshold: 20,
					halfDayThresholdUnit: TimeUnitType.Minutes,
					enableNotificationForUpperAuthority: true
				);

				await customAttendanceRuleRepository.InsertAsync(customAttendanceRule);

				#endregion

				# region Create Automated Notification Settings

				var automatedNotificationsSettings = new AutomatedNotificationsSettings(
					id: guidGenerator.Create(),
					tenantId: tenant.Id,
					employeeAttendanceStatusType: EmployeeAttendanceStatusType.FullDayLeave,
					notificationMethods: new List<NotificationMethodType>
					{
						NotificationMethodType.Email, NotificationMethodType.SMS
					}
				);

				//await automatedNotificationsSettingsRepository.InsertAsync(automatedNotificationsSettings);

				# endregion

				# endregion
			}
		}
	}
}