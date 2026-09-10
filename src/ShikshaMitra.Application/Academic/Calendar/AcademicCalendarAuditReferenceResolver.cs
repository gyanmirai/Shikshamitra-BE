using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Calendar;

/// <summary>
/// Resolves relational identifiers stored in immutable calendar audit snapshots into
/// school-facing labels. Repository data filters remain active, so references cannot
/// cross tenant boundaries.
/// </summary>
public class AcademicCalendarAuditReferenceResolver(
	IRepository<Room, Guid> roomRepository,
	IRepository<Building, Guid> buildingRepository,
	IRepository<Floor, Guid> floorRepository) : ITransientDependency
{
	private const string UnavailableRoomLabel = "Room unavailable";

	public async Task<IReadOnlyDictionary<Guid, string>> GetRoomDisplayNamesAsync(
		IEnumerable<string?> values)
	{
		var roomIds = values
			.Select(ParseId)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		if (roomIds.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var rooms = await roomRepository.GetListAsync(x => roomIds.Contains(x.Id));
		var buildingIds = rooms.Select(x => x.BuildingId).Distinct().ToList();
		var floorIds = rooms.Select(x => x.FloorId).Distinct().ToList();
		var buildings = buildingIds.Count == 0
			? []
			: await buildingRepository.GetListAsync(x => buildingIds.Contains(x.Id));
		var floors = floorIds.Count == 0
			? []
			: await floorRepository.GetListAsync(x => floorIds.Contains(x.Id));
		var buildingNames = buildings.ToDictionary(x => x.Id, x => x.BuildingName);
		var floorNames = floors.ToDictionary(x => x.Id, x => x.FloorName);

		return rooms.ToDictionary(
			x => x.Id,
			x => BuildRoomDisplayName(
				buildingNames.GetValueOrDefault(x.BuildingId),
				floorNames.GetValueOrDefault(x.FloorId),
				x.RoomName));
	}

	public static bool IsRoomIdField(string fieldName) =>
		fieldName.EndsWith("RoomId", StringComparison.OrdinalIgnoreCase);

	public static string ToRoomFieldDisplayName(string displayName)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return "Room";
		}

		if (displayName.EndsWith(" Id", StringComparison.OrdinalIgnoreCase))
		{
			return displayName[..^3].TrimEnd();
		}

		return displayName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
			? displayName[..^2].TrimEnd()
			: displayName;
	}

	public static string? ResolveRoomValue(
		string? value,
		IReadOnlyDictionary<Guid, string> roomNames)
	{
		var roomId = ParseId(value);
		if (!roomId.HasValue)
		{
			return value;
		}

		return roomNames.GetValueOrDefault(roomId.Value) ?? UnavailableRoomLabel;
	}

	private static Guid? ParseId(string? value) =>
		Guid.TryParse(value?.Trim().Trim('"'), out var id) ? id : null;

	private static string BuildRoomDisplayName(
		string? buildingName,
		string? floorName,
		string? roomName)
	{
		var parts = new[] { buildingName, floorName, roomName }
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x!.Trim());
		var displayName = string.Join(" > ", parts);
		return string.IsNullOrWhiteSpace(displayName) ? UnavailableRoomLabel : displayName;
	}
}