using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Audit;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarAuditRoomReference_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IAcademicCalendarAppService _calendarAuditService;
	private readonly IAcademicAuditTrailAppService _academicAuditService;
	private readonly IRepository<AcademicCalendarAuditLog, Guid> _auditLogRepository;
	private readonly IRepository<Building, Guid> _buildingRepository;
	private readonly IRepository<Floor, Guid> _floorRepository;
	private readonly IRepository<RoomType, Guid> _roomTypeRepository;
	private readonly IRepository<Room, Guid> _roomRepository;

	public AcademicCalendarAuditRoomReference_Tests()
	{
		_calendarAuditService = GetRequiredService<IAcademicCalendarAppService>();
		_academicAuditService = GetRequiredService<IAcademicAuditTrailAppService>();
		_auditLogRepository = GetRequiredService<IRepository<AcademicCalendarAuditLog, Guid>>();
		_buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
		_floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
		_roomTypeRepository = GetRequiredService<IRepository<RoomType, Guid>>();
		_roomRepository = GetRequiredService<IRepository<Room, Guid>>();
	}

	[Fact]
	public async Task Audit_Endpoints_Should_Resolve_Room_Ids_And_Remove_Id_From_Label()
	{
		var (oldRoom, newRoom) = await SeedRoomsAsync();
		var auditLogId = Guid.NewGuid();
		var changedFieldsJson = JsonSerializer.Serialize(
			new[]
			{
				new AuditLogDiffDto
				{
					FieldName = "RoomId",
					DisplayName = "Room Id",
					OldValue = oldRoom.Id.ToString(),
					NewValue = newRoom.Id.ToString(),
					ChangeType = "updated"
				}
			},
			new JsonSerializerOptions(JsonSerializerDefaults.Web));
		var auditLog = new AcademicCalendarAuditLog(
			auditLogId,
			null,
			Guid.NewGuid(),
			CalendarAuditAction.DayUpdated,
			"{}",
			"{}",
			Guid.NewGuid(),
			entityType: DayType.PTM.ToString(),
			entityName: "PTM Day",
			changedFieldsJson: changedFieldsJson);
		await _auditLogRepository.InsertAsync(auditLog, autoSave: true);

		var calendarLogs = await _calendarAuditService.GetAuditLogsAsync(null, null, 0, 100);
		var calendarRoomDiff = calendarLogs.Items.Single(x => x.Id == auditLogId).Diff.Single();
		AssertRoomDiff(calendarRoomDiff.DisplayName, calendarRoomDiff.OldValue, calendarRoomDiff.NewValue);

		var academicLogs = await _academicAuditService.GetListAsync(new AcademicAuditEventListRequestDto
		{
			SubModuleCode = "academic_calendar",
			SkipCount = 0,
			MaxResultCount = 500
		});
		var academicRoomDiff = academicLogs.Items
			.Single(x => x.SourceAuditLogId == auditLogId)
			.ChangedFields
			.Single();
		AssertRoomDiff(academicRoomDiff.DisplayName, academicRoomDiff.OldValue, academicRoomDiff.NewValue);
	}

	[Fact]
	public void Missing_Room_Should_Use_A_Safe_Label_Instead_Of_Exposing_Its_Id()
	{
		var missingRoomId = Guid.NewGuid();

		var value = AcademicCalendarAuditReferenceResolver.ResolveRoomValue(
			missingRoomId.ToString(),
			new Dictionary<Guid, string>());

		value.ShouldBe("Room unavailable");
		value.ShouldNotBe(missingRoomId.ToString());
	}

	private async Task<(Room OldRoom, Room NewRoom)> SeedRoomsAsync()
	{
		var building = new Building { BuildingName = "Science And Activity Block", TotalFloors = 2 };
		SetEntityId(building, Guid.NewGuid());
		await _buildingRepository.InsertAsync(building, autoSave: true);

		var firstFloor = await CreateFloorAsync(building.Id, "First Floor", 1);
		var groundFloor = await CreateFloorAsync(building.Id, "Ground Floor", 0);
		var roomType = new RoomType { RoomTypeName = "Meeting Room", ShortName = "MTG" };
		SetEntityId(roomType, Guid.NewGuid());
		await _roomTypeRepository.InsertAsync(roomType, autoSave: true);

		var oldRoom = await CreateRoomAsync(building.Id, firstFloor.Id, roomType.Id, "PTM Room 101");
		var newRoom = await CreateRoomAsync(building.Id, groundFloor.Id, roomType.Id, "PTM Room 001");
		return (oldRoom, newRoom);
	}

	private async Task<Floor> CreateFloorAsync(Guid buildingId, string name, int number)
	{
		var floor = new Floor { BuildingId = buildingId, FloorName = name, FloorNo = number, TotalRoom = 1 };
		SetEntityId(floor, Guid.NewGuid());
		return await _floorRepository.InsertAsync(floor, autoSave: true);
	}

	private async Task<Room> CreateRoomAsync(Guid buildingId, Guid floorId, Guid roomTypeId, string name)
	{
		var room = new Room
		{
			BuildingId = buildingId,
			FloorId = floorId,
			RoomTypeId = roomTypeId,
			RoomName = name,
			Capacity = 20
		};
		SetEntityId(room, Guid.NewGuid());
		return await _roomRepository.InsertAsync(room, autoSave: true);
	}

	private static void AssertRoomDiff(string displayName, string? oldValue, string? newValue)
	{
		displayName.ShouldBe("Room");
		oldValue.ShouldBe("Science And Activity Block > First Floor > PTM Room 101");
		newValue.ShouldBe("Science And Activity Block > Ground Floor > PTM Room 001");
	}

	private static void SetEntityId<TEntity>(TEntity entity, Guid id)
		where TEntity : Entity<Guid> =>
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, id);
}