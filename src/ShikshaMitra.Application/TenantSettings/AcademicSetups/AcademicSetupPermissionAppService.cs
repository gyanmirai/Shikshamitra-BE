using ShikshaMitra.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.MapperProfile;

public class AcademicSetupPermissionAppService(
	IRepository<AcademicYearPermission, Guid> academicYearPermissionRepository,
	IRepository<EducationBoardPermission, Guid> educationBoardPermissionRepository,
	IRepository<EducationMediumPermission, Guid> educationMediumPermissionRepository,
	IRepository<EducationMediumStagePermission, Guid> educationMediumStagePermissionRepository,
	IRepository<StandardPermission, Guid> standardPermissionRepository,
	IRepository<DivisionPermission, Guid> divisionPermissionRepository,
	AcademicSetupPermissionManager academicSetupPermissionManager)
	: ApplicationService
{
	public async Task AssignPermissionsAsync(AcademicSetupPermissionsDto input)
	{
		await academicYearPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateAcademicYearPermissions(input.AcademicYearIds));

		await educationBoardPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateEducationBoardPermissions(input.EducationBoardIds));

		await educationMediumPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateEducationMediumPermissions(input.EducationMediumIds));

		await educationMediumStagePermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateEducationMediumStagePermissions(input.EducationMediumStageIds));

		await standardPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateStandardPermissions(input.StandardIds));

		await divisionPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateDivisionPermissions(input.DivisionIds));
	}
}