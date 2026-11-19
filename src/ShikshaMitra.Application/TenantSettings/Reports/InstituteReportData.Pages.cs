using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

namespace ShikshaMitra.TenantSettings.Reports;

public partial class InstituteReportData
{
	private async Task<(List<Dictionary<string, string?>> Rows, int Count)?> ReadPageAsync(InstituteReportRequest request, Guid user, bool all)
	{
		if (InstituteReportCatalog.IsDocuments(request.ReportType)) return await documents.PageAsync(request, user, all);
		if (InstituteReportCatalog.IsIdentity(request.ReportType)) return await identities.PageAsync(request, user, all);
		if (request.ReportType is "academic-hierarchy" or "division-capacity" or "class-division-summary") return await HierarchyPageAsync(request, user, all);
		if (request.ReportType == "rooms-inventory") return await RoomsPageAsync(request, all);
		var filter = request.Filters;
		var shiftsQuery = (await shifts.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && (!filter.ShiftId.HasValue || x.Id == filter.ShiftId));
		var page = await InstituteReportQuery.PageAsync(shiftsQuery, request, all, AsyncExecuter, q => q.OrderBy(x => x.ShiftName).ThenBy(x => x.Id),
			new() { ["shift"] = x => x.ShiftName });
		return page == null ? null : (page.Value.Items.Select(x => Row(("shift", x.ShiftName), ("start", x.StartTime.ToString("HH:mm")), ("end", x.EndTime.ToString("HH:mm")),
			("totalHours", InstituteReportCalculations.ShiftTotalHours(x.StartTime, x.EndTime)))).ToList(), page.Value.Count);
	}
	private async Task<(List<Dictionary<string, string?>> Rows, int Count)?> HierarchyPageAsync(InstituteReportRequest request, Guid user, bool all)
	{
		if (request.ReportType == "class-division-summary") return await SummaryPageAsync(request, user, all);
		if (request.Filters.RoomAssignment != null) return null;
		var query = await HierarchyQueryAsync(user, request.Filters);
		if (request.ReportType == "division-capacity") query = query.Where(x => x.DivisionId.HasValue);
		var fields = HierarchyText();
		var page = await InstituteReportQuery.PageAsync(query, request, all, AsyncExecuter,
			q => q.OrderBy(x => x.Year).ThenBy(x => x.Medium).ThenBy(x => x.Standard).ThenBy(x => x.Division).ThenBy(x => x.DivisionId),
			fields, new() { ["capacity"] = x => x.Capacity });
		if (page == null) return null;
		await HydrateBoardNamesAsync(page.Value.Items);
		if (request.ReportType == "academic-hierarchy") return (page.Value.Items.Select(HierarchyValues).ToList(), page.Value.Count);
		var roomIds = page.Value.Items.Select(x => Guid.TryParse(x.RoomReference, out var id) ? id : Guid.Empty).Distinct().ToArray();
		var roomNames = await AsyncExecuter.ToListAsync((await rooms.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(roomIds, x.Id)).Select(x => new { x.Id, x.RoomName }));
		var shiftIds = page.Value.Items.Select(x => x.ShiftId).Distinct().ToArray();
		var shiftNames = await AsyncExecuter.ToListAsync((await shifts.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(shiftIds, x.Id)).Select(x => new { x.Id, x.ShiftName }));
		var roomLookup = roomNames.ToDictionary(x => x.Id, x => x.RoomName);
		var shiftLookup = shiftNames.ToDictionary(x => x.Id, x => x.ShiftName);
		return (page.Value.Items.Select(x => {
			var row = HierarchyValues(x);
			var room = Guid.TryParse(x.RoomReference, out var id) ? roomLookup.GetValueOrDefault(id) : null;
			row["capacity"] = x.Capacity?.ToString(CultureInfo.InvariantCulture); row["room"] = room ?? x.RoomReference;
			row["roomAssignment"] = string.IsNullOrWhiteSpace(x.RoomReference) ? "unassigned" : room == null ? "unresolved" : "assigned";
			row["shift"] = x.ShiftId.HasValue ? shiftLookup.GetValueOrDefault(x.ShiftId.Value) : null;
			return row;
		}).ToList(), page.Value.Count);
	}
	private static Dictionary<string, Expression<Func<InstituteHierarchyRow, string?>>> HierarchyText() => new() {
		["year"] = x => x.Year, ["board"] = x => x.Board, ["medium"] = x => x.Medium,
		["stage"] = x => x.Stage, ["standard"] = x => x.Standard, ["division"] = x => x.Division };
	private async Task HydrateBoardNamesAsync(List<InstituteHierarchyRow> rows)
	{
		var names = await InstituteReportBoardNames.CustomAsync((await boards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id), rows.Select(x => x.EducationBoardId), AsyncExecuter);
		foreach (var row in rows)
			if (row.EducationBoardId.HasValue && names.TryGetValue(row.EducationBoardId.Value, out var name)) row.Board = name;
	}
	private async Task<(List<Dictionary<string, string?>> Rows, int Count)?> RoomsPageAsync(InstituteReportRequest request, bool all)
	{
		var f = request.Filters; var tenant = CurrentTenant.Id;
		var roomQuery = (await rooms.GetQueryableAsync()).Where(x => x.TenantId == tenant && (!f.BuildingId.HasValue || x.BuildingId == f.BuildingId)
			&& (!f.FloorId.HasValue || x.FloorId == f.FloorId) && (!f.RoomTypeId.HasValue || x.RoomTypeId == f.RoomTypeId));
		var buildingQuery = (await buildings.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var floorQuery = (await floors.GetQueryableAsync()).Where(x => buildingQuery.Any(building => building.Id == x.BuildingId));
		var typeQuery = (await roomTypes.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var query = from room in roomQuery
			join building in buildingQuery on room.BuildingId equals building.Id into bg from b in bg.DefaultIfEmpty()
			join floor in floorQuery on new { room.FloorId, room.BuildingId } equals new { FloorId = floor.Id, floor.BuildingId } into fg from fl in fg.DefaultIfEmpty()
			join type in typeQuery on room.RoomTypeId equals type.Id into tg from t in tg.DefaultIfEmpty()
			select new RoomRow { Id = room.Id, Room = room.RoomName, Building = b == null ? null : b.BuildingName, Floor = fl == null ? null : fl.FloorName,
				RoomType = t == null ? null : t.RoomTypeName, Capacity = room.Capacity, Benches = room.TotalBenches, BenchCapacity = room.BenchCapacity, Features = room.Features };
		var page = await InstituteReportQuery.PageAsync(query, request, all, AsyncExecuter, q => q.OrderBy(x => x.Room).ThenBy(x => x.Id),
			new() { ["room"] = x => x.Room, ["building"] = x => x.Building, ["floor"] = x => x.Floor, ["roomType"] = x => x.RoomType },
			new() { ["capacity"] = x => x.Capacity, ["benches"] = x => x.Benches, ["benchCapacity"] = x => x.BenchCapacity });
		return page == null ? null : (page.Value.Items.Select(x => Row(("building", x.Building), ("floor", x.Floor), ("room", x.Room), ("roomType", x.RoomType),
			("capacity", x.Capacity.ToString(CultureInfo.InvariantCulture)), ("benches", x.Benches.ToString(CultureInfo.InvariantCulture)),
			("benchCapacity", x.BenchCapacity.ToString(CultureInfo.InvariantCulture)), ("features", string.Join(", ", x.Features ?? [])))).ToList(), page.Value.Count);
	}
	private sealed class RoomRow
	{
		public Guid Id { get; set; }
		public string Room { get; set; } = "";
		public string? Building { get; set; }
		public string? Floor { get; set; }
		public string? RoomType { get; set; }
		public int Capacity { get; set; }
		public int Benches { get; set; }
		public int BenchCapacity { get; set; }
		public List<string>? Features { get; set; }
	}
}
