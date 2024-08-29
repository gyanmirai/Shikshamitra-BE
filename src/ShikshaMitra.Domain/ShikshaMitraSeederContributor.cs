using System;
using System.Threading.Tasks;
using ShikshaMitra.Authors;
using ShikshaMitra.Books;
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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.TenantManagement;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

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
		IdentityUserManager userManager) : IDataSeedContributor, ITransientDependency
	{
		public async Task SeedAsync(DataSeedContext context)
		{
			if (await tenantRepository.GetCountAsync() > 0)
			{
				return;
			}
			
			// Create a tenant if it doesn't exist
			var tenantName = "DDVS";
			var tenant = await tenantRepository.FindByNameAsync(tenantName);
			if (tenant == null)
			{
				tenant = await tenantManager.CreateAsync(tenantName);
				await tenantRepository.InsertAsync(tenant);
			}

			// Create a user for the tenant
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

			var academicYear = await academicYearRepository.InsertAsync(new AcademicYear(
				guidGenerator.Create(),
				tenant.Id,
				"2021-2022",
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
		}
	}
}