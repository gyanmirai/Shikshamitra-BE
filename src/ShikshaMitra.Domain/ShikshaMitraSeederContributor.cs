using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
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
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.TenantManagement;
using Volo.Abp.Identity;

namespace ShikshaMitra
{
	public class ShikshaMitraSeederContributor(
		IAcademicYearRepository academicYearRepository,
		IEducationBoardRepository educationBoardRepository,
		IEducationMediumRepository educationMediumRepository,
		IEducationMediumStageRepository educationMediumStageRepository,
		IStandardRepository standardRepository,
		IDivisionRepository divisionRepository,
		ITenantManager tenantManager,
		ITenantRepository tenantRepository,
		IIdentityUserRepository userRepository,
		IGuidGenerator guidGenerator,
		IStudentRepository studentRepository,
		IEmployeeRepository employeeRepository,
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

			var tenantName = "DDVS";
			var tenant = await tenantRepository.FindByNameAsync(tenantName);
			if (tenant == null)
			{
				tenant = await tenantManager.CreateAsync(tenantName);
				await tenantRepository.InsertAsync(tenant);
			}

			#endregion

			#region CreateTenantUser

			var userName = "adminddvs";
			var user = await userRepository.FindByNormalizedUserNameAsync(userName.ToUpperInvariant());
			if (user == null)
			{
				user = new IdentityUser(
					guidGenerator.Create(),
					userName,
					"admin@ddvs.com",
					tenant.Id
				);

				user.Name = "DDVSAdmin";
				user.Surname = "Ahir";

				var result = await userManager.CreateAsync(user, "Temp@123");
				if (!result.Succeeded)
				{
					throw new Exception($"Could not create user: {result.Errors}");
				}

				await userManager.AddDefaultRolesAsync(user);
			}

			#endregion

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

			#region CreateEmployee

			var employeeId = guidGenerator.Create();

			var employee = new Employee(
				employeeId,
				tenant.Id,
				user.Id,
				"DDVS",
				"EMP001",
				"Doe",
				"John",
				"Michael",
				"JM",
				"Active",
				DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
				"Smith",
				CastType.General,
				GenderType.Male,
				"Christianity",
				MaritalStatusType.Married,
				"O+",
				"+91-9876543210",
				"john.doe@ddvs.com",
				"123 Main Street, Mumbai",
				"IT",
				"Software Engineer",
				DateOnly.FromDateTime(new DateTime(2021, 6, 1)),
				null,
				"Group A",
				"B.Tech in Computer Science",
				"Developer",
				"Mole on right cheek",
				"Scar on left hand",
				null,
				new ContactDetails("Robert Smith", "Engineer", "+91-9876543211", "456, Elm Street, Mumbai"),
				new ContactDetails("Mary Smith", "Teacher", "+91-9876543212", "456, Elm Street, Mumbai"),
				new ContactDetails("Jane Doe", "Doctor", "+91-9876543213", "789, Maple Street, Mumbai"),
				new BankDetails("ABC Bank", "Mumbai Main Branch", "John Doe", "123456789012", "ABC123456"),
				new List<WorkExperience>
				{
					new WorkExperience(
						guidGenerator.Create(),
						employeeId,
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
				Convert.FromBase64String(
					"Qk1CAAAAAAAAADYAAAAoAAAAAQAAAAD4AAABABgAAAAAACgAAAEDAAEAAAAAAAAAAADEDgAAxA4AAAAAAAAAAAAA//8A/wAA//8A/wD/")
			);

			await employeeRepository.InsertAsync(employee);

			#endregion
		}
	}
}