using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Employees;

public class EmployeeOrganizationScopeLookupReader(
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	internal async Task<List<EmployeeOrganizationStageInfo>> LoadStageInfosAsync(Guid? academicYearId)
	{
		var stageQuery = await educationMediumStageRepository.GetQueryableAsync();
		var mediumQuery = await educationMediumRepository.GetQueryableAsync();
		var boardQuery = await educationBoardRepository.GetQueryableAsync();

		var query =
			from stage in stageQuery
			join medium in mediumQuery on stage.EducationMediumId equals medium.Id
			join board in boardQuery on medium.EducationBoardId equals board.Id
			where !academicYearId.HasValue || board.AcademicYearId == academicYearId.Value
			select new EmployeeOrganizationStageInfo
			{
				Id = stage.Id,
				Name = stage.Name,
				MediumId = medium.Id,
				BoardId = board.Id,
				AcademicYearId = board.AcademicYearId
			};

		return await asyncExecuter.ToListAsync(query);
	}

	public async Task<Dictionary<Guid, Guid>> LoadStandardStageLookupAsync(
		IReadOnlyCollection<UserScopeAssignment> assignments)
	{
		var standardIds = assignments
			.Where(x => x.StandardId.HasValue)
			.Select(x => x.StandardId!.Value)
			.Distinct()
			.ToList();
		if (standardIds.Count == 0)
		{
			return [];
		}

		var standardQuery = await standardRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(
			standardQuery
				.Where(x => standardIds.Contains(x.Id))
				.Select(x => new { x.Id, x.EducationMediumStageId }));

		return rows.ToDictionary(x => x.Id, x => x.EducationMediumStageId);
	}

	public async Task<Dictionary<Guid, Guid>> LoadDivisionStageLookupAsync(
		IReadOnlyCollection<UserScopeAssignment> assignments)
	{
		var divisionIds = assignments
			.Where(x => x.DivisionId.HasValue)
			.Select(x => x.DivisionId!.Value)
			.Distinct()
			.ToList();
		if (divisionIds.Count == 0)
		{
			return [];
		}

		var divisionQuery = await divisionRepository.GetQueryableAsync();
		var standardQuery = await standardRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(
			from division in divisionQuery
			join standard in standardQuery on division.StandardId equals standard.Id
			where divisionIds.Contains(division.Id)
			select new
			{
				division.Id,
				standard.EducationMediumStageId
			});

		return rows.ToDictionary(x => x.Id, x => x.EducationMediumStageId);
	}
}
