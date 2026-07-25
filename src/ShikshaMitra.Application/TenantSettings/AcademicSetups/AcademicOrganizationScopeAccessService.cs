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
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

namespace ShikshaMitra.TenantSettings.EducationBoards;

[RemoteService(false)]
public class AcademicOrganizationScopeAccessService(
	IUserScopeAccessService userScopeAccessService,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<Room, Guid> roomRepository)
	: ApplicationService, ITransientDependency
{
	public async Task ResolveRoomIdsAsync(IEnumerable<DivisionDto> divisions)
	{
		var divisionsList = divisions.ToList();
		var nonGuidDivisions = divisionsList
			.Where(d => !string.IsNullOrEmpty(d.RoomId) && !Guid.TryParse(d.RoomId, out _))
			.ToList();

		if (nonGuidDivisions.Count == 0)
		{
			return;
		}

		var rooms = await roomRepository.GetListAsync();
		var roomsByName = rooms.ToDictionary(r => r.RoomName, r => r.Id, StringComparer.OrdinalIgnoreCase);

		foreach (var division in nonGuidDivisions)
		{
			var searchRoomId = division.RoomId == "101" ? "B101" : division.RoomId;
			if (roomsByName.TryGetValue(searchRoomId, out var roomId))
			{
				division.RoomId = roomId.ToString();
			}
			else
			{
				var matched = rooms.FirstOrDefault(r => r.RoomName.EndsWith(searchRoomId, StringComparison.OrdinalIgnoreCase));
				if (matched != null)
				{
					division.RoomId = matched.Id.ToString();
				}
			}
		}
	}
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

		var hierarchyByStage = new Dictionary<Guid, StandardHierarchyRow>();
		var hasAncestorWildcard =
			scope.WildcardStandardMediumStageIds.Count > 0
			|| scope.WildcardMediumStageMediumIds.Count > 0
			|| scope.WildcardMediumBoardIds.Count > 0
			|| scope.WildcardBoardAcademicYearIds.Count > 0;

		if (hasAncestorWildcard && standards.Count > 0)
		{
			var stageIds = standards
				.Select(standard => standard.EducationMediumStageId)
				.Distinct()
				.ToList();
			var stageQuery = await stageRepository.GetQueryableAsync();
			var mediumQuery = await mediumRepository.GetQueryableAsync();
			var boardQuery = await boardRepository.GetQueryableAsync();
			var hierarchyRows = await AsyncExecuter.ToListAsync(
				from stage in stageQuery
				join medium in mediumQuery on stage.EducationMediumId equals medium.Id
				join board in boardQuery on medium.EducationBoardId equals board.Id
				where stageIds.Contains(stage.Id)
				select new StandardHierarchyRow(
					stage.Id,
					medium.Id,
					board.Id,
					board.AcademicYearId));

			hierarchyByStage = hierarchyRows.ToDictionary(row => row.StageId);
		}

		return standards
			.Where(standard =>
			{
				var hasAncestorFullAccess =
					hierarchyByStage.TryGetValue(standard.EducationMediumStageId, out var hierarchy)
					&& (scope.WildcardStandardMediumStageIds.Contains(hierarchy.StageId)
						|| scope.WildcardMediumStageMediumIds.Contains(hierarchy.MediumId)
						|| scope.WildcardMediumBoardIds.Contains(hierarchy.BoardId)
						|| scope.WildcardBoardAcademicYearIds.Contains(hierarchy.AcademicYearId));
				var standardFullAccess =
					hasAncestorFullAccess
					|| scope.WildcardDivisionStandardIds.Contains(standard.Id);
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

	private sealed record StandardHierarchyRow(
		Guid StageId,
		Guid MediumId,
		Guid BoardId,
		Guid AcademicYearId);

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
