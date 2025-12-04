using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.RoomsManagement.RoomTypes;

[Authorize]
[Route("api/institute-setup/rooms-management/room-types")]
public class RoomTypeController(IRoomTypeAppService roomTypeAppService) : AbpController
{
	[HttpGet]
	public async Task<PagedResultDto<RoomTypeDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await roomTypeAppService.GetListAsync(input);
	}

	[HttpGet("{id}")]
	public async Task<RoomTypeDto> GetAsync(Guid id)
	{
		return await roomTypeAppService.GetAsync(id);
	}

	[HttpPost]
	public async Task<RoomTypeDto> CreateAsync([FromBody] CreateUpdateRoomTypeDto input)
	{
		return await roomTypeAppService.CreateAsync(input);
	}

	[HttpPut("{id}")]
	public async Task<RoomTypeDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRoomTypeDto input)
	{
		return await roomTypeAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	public async Task DeleteAsync(Guid id)
	{
		await roomTypeAppService.DeleteAsync(id);
	}
}
