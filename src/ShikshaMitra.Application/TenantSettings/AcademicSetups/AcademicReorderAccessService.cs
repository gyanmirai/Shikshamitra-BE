using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

[RemoteService(false)]
public class AcademicReorderAccessService(
	IUserScopeAccessService userScopeAccessService,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<Standard, Guid> standardRepository) : ApplicationService, ITransientDependency
{
	public async Task CheckStagesAsync(Guid mediumId, IReadOnlyList<EducationMediumStage> siblings)
	{
		var hierarchy = await GetMediumHierarchyAsync(mediumId);
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.HasTenantWideScope() || scope.WildcardMediumStageMediumIds.Contains(mediumId)
			|| scope.WildcardMediumBoardIds.Contains(hierarchy.BoardId)
			|| scope.WildcardBoardAcademicYearIds.Contains(hierarchy.AcademicYearId)) return;

		var visibleStageIds = scope.MediumStageIds.Union(scope.WildcardStandardMediumStageIds).ToHashSet();
		var siblingIds = siblings.Select(x => x.Id).ToArray();
		var descendantStages = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
			.Where(standard => standard.TenantId == CurrentTenant.Id
				&& Enumerable.Contains(siblingIds, standard.EducationMediumStageId)
				&& (scope.StandardIds.Contains(standard.Id) || scope.WildcardDivisionStandardIds.Contains(standard.Id)
					|| standard.Divisions.Any(division => division.TenantId == CurrentTenant.Id && scope.DivisionIds.Contains(division.Id))))
			.Select(standard => standard.EducationMediumStageId).Distinct());
		visibleStageIds.UnionWith(descendantStages);
		if (siblings.Count == 0 || siblings.Any(stage => !visibleStageIds.Contains(stage.Id)))
		{
			throw new AbpAuthorizationException("You are not authorized to reorder all stages in this medium.");
		}
	}

	public async Task CheckStandardsAsync(Guid stageId, IReadOnlyList<Standard> siblings)
	{
		var hierarchy = await GetHierarchyAsync(stageId);
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (HasStageWideAccess(scope, hierarchy)) return;
		if (siblings.Count == 0 || siblings.Any(x => !scope.StandardIds.Contains(x.Id)
				&& !scope.WildcardDivisionStandardIds.Contains(x.Id)
				&& !(x.Divisions?.Any(d => scope.DivisionIds.Contains(d.Id)) ?? false)))
		{
			throw new AbpAuthorizationException("You are not authorized to reorder all classes in this stage.");
		}
	}

	public async Task CheckDivisionsAsync(Standard standard, IReadOnlyList<Division> siblings)
	{
		var hierarchy = await GetHierarchyAsync(standard.EducationMediumStageId);
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (HasStageWideAccess(scope, hierarchy) || scope.WildcardDivisionStandardIds.Contains(standard.Id)) return;
		if (siblings.Count == 0 || siblings.Any(x => !scope.DivisionIds.Contains(x.Id)))
		{
			throw new AbpAuthorizationException("You are not authorized to reorder all divisions in this class.");
		}
	}

	private static bool HasStageWideAccess(UserScopeContext scope, Hierarchy hierarchy) =>
		scope.HasTenantWideScope() || scope.WildcardStandardMediumStageIds.Contains(hierarchy.StageId)
		|| scope.WildcardMediumStageMediumIds.Contains(hierarchy.MediumId)
		|| scope.WildcardMediumBoardIds.Contains(hierarchy.BoardId)
		|| scope.WildcardBoardAcademicYearIds.Contains(hierarchy.AcademicYearId);

	private async Task<Hierarchy> GetHierarchyAsync(Guid stageId)
	{
		if (!CurrentTenant.Id.HasValue)
		{
			throw new AbpAuthorizationException("Select a school before reordering classes or divisions.");
		}
		var stages = await stageRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			from stage in stages
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where stage.Id == stageId && stage.TenantId == CurrentTenant.Id
				&& medium.TenantId == CurrentTenant.Id && board.TenantId == CurrentTenant.Id
			select new Hierarchy(stage.Id, medium.Id, board.Id, board.AcademicYearId))
			?? throw new AbpAuthorizationException("This academic stage is not available in the current school.");
	}

	private sealed record Hierarchy(Guid StageId, Guid MediumId, Guid BoardId, Guid AcademicYearId);

	private async Task<MediumHierarchy> GetMediumHierarchyAsync(Guid mediumId)
	{
		if (!CurrentTenant.Id.HasValue)
		{
			throw new AbpAuthorizationException("Select a school before reordering stages.");
		}
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			from medium in mediums
			join board in boards on medium.EducationBoardId equals board.Id
			where medium.Id == mediumId && medium.TenantId == CurrentTenant.Id && board.TenantId == CurrentTenant.Id
			select new MediumHierarchy(medium.Id, board.Id, board.AcademicYearId))
			?? throw new AbpAuthorizationException("This academic medium is not available in the current school.");
	}

	private sealed record MediumHierarchy(Guid MediumId, Guid BoardId, Guid AcademicYearId);
}
