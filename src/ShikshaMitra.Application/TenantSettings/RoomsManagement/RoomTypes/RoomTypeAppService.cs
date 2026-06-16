using ShikshaMitra.Constants.Permissions;
using System;
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
}
