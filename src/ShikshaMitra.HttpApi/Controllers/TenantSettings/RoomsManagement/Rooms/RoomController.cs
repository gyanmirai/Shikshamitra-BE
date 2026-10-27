using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Models;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Validation;

namespace ShikshaMitra.Controllers.TenantSettings.RoomsManagement.Rooms;

[Authorize]
[Route("api/institute-setup/rooms")]
public class RoomController(IRoomAppService roomAppService) : AbpController
{
	private static readonly JsonSerializerOptions RoomJson = new(JsonSerializerDefaults.Web);

	[HttpGet("{id}/image")]
	public Task<RoomImageDto?> GetImageAsync(Guid id) => roomAppService.GetImageAsync(id);

	[HttpGet("{id}/image/file")]
	public async Task<IActionResult> GetImageFileAsync(Guid id)
	{
		var image = await roomAppService.GetImageAsync(id);
		Response.Headers.CacheControl = "private, no-store";
		Response.Headers["X-Content-Type-Options"] = "nosniff";
		return image == null ? NoContent() : File(image.Content, image.ContentType);
	}

	[HttpGet("features")]
	public Task<ListResultDto<RoomFeatureDto>> GetFeaturesAsync() => roomAppService.GetFeaturesAsync();

	[HttpPost("features")]
	public Task<RoomFeatureDto> CreateFeatureAsync([FromBody] CreateRoomFeatureDto input) => roomAppService.CreateFeatureAsync(input);

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
	[Consumes("application/json")]
	[ApiExplorerSettings(IgnoreApi = true)]
	[ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateUpdateRoomDto input)
	{
		var room = await roomAppService.CreateAsync(input);
		return StatusCode(StatusCodes.Status201Created, room);
	}

	[HttpPut("{id}")]
	[Consumes("application/json")]
	[ApiExplorerSettings(IgnoreApi = true)]
	[ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<RoomDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRoomDto input)
	{
		return await roomAppService.UpdateAsync(id, input);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add)]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024, ValueLengthLimit = 16384)]
	public async Task<IActionResult> CreateMultipartAsync([FromForm] RoomWriteRequest request)
	{
		var room = await roomAppService.CreateAsync(await ReadMultipartAsync(request));
		return StatusCode(StatusCodes.Status201Created, room);
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add)]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024, ValueLengthLimit = 16384)]
	public async Task<RoomDto> UpdateMultipartAsync(Guid id, [FromForm] RoomWriteRequest request) =>
		await roomAppService.UpdateAsync(id, await ReadMultipartAsync(request));

	private async Task<CreateUpdateRoomDto> ReadMultipartAsync(RoomWriteRequest input)
	{
		CreateUpdateRoomDto command;
		try
		{
			command = JsonSerializer.Deserialize<CreateUpdateRoomDto>(input.Input, RoomJson)
				?? throw new JsonException();
		}
		catch (JsonException)
		{
			throw new AbpValidationException("Room input must contain a valid room command.",
				[new ValidationResult("Enter valid room details.", ["input"])]);
		}
		if (command.Image != null)
			throw new AbpValidationException("Send the room image as the image file field.",
				[new ValidationResult("Use the image file field.", ["image"])]);
		if (input.Image != null)
		{
			if (input.Image.Length is <= 0 or > RoomConsts.MaxImageBytes)
				throw new BusinessException("Rooms:InvalidImage", "Room image must be a JPG or PNG file up to 5 MB.");
			await using var stream = input.Image.OpenReadStream();
			await using var memory = new MemoryStream();
			await stream.CopyToAsync(memory, HttpContext.RequestAborted);
			command.Image = new RoomImageInput { FileName = Path.GetFileName(input.Image.FileName), Content = memory.ToArray() };
		}
		return command;
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
