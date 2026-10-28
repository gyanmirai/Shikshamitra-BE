using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

[RemoteService(false)]
public class AcademicHierarchyReader(
	IRepository<EducationBoard, Guid> boards, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationMediumStage, Guid> stages, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, AcademicHierarchyQueryScope scope,
	AcademicOrganizationScopeAccessService legacyRooms) : ApplicationService
{
	public async Task<List<EducationBoardDto>> ReadAsync(Guid? academicYearId, Guid? boardId = null)
	{
		var query = await scope.BoardsAsync(await boards.GetQueryableAsync());
		if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId);
		if (boardId.HasValue) query = query.Where(x => x.Id == boardId);
		return await ReadBoardsAsync(query.OrderBy(x => x.Type).ThenBy(x => x.Id));
	}
	public async Task<List<EducationBoardDto>> ReadBoardsAsync(IQueryable<EducationBoard> query)
	{
		var result = await AsyncExecuter.ToListAsync(query.Select(AcademicHierarchyProjection.Board));
		if (result.Count == 0) return result;
		var ids = result.Select(x => x.Id).ToArray();
		var propertyQuery = await boards.GetQueryableAsync();
		var properties = await AsyncExecuter.ToListAsync(propertyQuery.Where(x => Enumerable.Contains(ids, x.Id))
			.Select(x => new { x.Id, x.ExtraProperties }));
		var names = properties.ToDictionary(x => x.Id, x =>
			x.ExtraProperties.TryGetValue("CustomName", out var name) ? name?.ToString() : null);
		foreach (var board in result)
			if (names.TryGetValue(board.Id, out var name)) board.CustomName = name;
		var mq = await scope.MediumsAsync(await mediums.GetQueryableAsync());
		var children = await ReadMediumsAsync(mq.Where(x => Enumerable.Contains(ids, x.EducationBoardId)).OrderBy(x => x.CreationTime).ThenBy(x => x.Id));
		var lookup = children.ToLookup(x => x.EducationBoardId);
		foreach (var board in result) board.EducationMediums = lookup[board.Id].ToList();
		return result;
	}
	public async Task<List<EducationMediumDto>> ReadMediumsAsync(IQueryable<EducationMedium> query)
	{
		var result = await AsyncExecuter.ToListAsync(query.Select(AcademicHierarchyProjection.Medium));
		if (result.Count == 0) return result;
		var ids = result.Select(x => x.Id).ToArray();
		var sq = await scope.StagesAsync(await stages.GetQueryableAsync());
		var children = await ReadStagesAsync(sq.Where(x => Enumerable.Contains(ids, x.EducationMediumId))
			.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id));
		var lookup = children.ToLookup(x => x.EducationMediumId);
		foreach (var medium in result) medium.EducationMediumStages = lookup[medium.Id].ToList();
		return result;
	}
	public async Task<List<EducationMediumStageDto>> ReadStagesAsync(IQueryable<EducationMediumStage> query)
	{
		var result = await AsyncExecuter.ToListAsync(query.Select(AcademicHierarchyProjection.Stage));
		if (result.Count == 0) return result;
		var ids = result.Select(x => x.Id).ToArray();
		var cq = await scope.StandardsAsync(await standards.GetQueryableAsync());
		var children = await ReadStandardsAsync(cq.Where(x => Enumerable.Contains(ids, x.EducationMediumStageId))
			.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id));
		var lookup = children.ToLookup(x => x.EducationMediumStageId);
		foreach (var stage in result) stage.Standards = lookup[stage.Id].ToList();
		return result;
	}
	public async Task<List<StandardDto>> ReadStandardsAsync(IQueryable<Standard> query)
	{
		var result = await AsyncExecuter.ToListAsync(query.Select(AcademicHierarchyProjection.Standard));
		if (result.Count == 0) return result;
		var ids = result.Select(x => x.Id).ToArray();
		var dq = await scope.DivisionsAsync(await divisions.GetQueryableAsync());
		var children = await ReadDivisionsAsync(dq.Where(x => Enumerable.Contains(ids, x.StandardId))
			.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id));
		var lookup = children.ToLookup(x => x.StandardId);
		foreach (var standard in result) standard.Divisions = lookup[standard.Id].ToList();
		return result;
	}
	public async Task<List<ShikshaMitra.TenantSettings.AcademicSetups.Divisions.DivisionDto>> ReadDivisionsAsync(IQueryable<Division> query)
	{
		var result = await AsyncExecuter.ToListAsync(query.Select(AcademicHierarchyProjection.Division));
		await legacyRooms.ResolveRoomIdsAsync(result);
		return result;
	}
}
