using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.Reports;

[RemoteService(false)]
public partial class InstituteReportData(
	IRepository<AcademicYear, Guid> years, IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationMediumStage, Guid> stages,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<Room, Guid> rooms, IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
	IRepository<RoomType, Guid> roomTypes, IRepository<ShiftManagement, Guid> shifts,
	IUserScopeService scopes, InstituteReportAccess access, InstituteDocumentReportData documents, InstituteIdentityReportData identities) : ApplicationService
{
	internal async Task<IQueryable<InstituteHierarchyRow>> HierarchyQueryAsync(Guid userId, InstituteReportFilters filter)
	{
		var tenant = CurrentTenant.Id ?? throw new BusinessException("InstituteReports:TenantRequired");
		var yq = (await years.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var bq = (await boards.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var mq = (await mediums.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var sq = (await stages.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var cq = (await standards.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var dq = (await divisions.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var query = from y in yq
			join board in bq on y.Id equals board.AcademicYearId into yearBoards from b in yearBoards.DefaultIfEmpty()
			join medium in mq on b.Id equals medium.EducationBoardId into mb from m in mb.DefaultIfEmpty()
			join stage in sq on m.Id equals stage.EducationMediumId into sm from s in sm.DefaultIfEmpty()
			join standard in cq on s.Id equals standard.EducationMediumStageId into cs from c in cs.DefaultIfEmpty()
			join division in dq on c.Id equals division.StandardId into dc from d in dc.DefaultIfEmpty()
			select new InstituteHierarchyRow {
				AcademicYearId = y.Id, EducationBoardId = b == null ? null : b.Id,
				EducationMediumId = m == null ? null : m.Id, EducationMediumStageId = s == null ? null : s.Id,
				StandardId = c == null ? null : c.Id, DivisionId = d == null ? null : d.Id,
				Year = y.Name, Board = b == null ? "" : b.Type == EducationBoardType.CBSE ? "CBSE" : "GSEB", Medium = m == null ? "" : m.Name,
				Stage = s == null ? "" : s.Name, Standard = c == null ? "" : c.Name, Division = d == null ? "" : d.Name,
				Capacity = d == null ? null : d.Strength, RoomReference = d == null ? null : d.RoomId.HasValue ? d.RoomId.Value.ToString() : d.RoomNumber,
				ShiftId = c == null ? null : c.ShiftId
			};
		query = query.ApplyUserScope(await scopes.GetUserScopeAsync(userId));
		if (filter.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == filter.AcademicYearId);
		if (filter.BoardId.HasValue) query = query.Where(x => x.EducationBoardId == filter.BoardId);
		if (filter.MediumId.HasValue) query = query.Where(x => x.EducationMediumId == filter.MediumId);
		if (filter.StageId.HasValue) query = query.Where(x => x.EducationMediumStageId == filter.StageId);
		if (filter.StandardId.HasValue) query = query.Where(x => x.StandardId == filter.StandardId);
		if (filter.DivisionId.HasValue) query = query.Where(x => x.DivisionId == filter.DivisionId);
		if (filter.ShiftId.HasValue) query = query.Where(x => x.ShiftId == filter.ShiftId);
		return query;
	}
	public async Task<List<InstituteHierarchyRow>> HierarchyAsync(Guid userId, InstituteReportFilters filter)
	{
		var rows = await AsyncExecuter.ToListAsync((await HierarchyQueryAsync(userId, filter)).OrderBy(x => x.Year).ThenBy(x => x.Medium).ThenBy(x => x.Standard).ThenBy(x => x.Division));
		await HydrateBoardNamesAsync(rows);
		return rows;
	}
	public async Task<AcademicReportPreviewDto> GenerateAsync(InstituteReportRequest request, Guid userId, bool all = false)
	{
		InstituteReportCatalog.Validate(request);
		await access.EnsureAsync(userId, request.ReportType);
		var definition = InstituteReportCatalog.Get(request.ReportType);
		var selected = await ValidateFiltersAsync(request, userId);
		var result = new AcademicReportPreviewDto { ReportType = definition.Label, GeneratedAtUtc = DateTime.UtcNow,
			Columns = request.Columns.Count == 0 ? definition.Columns : request.Columns.Select(id => definition.Columns.Single(x => x.Id == id)).ToList(),
			FilterSummary = (definition.Academic ? "Permitted academic hierarchy" : "School-wide catalogue") + (selected.Count > 0 ? ": " + string.Join(" / ", selected) : ""), Page = request.Page, PageSize = request.PageSize };
		if (request.ReportType == "class-division-summary") result.FilterSummary += ". Division counts include only permitted divisions.";
		if (InstituteReportCatalog.IsDocuments(request.ReportType)) result.FilterSummary += ". Active library metadata only; attachment contents are excluded. Counts distinguish documents from attachments.";
		if (InstituteReportCatalog.IsIdentity(request.ReportType)) result.FilterSummary = "Permitted configured identity profiles, including readable school defaults. Selections describe saved configuration, not effective inherited assets; images are excluded."
			+ (selected.Count > 0 ? " Filters: " + string.Join(" / ", selected) : "");
		if (request.Filters.DocumentCategory != null) result.FilterSummary += " Category: " + request.Filters.DocumentCategory;
		if (request.Filters.IdentityScope != null) result.FilterSummary += " Scope: " + request.Filters.IdentityScope;
		if (request.ReportType == "shift-register" && (result.Columns.Exists(x => x.Id == "totalHours") || request.Conditions.Exists(x => x.Field == "totalHours") || request.SortField == "totalHours"))
			result.FilterSummary += ". Total hours is elapsed time rounded to two decimal places, without break deductions. An earlier end time means the next day; equal times mean zero hours.";
		var page = await ReadPageAsync(request, userId, all);
		if (page != null) { result.Rows = page.Value.Rows; result.TotalCount = page.Value.Count; }
		else
		{
			var rows = InstituteReportProjection.Apply(await ReadRowsAsync(request, userId), request).ToList();
			result.TotalCount = rows.Count;
			result.Rows = all ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
		}
		result.NormalizedFilters.AcademicYearId = request.Filters.AcademicYearId;
		return result;
	}
	private async Task<List<Dictionary<string, string?>>> ReadRowsAsync(InstituteReportRequest request, Guid userId)
	{
		var tenant = CurrentTenant.Id ?? throw new BusinessException("InstituteReports:TenantRequired");
		var filter = request.Filters;
		if (InstituteReportCatalog.IsDocuments(request.ReportType)) return await documents.ReadAsync(request, userId);
		if (InstituteReportCatalog.IsIdentity(request.ReportType)) return await identities.ReadAsync(request, userId);
		if (request.ReportType == "shift-register")
			return (await shifts.GetListAsync(x => x.TenantId == tenant && (!filter.ShiftId.HasValue || x.Id == filter.ShiftId))).OrderBy(x => x.ShiftName)
				.Select(x => Row(("shift", x.ShiftName), ("start", x.StartTime.ToString("HH:mm")), ("end", x.EndTime.ToString("HH:mm")),
					("totalHours", InstituteReportCalculations.ShiftTotalHours(x.StartTime, x.EndTime)))).ToList();
		var hierarchy = request.ReportType is "academic-hierarchy" or "division-capacity" or "class-division-summary" ? await HierarchyAsync(userId, filter) : [];
		if (request.ReportType == "academic-hierarchy") return hierarchy.Select(HierarchyValues).ToList();
		if (request.ReportType == "class-division-summary") return InstituteClassDivisionSummary.Create(hierarchy);
		var roomList = await AsyncExecuter.ToListAsync((await rooms.GetQueryableAsync()).Where(x => x.TenantId == tenant)
			.Select(x => new { x.Id, x.RoomName, x.BuildingId, x.FloorId, x.RoomTypeId, x.Capacity, x.TotalBenches, x.BenchCapacity, x.Features }));
		var roomLookup = roomList.ToDictionary(x => x.Id);
		if (request.ReportType == "division-capacity")
		{
			var shiftList = await AsyncExecuter.ToListAsync((await shifts.GetQueryableAsync()).Where(x => x.TenantId == tenant).Select(x => new { x.Id, x.ShiftName }));
			var shiftLookup = shiftList.ToDictionary(x => x.Id, x => x.ShiftName);
			return hierarchy.Where(x => x.DivisionId.HasValue).Select(x => {
				var row = HierarchyValues(x); var room = Guid.TryParse(x.RoomReference, out var id) ? roomLookup.GetValueOrDefault(id) : null;
				row["capacity"] = x.Capacity?.ToString(CultureInfo.InvariantCulture);
				row["room"] = room?.RoomName ?? x.RoomReference;
				row["roomAssignment"] = string.IsNullOrWhiteSpace(x.RoomReference) ? "unassigned" : room == null ? "unresolved" : "assigned";
				row["shift"] = x.ShiftId.HasValue ? shiftLookup.GetValueOrDefault(x.ShiftId.Value) : null;
				return row;
			}).Where(x => filter.RoomAssignment == null || x["roomAssignment"] == filter.RoomAssignment).ToList();
		}
		var buildingList = await AsyncExecuter.ToListAsync((await buildings.GetQueryableAsync()).Where(x => x.TenantId == tenant).Select(x => new { x.Id, x.BuildingName }));
		var buildingIds = buildingList.Select(x => x.Id).ToArray();
		var floorList = await AsyncExecuter.ToListAsync((await floors.GetQueryableAsync()).Where(x => Enumerable.Contains(buildingIds, x.BuildingId)).Select(x => new { x.Id, x.BuildingId, x.FloorName }));
		var typeList = await AsyncExecuter.ToListAsync((await roomTypes.GetQueryableAsync()).Where(x => x.TenantId == tenant).Select(x => new { x.Id, x.RoomTypeName }));
		return roomList.Where(x => (!filter.BuildingId.HasValue || x.BuildingId == filter.BuildingId) && (!filter.FloorId.HasValue || x.FloorId == filter.FloorId) && (!filter.RoomTypeId.HasValue || x.RoomTypeId == filter.RoomTypeId))
			.OrderBy(x => x.RoomName).Select(x => Row(("building", buildingList.Find(b => b.Id == x.BuildingId)?.BuildingName),
				("floor", floorList.Find(f => f.Id == x.FloorId && f.BuildingId == x.BuildingId)?.FloorName), ("room", x.RoomName),
				("roomType", typeList.Find(t => t.Id == x.RoomTypeId)?.RoomTypeName), ("capacity", x.Capacity.ToString(CultureInfo.InvariantCulture)),
				("benches", x.TotalBenches.ToString(CultureInfo.InvariantCulture)), ("benchCapacity", x.BenchCapacity.ToString(CultureInfo.InvariantCulture)), ("features", string.Join(", ", x.Features ?? [])))).ToList();
	}
	private static Dictionary<string, string?> HierarchyValues(InstituteHierarchyRow x) => Row(("year", x.Year), ("board", x.Board), ("medium", x.Medium), ("stage", x.Stage), ("standard", x.Standard), ("division", x.Division));
	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) => values.ToDictionary(x => x.Key, x => x.Value);
	public async Task<InstituteReportOptions> OptionsAsync(Guid userId)
	{
		await access.EnsureAsync(userId);
		var result = new InstituteReportOptions();
		foreach (var item in InstituteReportCatalog.All)
			if (await access.CanReadAsync(userId, item.Id)) result.ReportTypes.Add(item);
		if (result.ReportTypes.Any(x => InstituteReportCatalog.IsDocuments(x.Id))) result.Values["documentCategory"] = InstituteDocumentReportData.Categories;
		if (result.ReportTypes.Any(x => InstituteReportCatalog.IsIdentity(x.Id)))
		{
			result.Values["identityScope"] = InstituteIdentityReportData.ScopeValues;
			foreach (var (key, choices) in await identities.ChoicesAsync(userId)) result.Choices[key] = choices;
		}
		if (result.ReportTypes.Any(x => x.Academic))
		{
			var rows = await HierarchyAsync(userId, new());
			Merge("academicYearId", Options(rows, x => x.AcademicYearId, x => x.Year));
			Merge("boardId", Options(rows, x => x.EducationBoardId, x => x.Board, x => x.AcademicYearId));
			Merge("mediumId", Options(rows, x => x.EducationMediumId, x => x.Medium, x => x.EducationBoardId));
			Merge("stageId", Options(rows, x => x.EducationMediumStageId, x => x.Stage, x => x.EducationMediumId));
			result.Choices["standardId"] = Options(rows, x => x.StandardId, x => x.Standard, x => x.EducationMediumStageId);
			result.Choices["divisionId"] = Options(rows, x => x.DivisionId, x => x.Division, x => x.StandardId);
		}
		if (result.ReportTypes.Any(x => x.Id == "rooms-inventory"))
		{
			var list = await buildings.GetListAsync(x => x.TenantId == CurrentTenant.Id);
			var ids = list.Select(x => x.Id).ToArray();
			result.Choices["buildingId"] = list.Select(x => new InstituteReportOption(x.Id, x.BuildingName)).ToList();
			result.Choices["floorId"] = (await floors.GetListAsync(x => Enumerable.Contains(ids, x.BuildingId))).Select(x => new InstituteReportOption(x.Id, x.FloorName, x.BuildingId)).ToList();
			result.Choices["roomTypeId"] = (await roomTypes.GetListAsync(x => x.TenantId == CurrentTenant.Id)).Select(x => new InstituteReportOption(x.Id, x.RoomTypeName)).ToList();
		}
		if (result.ReportTypes.Any(x => x.Id is "shift-register" or "division-capacity"))
			result.Choices["shiftId"] = (await shifts.GetListAsync(x => x.TenantId == CurrentTenant.Id)).Select(x => new InstituteReportOption(x.Id, x.ShiftName)).ToList();
		return result;
		void Merge(string key, List<InstituteReportOption> choices) => result.Choices[key] = (result.Choices.GetValueOrDefault(key) ?? []).Concat(choices).DistinctBy(x => x.Id).ToList();
	}
	private static List<InstituteReportOption> Options(List<InstituteHierarchyRow> rows, Func<InstituteHierarchyRow, Guid?> id, Func<InstituteHierarchyRow, string> name, Func<InstituteHierarchyRow, Guid?>? parent = null) =>
		rows.Where(x => id(x).HasValue).Select(x => new InstituteReportOption(id(x)!.Value, name(x), parent?.Invoke(x))).DistinctBy(x => x.Id).ToList();
}
