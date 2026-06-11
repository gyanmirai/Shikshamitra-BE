using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.EducationBoards;

[RemoteService(false)]
public class AcademicOrganizationScopeAccessService(
	IUserScopeAccessService userScopeAccessService,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> stageRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<EducationBoardDto>> PruneBoardTreeAsync(
		IReadOnlyList<EducationBoardDto> boards)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		return EducationBoardScopeFilter.PruneTree(boards, scope);
	}

	public async Task<IReadOnlyList<EducationMediumDto>> FilterMediumsAsync(
		IReadOnlyList<EducationMediumDto> mediums)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return mediums;
		}

		return mediums
			.Where(m =>
				scope.WildcardMediumBoardIds.Contains(m.EducationBoardId)
				|| scope.MediumIds.Contains(m.Id)
				|| scope.WildcardMediumStageMediumIds.Contains(m.Id))
			.ToList();
	}

	public async Task<IReadOnlyList<EducationMediumStageDto>> FilterStagesAsync(
		IReadOnlyList<EducationMediumStageDto> stages)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return stages;
		}

		return stages
			.Where(stage =>
				scope.MediumStageIds.Contains(stage.Id)
				|| scope.WildcardStandardMediumStageIds.Contains(stage.Id)
				|| scope.WildcardMediumStageMediumIds.Contains(stage.EducationMediumId))
			.ToList();
	}

	public async Task<IReadOnlyList<DivisionDto>> FilterDivisionsAsync(
		IReadOnlyList<DivisionDto> divisions)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return divisions;
		}

		return divisions
			.Where(d =>
				scope.WildcardDivisionStandardIds.Contains(d.StandardId)
				|| scope.DivisionIds.Contains(d.Id))
			.ToList();
	}

	public async Task<List<Standard>> FilterStandardsAsync(List<Standard> standards)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return standards;
		}

		return standards
			.Where(standard =>
			{
				var standardFullAccess = scope.WildcardDivisionStandardIds.Contains(standard.Id);
				var standardVisible =
					standardFullAccess ||
					scope.StandardIds.Contains(standard.Id) ||
					(standard.Divisions?.Any(d => scope.DivisionIds.Contains(d.Id)) ?? false);

				if (!standardVisible)
				{
					return false;
				}

				if (!standardFullAccess)
				{
					standard.Divisions = standard.Divisions?
						.Where(d => scope.DivisionIds.Contains(d.Id))
						.ToList() ?? [];
				}

				return true;
			})
			.ToList();
	}

	public async Task<bool> CanAccessMediumStageAsync(
		Guid stageId,
		Guid mediumId,
		Guid boardId,
		Guid academicYearId)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		return scope.CanAccessMediumStage(stageId, mediumId, boardId, academicYearId);
	}

	public async Task<bool> CanAccessMediumAsync(Guid mediumId)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();

		// Fast path: tenant-wide, directly assigned medium, or stage-wildcard in this medium
		if (scope.HasTenantWideScope()
			|| scope.MediumIds.Contains(mediumId)
			|| scope.WildcardMediumStageMediumIds.Contains(mediumId))
		{
			return true;
		}

		// DB lookup: check if any of the user's assigned standards belong to this medium
		// (covers scope assignments stored with only AcademicYearId + StandardId, without EducationMediumId)
		var standardIds = scope.StandardIds
			.Union(scope.WildcardDivisionStandardIds)
			.Distinct()
			.ToList();

		if (standardIds.Count == 0)
			return false;

		var standardQuery = await standardRepository.GetQueryableAsync();
		var stageQuery = await stageRepository.GetQueryableAsync();

		return await AsyncExecuter.AnyAsync(
			from s in standardQuery
			where standardIds.Contains(s.Id)
			join st in stageQuery on s.EducationMediumStageId equals st.Id
			where st.EducationMediumId == mediumId
			select s
		);
	}
}
