using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

internal static class EmployeeOrganizationTestHelper
{
	public static async Task<Guid> EnsureOrganizationExistsAsync(IServiceProvider serviceProvider)
	{
		var stageRepository = serviceProvider.GetRequiredService<IEducationMediumStageRepository>();
		var existingStages = await stageRepository.GetListAsync();
		var existingStage = existingStages.FirstOrDefault();
		if (existingStage != null)
		{
			return existingStage.Id;
		}

		var academicYearRepository = serviceProvider.GetRequiredService<IAcademicYearRepository>();
		var educationBoardRepository = serviceProvider.GetRequiredService<IEducationBoardRepository>();
		var educationMediumRepository = serviceProvider.GetRequiredService<IEducationMediumRepository>();
		var academicYearManager = serviceProvider.GetRequiredService<AcademicYearManager>();
		var educationBoardManager = serviceProvider.GetRequiredService<EducationBoardManager>();
		var educationMediumManager = serviceProvider.GetRequiredService<EducationMediumManager>();
		var educationMediumStageManager = serviceProvider.GetRequiredService<EducationMediumStageManager>();
		var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
		var tenantId = currentTenant.Id ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

		using (currentTenant.Change(tenantId))
		{
			var academicYear = await academicYearManager.CreateAsync(
				$"TY-{Guid.NewGuid():N}"[..35],
				new DateTime(2026, 4, 1),
				new DateTime(2027, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: true);

			var board = await educationBoardManager.CreateAsync(academicYear.Id, EducationBoardType.CBSE);
			await educationBoardRepository.InsertAsync(board, autoSave: true);

			var medium = await educationMediumManager.CreateAsync(board.Id, "English");
			await educationMediumRepository.InsertAsync(medium, autoSave: true);

			var stage = await educationMediumStageManager.CreateAsync(medium.Id, "Primary");
			await stageRepository.InsertAsync(stage, autoSave: true);

			return stage.Id;
		}
	}
}
