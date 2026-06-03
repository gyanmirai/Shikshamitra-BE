using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR;

public class HrMediumStageScopeReader(
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<HrMediumStageScope>> GetAllAsync()
	{
		var stageQuery = await stageRepository.GetQueryableAsync();
		var mediumQuery = await mediumRepository.GetQueryableAsync();
		var boardQuery = await boardRepository.GetQueryableAsync();

		return await asyncExecuter.ToListAsync(
			from stage in stageQuery
			join medium in mediumQuery on stage.EducationMediumId equals medium.Id
			join board in boardQuery on medium.EducationBoardId equals board.Id
			select new HrMediumStageScope(
				stage.Id,
				medium.Id,
				board.Id,
				board.AcademicYearId));
	}

	public async Task<Dictionary<Guid, HrMediumStageScope>> GetByStageIdsAsync(IReadOnlyCollection<Guid> stageIds)
	{
		if (stageIds.Count == 0)
		{
			return [];
		}

		var stages = await asyncExecuter.ToListAsync(
			(await stageRepository.GetQueryableAsync()).Where(x => stageIds.Contains(x.Id)));
		var mediumIds = stages.Select(x => x.EducationMediumId).Distinct().ToList();
		var mediums = await asyncExecuter.ToListAsync(
			(await mediumRepository.GetQueryableAsync()).Where(x => mediumIds.Contains(x.Id)));
		var mediumById = mediums.ToDictionary(x => x.Id);
		var boardIds = mediums.Select(x => x.EducationBoardId).Distinct().ToList();
		var boards = await asyncExecuter.ToListAsync(
			(await boardRepository.GetQueryableAsync()).Where(x => boardIds.Contains(x.Id)));
		var boardById = boards.ToDictionary(x => x.Id);

		var result = new Dictionary<Guid, HrMediumStageScope>();
		foreach (var stage in stages)
		{
			if (!mediumById.TryGetValue(stage.EducationMediumId, out var medium) ||
			    !boardById.TryGetValue(medium.EducationBoardId, out var board))
			{
				continue;
			}

			result[stage.Id] = new HrMediumStageScope(stage.Id, medium.Id, board.Id, board.AcademicYearId);
		}

		return result;
	}
}

public sealed record HrMediumStageScope(Guid StageId, Guid MediumId, Guid BoardId, Guid AcademicYearId);
