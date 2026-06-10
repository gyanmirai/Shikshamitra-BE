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

namespace ShikshaMitra.TenantSettings.EducationBoards;

[RemoteService(false)]
public class AcademicOrganizationScopeAccessService(IUserScopeAccessService userScopeAccessService)
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
		return scope.HasTenantWideScope()
			|| scope.MediumIds.Contains(mediumId)
			|| scope.WildcardMediumStageMediumIds.Contains(mediumId);
	}
}
