using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.RoomsManagement.Rooms;

[Authorize]
[Route("api/institute-setup/rooms")]
public class RoomController(IRoomAppService roomAppService) : AbpController
{
	[HttpGet]
	[ProducesResponseType(typeof(PagedResultDto<RoomDto>), StatusCodes.Status200OK)]
	public async Task<PagedResultDto<RoomDto>> GetListAsync([FromQuery] GetRoomListDto input)
	{
		return await roomAppService.GetListAsync(input);
	}

	[HttpGet("{id}")]
	[ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<RoomDto> GetAsync(Guid id)
	{
		return await roomAppService.GetAsync(id);
	}

	[HttpPost]
	[ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateUpdateRoomDto input)
	{
		var room = await roomAppService.CreateAsync(input);
		return StatusCode(StatusCodes.Status201Created, room);
	}

	[HttpPut("{id}")]
	[ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<RoomDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRoomDto input)
	{
		return await roomAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync(Guid id)
	{
		await roomAppService.DeleteAsync(id);
		return NoContent();
	}
}
