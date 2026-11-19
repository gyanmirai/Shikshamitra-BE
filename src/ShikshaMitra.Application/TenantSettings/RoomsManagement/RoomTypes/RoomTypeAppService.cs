using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Formatting;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomTypeAppService : CrudAppService<RoomType, RoomTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateRoomTypeDto>, IRoomTypeAppService
{
	public RoomTypeAppService(IRepository<RoomType, Guid> repository) : base(repository)
	{
		GetPolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.View;
		GetListPolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.View;
		CreatePolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.Add;
		UpdatePolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.Add;
		DeletePolicyName = ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.Delete;
	}

	public override async Task<RoomTypeDto> CreateAsync(CreateUpdateRoomTypeDto input)
	{
		await CheckCreatePolicyAsync();
		var rules = await LazyServiceProvider.LazyGetRequiredService<InstituteFieldFormattingStore>().GetAsync("roomTypes");
		input.RoomTypeName = InstituteFieldFormattingCatalog.Required(rules, "roomTypeName", input.RoomTypeName, RoomTypeConsts.MaxRoomTypeLength);
		input.ShortName = InstituteFieldFormattingCatalog.Required(rules, "shortName", input.ShortName, RoomTypeConsts.MaxShortNameLength);
		return await base.CreateAsync(input);
	}
}
