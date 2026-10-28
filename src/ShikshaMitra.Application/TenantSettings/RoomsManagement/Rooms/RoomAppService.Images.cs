using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using ShikshaMitra.TenantSettings.Divisions;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public partial class RoomAppService
{
	private IBlobContainer ImageBlobs => LazyServiceProvider.LazyGetRequiredService<IBlobContainer>();

	public async Task<RoomImageDto?> GetImageAsync(Guid id)
	{
		await CheckGetPolicyAsync();
		var room = await Repository.GetAsync(id);
		if (!room.HasImage) return null;
		return new RoomImageDto
		{
			ContentType = room.GetProperty<string>("RoomImageContentType"),
			Content = await ImageBlobs.GetAllBytesAsync(room.GetProperty<string>("RoomImageBlob"))
		};
	}

	private async Task ApplyImageAsync(Room room, CreateUpdateRoomDto input)
	{
		if (input.Image == null && !input.RemoveImage) return;
		if (input.Image != null && input.RemoveImage)
			throw new UserFriendlyException("Choose a replacement room image or remove the current image.");
		var previous = room.GetProperty<string?>("RoomImageBlob", null);
		if (input.Image != null)
		{
			var contentType = RoomImageValidation.Validate(input.Image.FileName, input.Image.Content);
			var blobName = $"room-images/{CurrentTenant.Id}/{room.Id}/{GuidGenerator.Create():N}";
			await ImageBlobs.SaveAsync(blobName, input.Image.Content, overrideExisting: false);
			room.SetProperty("RoomImageBlob", blobName);
			room.SetProperty("RoomImageContentType", contentType);
		}
		else
		{
			room.RemoveProperty("RoomImageBlob");
			room.RemoveProperty("RoomImageContentType");
		}
		if (!string.IsNullOrEmpty(previous)) await ImageBlobs.DeleteAsync(previous);
	}

	[UnitOfWork(isTransactional: true)]
	public override async Task DeleteAsync(Guid id)
	{
		await CheckDeletePolicyAsync();
		var room = await Repository.GetAsync(id);
		var divisions = LazyServiceProvider.LazyGetRequiredService<IRepository<Division, Guid>>();
		using (LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>())
		{
			if (await divisions.AnyAsync(division => division.TenantId == CurrentTenant.Id && division.RoomId == id))
				throw new BusinessException(RoomsManagementErrorCodes.RoomInUse).WithData("RoomId", id);
		}
		var blob = room.GetProperty<string?>("RoomImageBlob", null);
		await base.DeleteAsync(id);
		if (!string.IsNullOrEmpty(blob)) await ImageBlobs.DeleteAsync(blob);
	}
}
