using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{

	private Task<Room> CreateLegacyAdmissionRoomAsync(int capacity) => WithUnitOfWorkAsync(async () =>
	{
		var building = new Building { TenantId = TestTenantId, BuildingName = "Admission capacity block", TotalFloors = 1 };
		AssignCapacityEntityId(building);
		await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building, autoSave: true);
		var floor = new Floor { BuildingId = building.Id, FloorName = "Ground", FloorNo = 0, TotalRoom = 1 };
		AssignCapacityEntityId(floor);
		await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(floor, autoSave: true);
		var type = new RoomType { TenantId = TestTenantId, RoomTypeName = "Classroom", ShortName = "CLS" };
		AssignCapacityEntityId(type);
		await GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(type, autoSave: true);
		var room = new Room
		{
			TenantId = TestTenantId, RoomName = "Capacity room", BuildingId = building.Id,
			FloorId = floor.Id, RoomTypeId = type.Id, Capacity = capacity
		};
		AssignCapacityEntityId(room);
		return await GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room, autoSave: true);
	});

	private static void AssignCapacityEntityId<TEntity>(TEntity entity) where TEntity : Entity<Guid> =>
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, Guid.NewGuid());
}
