using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public partial class RoomAppService
{
	private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["roomName"] = "Room.RoomName", ["capacity"] = "Room.Capacity", ["totalBenches"] = "Room.TotalBenches",
		["buildingName"] = "BuildingName", ["floorName"] = "FloorName", ["roomTypeName"] = "RoomTypeName",
		["creationTime"] = "Room.CreationTime", ["id"] = "Room.Id", ["benchCapacity"] = "Room.BenchCapacity",
		["benchesPerRow"] = "Room.BenchesPerRow", ["benchesPerColumn"] = "Room.BenchesPerColumn"
	};

	private async Task<PagedResultDto<RoomDto>> ReadPageAsync(GetRoomListDto input)
	{
		var filters = RoomColumnFilters.Parse(input.ColumnFilters);
		var rooms = await CreateFilteredQueryAsync(input);
		var featureFilters = filters.Where(filter => filter.Column == "features").ToList();
		if (featureFilters.Count > 0)
		{
			var matcher = LazyServiceProvider.LazyGetRequiredService<IRoomFeatureQuery>();
			var matchingIds = await matcher.GetMatchingRoomIdsAsync(featureFilters.Select(filter => new RoomFeatureTextFilter(
				filter.Type switch
				{
					"equals" => RoomFeatureTextOperator.Equals, "startsWith" => RoomFeatureTextOperator.StartsWith,
					"endsWith" => RoomFeatureTextOperator.EndsWith, _ => RoomFeatureTextOperator.Contains
				}, filter.Text!)).ToList());
			rooms = rooms.Where(room => matchingIds.Contains(room.Id));
		}
		var buildings = await _buildingRepository.GetQueryableAsync();
		var floors = await _floorRepository.GetQueryableAsync();
		var types = await _roomTypeRepository.GetQueryableAsync();
		var query = from room in rooms
			join building in buildings on room.BuildingId equals building.Id into buildingRows
			from building in buildingRows.DefaultIfEmpty()
			join floor in floors on new { Id = room.FloorId, room.BuildingId } equals new { floor.Id, floor.BuildingId } into floorRows
			from floor in floorRows.DefaultIfEmpty()
			join type in types on room.RoomTypeId equals type.Id into typeRows
			from type in typeRows.DefaultIfEmpty()
			select new RoomListRow
			{
				Room = room, BuildingName = building == null ? null : building.BuildingName,
				FloorName = floor == null ? null : floor.FloorName, RoomTypeName = type == null ? null : type.RoomTypeName
			};
		foreach (var group in filters.Where(filter => filter.Column != "features").GroupBy(filter => filter.Column))
		{
			var predicates = new List<string>();
			var values = new List<object>();
			foreach (var filter in group)
			{
				var field = SortColumns[filter.Column];
				var index = values.Count;
				if (filter.Text != null)
				{
					var target = $"({field} ?? \"\").ToLower()";
					predicates.Add(filter.Type switch
					{
						"equals" => $"{target} == @{index}", "startsWith" => $"{target}.StartsWith(@{index})",
						"endsWith" => $"{target}.EndsWith(@{index})", _ => $"{target}.Contains(@{index})"
					});
					values.Add(filter.Text.ToLowerInvariant());
				}
				else
				{
					predicates.Add(filter.Type switch
					{
						"between" => $"({field} >= @{index} && {field} <= @{index + 1})",
						"greaterThan" => $"{field} > @{index}", "lessThan" => $"{field} < @{index}", _ => $"{field} == @{index}"
					});
					values.Add(filter.Number);
					if (filter.Type == "between") values.Add(filter.Maximum);
				}
			}
			query = query.Where(string.Join(" || ", predicates.Select(predicate => "(" + predicate + ")")), values.ToArray());
		}
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(Sorting(input.Sorting)).ThenBy(row => row.Room.Id)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		return new(total, rows.Select(row =>
		{
			var dto = ObjectMapper.Map<Room, RoomDto>(row.Room);
			dto.BuildingName = row.BuildingName!;
			dto.FloorName = row.FloorName!;
			dto.RoomTypeName = row.RoomTypeName!;
			return dto;
		}).ToList());
	}

	private static string Sorting(string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting)) return "Room.CreationTime desc";
		if (sorting.Length > 200) throw RoomColumnFilters.Invalid();
		return string.Join(", ", sorting.Split(',').Select(part =>
		{
			var terms = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (terms.Length is < 1 or > 2 || !SortColumns.TryGetValue(terms[0], out var field)
				|| (terms.Length == 2 && !terms[1].Equals("asc", StringComparison.OrdinalIgnoreCase) && !terms[1].Equals("desc", StringComparison.OrdinalIgnoreCase)))
				throw RoomColumnFilters.Invalid();
			return field + (terms.Length == 2 ? " " + terms[1] : " asc");
		}));
	}

	private sealed class RoomListRow
	{
		public Room Room { get; set; } = null!;
		public string? BuildingName { get; set; }
		public string? FloorName { get; set; }
		public string? RoomTypeName { get; set; }
	}
}
