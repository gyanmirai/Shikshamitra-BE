using ShikshaMitra.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.MapperProfile;

public class AcademicSetupPermissionAppService(
	IAsyncQueryableExecuter asyncQueryableExecuter,
	IRepository<AcademicYearPermission, Guid> academicYearPermissionRepository,
	IRepository<EducationBoardPermission, Guid> educationBoardPermissionRepository,
	IRepository<EducationMediumPermission, Guid> educationMediumPermissionRepository,
	IRepository<EducationMediumStagePermission, Guid> educationMediumStagePermissionRepository,
	IRepository<StandardPermission, Guid> standardPermissionRepository,
	IRepository<DivisionPermission, Guid> divisionPermissionRepository,
	AcademicSetupPermissionManager academicSetupPermissionManager)
	: ApplicationService
{
	// create one api that retrivers all employee with permissions
	public async Task<List<AcademicSetupPermissionsDto>> GetAllPermissions()
	{
		//TODO: Fix this API 

		// Fetch all data for each repository asynchronously
		var academicYearQueryable = await academicYearPermissionRepository.GetQueryableAsync();
		var educationBoardQueryable = await educationBoardPermissionRepository.GetQueryableAsync();
		var educationMediumQueryable = await educationMediumPermissionRepository.GetQueryableAsync();
		var educationMediumStageQueryable = await educationMediumStagePermissionRepository.GetQueryableAsync();
		var standardQueryable = await standardPermissionRepository.GetQueryableAsync();
		var divisionQueryable = await divisionPermissionRepository.GetQueryableAsync();

		// Execute queries to get all permissions
		var academicYearPermissions = await asyncQueryableExecuter.ToListAsync(academicYearQueryable);
		var educationBoardPermissions = await asyncQueryableExecuter.ToListAsync(educationBoardQueryable);
		var educationMediumPermissions = await asyncQueryableExecuter.ToListAsync(educationMediumQueryable);
		var educationMediumStagePermissions = await asyncQueryableExecuter.ToListAsync(educationMediumStageQueryable);
		var standardPermissions = await asyncQueryableExecuter.ToListAsync(standardQueryable);
		var divisionPermissions = await asyncQueryableExecuter.ToListAsync(divisionQueryable);

		// Group permissions by UserId
		var groupedPermissions = academicYearPermissions
			.GroupBy(x => x.UserId)
			.Select(group => new AcademicSetupPermissionsDto
			{
				UserId = group.Key.Value,
				AcademicYearIds =
					group.Where(x => x.AcademicYearId != Guid.Empty).Select(x => x.AcademicYearId).ToList(),
				EducationBoardIds =
					educationBoardPermissions.Where(x => x.UserId == group.Key).Select(x => x.EducationBoardId)
						.ToList(),
				EducationMediumIds =
					educationMediumPermissions.Where(x => x.UserId == group.Key).Select(x => x.EducationMediumId)
						.ToList(),
				EducationMediumStageIds =
					educationMediumStagePermissions.Where(x => x.UserId == group.Key)
						.Select(x => x.EducationMediumStageId).ToList(),
				StandardIds = standardPermissions.Where(x => x.UserId == group.Key).Select(x => x.StandardId).ToList(),
				DivisionIds = divisionPermissions.Where(x => x.UserId == group.Key).Select(x => x.DivisionId).ToList(),
			}).ToList();

		return groupedPermissions;
	}


	public async Task<AcademicSetupPermissionsDto> GetPermissionsByUserId(Guid userId)
	{
		// Get IQueryable for each repository
		var academicYearQueryable = await academicYearPermissionRepository.GetQueryableAsync();
		var educationBoardQueryable = await educationBoardPermissionRepository.GetQueryableAsync();
		var educationMediumQueryable = await educationMediumPermissionRepository.GetQueryableAsync();
		var educationMediumStageQueryable = await educationMediumStagePermissionRepository.GetQueryableAsync();
		var standardQueryable = await standardPermissionRepository.GetQueryableAsync();
		var divisionQueryable = await divisionPermissionRepository.GetQueryableAsync();

		// Build the queries
		var academicYearIdsQuery = academicYearQueryable
			.Where(x => x.UserId == userId)
			.Select(x => x.AcademicYearId);

		var educationBoardIdsQuery = educationBoardQueryable
			.Where(x => x.UserId == userId)
			.Select(x => x.EducationBoardId);

		var educationMediumIdsQuery = educationMediumQueryable
			.Where(x => x.UserId == userId)
			.Select(x => x.EducationMediumId);

		var educationMediumStageIdsQuery = educationMediumStageQueryable
			.Where(x => x.UserId == userId)
			.Select(x => x.EducationMediumStageId);

		var standardIdsQuery = standardQueryable
			.Where(x => x.UserId == userId)
			.Select(x => x.StandardId);

		var divisionIdsQuery = divisionQueryable
			.Where(x => x.UserId == userId)
			.Select(x => x.DivisionId);

		// Execute the queries asynchronously, one after the other
		var academicYearIds = await asyncQueryableExecuter.ToListAsync(academicYearIdsQuery);
		var educationBoardIds = await asyncQueryableExecuter.ToListAsync(educationBoardIdsQuery);
		var educationMediumIds = await asyncQueryableExecuter.ToListAsync(educationMediumIdsQuery);
		var educationMediumStageIds = await asyncQueryableExecuter.ToListAsync(educationMediumStageIdsQuery);
		var standardIds = await asyncQueryableExecuter.ToListAsync(standardIdsQuery);
		var divisionIds = await asyncQueryableExecuter.ToListAsync(divisionIdsQuery);

		// Create the DTO with the retrieved IDs
		var dto = new AcademicSetupPermissionsDto
		{
			UserId = userId,
			AcademicYearIds = academicYearIds,
			EducationBoardIds = educationBoardIds,
			EducationMediumIds = educationMediumIds,
			EducationMediumStageIds = educationMediumStageIds,
			StandardIds = standardIds,
			DivisionIds = divisionIds
		};

		return dto;
	}

	/// <summary>
	///  Use for assigning permissions to the user for academic setups.
	/// </summary>
	/// <param name="input">academic setup permissions dto</param>
	/// <returns></returns>
	public async Task AssignPermissions(AcademicSetupPermissionsDto input)
	{
		await academicYearPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateAcademicYearPermissions(input.UserId, input.AcademicYearIds));

		await educationBoardPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateEducationBoardPermissions(input.UserId, input.EducationBoardIds));

		await educationMediumPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateEducationMediumPermissions(input.UserId, input.EducationMediumIds));

		await educationMediumStagePermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateEducationMediumStagePermissions(input.UserId,
				input.EducationMediumStageIds));

		await standardPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateStandardPermissions(input.UserId, input.StandardIds));

		await divisionPermissionRepository.InsertManyAsync(
			academicSetupPermissionManager.CreateDivisionPermissions(input.UserId, input.DivisionIds));
	}
}