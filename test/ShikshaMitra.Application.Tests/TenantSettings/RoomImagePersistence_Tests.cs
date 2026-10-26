using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomImagePersistence_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Image_is_persisted_preserved_replaced_removed_and_tenant_isolated()
	{
		var service = GetRequiredService<IRoomAppService>();
		var tenant = GetRequiredService<ICurrentTenant>();
		using var context = tenant.Change(Guid.NewGuid());
		var input = await WithUnitOfWorkAsync(async () =>
		{
			var building = await GetRequiredService<IRepository<Building, Guid>>()
				.InsertAsync(new Building { BuildingName = "Photo Test", TotalFloors = 1 }, autoSave: true);
			var floor = await GetRequiredService<IRepository<Floor, Guid>>()
				.InsertAsync(new Floor { FloorName = "Ground", BuildingId = building.Id, FloorNo = 0, TotalRoom = 1 }, autoSave: true);
			var type = await GetRequiredService<IRepository<RoomType, Guid>>()
				.InsertAsync(new RoomType { RoomTypeName = "Classroom", ShortName = "CR" }, autoSave: true);
			return new CreateUpdateRoomDto
			{
				RoomName = "Photo Room", BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = type.Id,
				ColumnRows = [2], BenchCapacity = 2, Capacity = 4,
				Image = new RoomImageInput { FileName = "room.png", Content = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0] }
			};
		});
		var created = await service.CreateAsync(input);
		created.HasImage.ShouldBeTrue();
		(await service.GetImageAsync(created.Id))!.Content.ShouldBe(input.Image.Content);
		input.Image = null;
		(await service.UpdateAsync(created.Id, input)).HasImage.ShouldBeTrue();
		input.Image = new RoomImageInput { FileName = "room.jpg", Content = [255, 216, 255, 224, 0, 0, 0, 0, 0, 0, 0, 0] };
		await service.UpdateAsync(created.Id, input);
		(await service.GetImageAsync(created.Id))!.ContentType.ShouldBe("image/jpeg");
		using (tenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetImageAsync(created.Id));
		input.Image = null;
		input.RemoveImage = true;
		(await service.UpdateAsync(created.Id, input)).HasImage.ShouldBeFalse();
		(await service.GetImageAsync(created.Id)).ShouldBeNull();
	}
}
