using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.RoomsManagement.Rooms;

[Authorize]
[Route("api/institute-setup/rooms-management/rooms")]
public class RoomController(IRoomAppService roomAppService) : AbpController
{
	[HttpGet]
	public async Task<PagedResultDto<RoomDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await roomAppService.GetListAsync(input);
	}

	[HttpGet("{id}")]
	public async Task<RoomDto> GetAsync(Guid id)
	{
		return await roomAppService.GetAsync(id);
	}

	[HttpPost]
	public async Task<RoomDto> CreateAsync([FromBody] CreateUpdateRoomDto input)
	{
		return await roomAppService.CreateAsync(input);
	}

	[HttpPut("{id}")]
	public async Task<RoomDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRoomDto input)
	{
		return await roomAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	public async Task DeleteAsync(Guid id)
	{
		await roomAppService.DeleteAsync(id);
	}
}
