using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
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
	   IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	AcademicSetupPermissionManager academicSetupPermissionManager)
	: ApplicationService
{
	// create one api that retrivers all employee with permissions
	//public async Task<List<AcademicSetupPermissionsDto>> GetAllPermissions()
	//{
	//	var academicYearPerms = await academicYearPermissionRepository.GetListAsync();
	//	var educationBoardPerms = await educationBoardPermissionRepository.GetListAsync();
	//	var educationMediumPerms = await educationMediumPermissionRepository.GetListAsync();
	//	var educationMediumStagePerms = await educationMediumStagePermissionRepository.GetListAsync();
	//	var standardPerms = await standardPermissionRepository.GetListAsync();
	//	var divisionPerms = await divisionPermissionRepository.GetListAsync();

	//	// Collect all unique entity IDs
	//	var allAcademicYearIds = academicYearPerms.Select(x => x.AcademicYearId).Distinct().ToList();
	//	var allBoardIds = educationBoardPerms.Select(x => x.EducationBoardId).Distinct().ToList();
	//	var allMediumIds = educationMediumPerms.Select(x => x.EducationMediumId).Distinct().ToList();
	//	var allStageIds = educationMediumStagePerms.Select(x => x.EducationMediumStageId).Distinct().ToList();
	//	var allStandardIds = standardPerms.Select(x => x.StandardId).Distinct().ToList();
	//	var allDivisionIds = divisionPerms.Select(x => x.DivisionId).Distinct().ToList();

	//	// Fetch each entity
	//	var academicYears = await academicYearRepository.GetListAsync(y => allAcademicYearIds.Contains(y.Id));
	//	var boards = await educationBoardRepository.GetListAsync(b => allBoardIds.Contains(b.Id));
	//	var mediums = await educationMediumRepository.GetListAsync(m => allMediumIds.Contains(m.Id));
	//	var stages = await educationMediumStageRepository.GetListAsync(s => allStageIds.Contains(s.Id));
	//	var standards = await standardRepository.GetListAsync(s => allStandardIds.Contains(s.Id));
	//	var divisions = await divisionRepository.GetListAsync(d => allDivisionIds.Contains(d.Id));

	//	var academicYearLookup = academicYears.ToDictionary(x => x.Id, x => ObjectMapper.Map<AcademicYear, AcademicYearDto>(x));
	//	var boardLookup = boards.ToDictionary(x => x.Id, x => ObjectMapper.Map<EducationBoard, EducationBoardDto>(x));
	//	var mediumLookup = mediums.ToDictionary(x => x.Id, x => ObjectMapper.Map<EducationMedium, EducationMediumDto>(x));
	//	var stageLookup = stages.ToDictionary(x => x.Id, x => ObjectMapper.Map<EducationMediumStage, EducationMediumStageDto>(x));
	//	var standardLookup = standards.ToDictionary(x => x.Id, x => ObjectMapper.Map<Standard, StandardDto>(x));
	//	var divisionLookup = divisions.ToDictionary(x => x.Id, x => ObjectMapper.Map<Division, DivisionDto>(x));


	//	var groupedPermissions =
	//	  (from ayPerm in academicYearPerms
	//	   group ayPerm by ayPerm.UserId
	//	   into g
	//	   select new AcademicSetupPermissionsDto
	//	   {
	//		   UserId = g.Key.Value,

	//		   // Map each ID to the Dto, if present in the lookup
	//		   AcademicYears = g
	//			   .Select(x => x.AcademicYearId)
	//			   .Distinct()
	//			   .Where(id => academicYearLookup.ContainsKey(id))
	//			   .Select(id => academicYearLookup[id])
	//			   .ToList(),

	//		   EducationBoards = educationBoardPerms
	//			   .Where(x => x.UserId == g.Key)
	//			   .Select(x => x.EducationBoardId)
	//			   .Distinct()
	//			   .Where(id => boardLookup.ContainsKey(id))
	//			   .Select(id => boardLookup[id])
	//			   .ToList(),

	//		   EducationMediums = educationMediumPerms
	//				.Where(x => x.UserId == g.Key)
	//				.Select(x => x.EducationMediumId)
	//				.Distinct()
	//				.Where(id => mediumLookup.ContainsKey(id))
	//				.Select(id => mediumLookup[id])
	//				.ToList(),


	//		   EducationMediumStages = educationMediumStagePerms
	//				.Where(x => x.UserId == g.Key)
	//				.Select(x => x.EducationMediumStageId)
	//				.Distinct()
	//				.Where(id => stageLookup.ContainsKey(id))
	//				.Select(id => stageLookup[id])
	//				.ToList(),


	//		   Standards = standardPerms
	//				.Where(x => x.UserId == g.Key)
	//				.Select(x => x.StandardId)
	//				.Distinct()
	//				.Where(id => standardLookup.ContainsKey(id))
	//				.Select(id => standardLookup[id])
	//				.ToList(),

	//		   Divisions = divisionPerms
	//				.Where(x => x.UserId == g.Key)
	//				.Select(x => x.DivisionId)
	//				.Distinct()
	//				.Where(id => divisionLookup.ContainsKey(id))
	//				.Select(id => divisionLookup[id])
	//				.ToList()
	//	   }).ToList();

	//	return groupedPermissions;
	//}

	public async Task<AcademicSetupPermissionsDto> GetPermissionsByUserId(Guid userId)
	{
		// Fetch permissions for the specified user
		var academicYearPerms = await academicYearPermissionRepository.GetListAsync(x => x.UserId == userId);
		var educationBoardPerms = await educationBoardPermissionRepository.GetListAsync(x => x.UserId == userId);
		var educationMediumPerms = await educationMediumPermissionRepository.GetListAsync(x => x.UserId == userId);
		var educationMediumStagePerms = await educationMediumStagePermissionRepository.GetListAsync(x => x.UserId == userId);
		var standardPerms = await standardPermissionRepository.GetListAsync(x => x.UserId == userId);
		var divisionPerms = await divisionPermissionRepository.GetListAsync(x => x.UserId == userId);

		// Collect all unique entity IDs
		var allAcademicYearIds = academicYearPerms.Select(x => x.AcademicYearId).Distinct().ToList();
		var allBoardIds = educationBoardPerms.Select(x => x.EducationBoardId).Distinct().ToList();
		var allMediumIds = educationMediumPerms.Select(x => x.EducationMediumId).Distinct().ToList();
		var allStageIds = educationMediumStagePerms.Select(x => x.EducationMediumStageId).Distinct().ToList();
		var allStandardIds = standardPerms.Select(x => x.StandardId).Distinct().ToList();
		var allDivisionIds = divisionPerms.Select(x => x.DivisionId).Distinct().ToList();

		// Fetch each entity
		var academicYears = await academicYearRepository.GetListAsync(y => allAcademicYearIds.Contains(y.Id));
		var boards = await educationBoardRepository.GetListAsync(b => allBoardIds.Contains(b.Id));
		var mediums = await educationMediumRepository.GetListAsync(m => allMediumIds.Contains(m.Id));
		var stages = await educationMediumStageRepository.GetListAsync(s => allStageIds.Contains(s.Id));
		var standards = await standardRepository.GetListAsync(s => allStandardIds.Contains(s.Id));
		var divisions = await divisionRepository.GetListAsync(d => allDivisionIds.Contains(d.Id));

		// Convert to lookups
		var academicYearLookup = academicYears
			.ToDictionary(x => x.Id, x => ObjectMapper.Map<AcademicYear, AcademicYearDto>(x));
		var boardLookup = boards
			.ToDictionary(x => x.Id, x => ObjectMapper.Map<EducationBoard, EducationBoardDto>(x));
		var mediumLookup = mediums
			.ToDictionary(x => x.Id, x => ObjectMapper.Map<EducationMedium, EducationMediumDto>(x));
		var stageLookup = stages
			.ToDictionary(x => x.Id, x => ObjectMapper.Map<EducationMediumStage, EducationMediumStageDto>(x));
		var standardLookup = standards
			.ToDictionary(x => x.Id, x => ObjectMapper.Map<Standard, StandardDto>(x));
		var divisionLookup = divisions
			.ToDictionary(x => x.Id, x => ObjectMapper.Map<Division, DivisionDto>(x));

		// Build the DTO for this user
		var dto = new AcademicSetupPermissionsDto
		{
			UserId = userId,

			AcademicYears = academicYearPerms
				.Select(x => x.AcademicYearId)
				.Distinct()
				.Where(id => academicYearLookup.ContainsKey(id))
				.Select(id => academicYearLookup[id])
				.ToList(),

			EducationBoards = educationBoardPerms
				.Select(x => x.EducationBoardId)
				.Distinct()
				.Where(id => boardLookup.ContainsKey(id))
				.Select(id => boardLookup[id])
				.ToList(),

			EducationMediums = educationMediumPerms
				.Select(x => x.EducationMediumId)
				.Distinct()
				.Where(id => mediumLookup.ContainsKey(id))
				.Select(id => mediumLookup[id])
				.ToList(),

			EducationMediumStages = educationMediumStagePerms
				.Select(x => x.EducationMediumStageId)
				.Distinct()
				.Where(id => stageLookup.ContainsKey(id))
				.Select(id => stageLookup[id])
				.ToList(),

			Standards = standardPerms
				.Select(x => x.StandardId)
				.Distinct()
				.Where(id => standardLookup.ContainsKey(id))
				.Select(id => standardLookup[id])
				.ToList(),

			Divisions = divisionPerms
				.Select(x => x.DivisionId)
				.Distinct()
				.Where(id => divisionLookup.ContainsKey(id))
				.Select(id => divisionLookup[id])
				.ToList()
		};

		return dto;
	}


	public async Task AssignPermissions(CreateUpdateAcademicSetupPermissionsDto input)
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