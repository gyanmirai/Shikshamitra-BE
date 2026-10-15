using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings;

[Authorize]
[Route("api/institute-setup/school-identity")]
public class SchoolIdentityController(ISchoolIdentityAppService service) : AbpControllerBase
{
	[HttpGet]
	public Task<SchoolIdentityProfileDto> GetAsync([FromQuery] SchoolIdentityContextDto input) => service.GetAsync(input);
	[HttpGet("branding")]
	public Task<SchoolIdentityProfileDto> BrandingAsync() => service.GetBrandingAsync();
	[HttpGet("branding/{kind:int}")]
	public async Task<IActionResult> BrandingAssetAsync(SchoolIdentityAssetKind kind)
	{
		var result = await service.GetBrandingAssetAsync(kind);
		Response.Headers.CacheControl = "private, no-store";
		return File(result.Content, result.ContentType);
	}
	[HttpPost("preview")]
	public async Task<IActionResult> PreviewAsync([FromBody] SchoolIdentityPreviewDto input)
	{
		var result = await service.PreviewAsync(input);
		Response.Headers.CacheControl = "private, no-store";
		return File(result.Content, result.ContentType);
	}
	[HttpPut]
	public Task<SchoolIdentityProfileDto> UpdateAsync([FromBody] SchoolIdentityUpdateDto input) => service.UpdateAsync(input);
	[HttpPost("copy")]
	public Task<SchoolIdentityProfileDto> CopyAsync([FromBody] SchoolIdentityCopyDto input) => service.CopyAsync(input);
	[HttpGet("history")]
	public Task<List<SchoolIdentityRevisionDto>> HistoryAsync([FromQuery] SchoolIdentityContextDto input) => service.GetHistoryAsync(input);
	[HttpGet("assets/{id:guid}")]
	public async Task<IActionResult> DownloadAsync(Guid id, [FromQuery] SchoolIdentityContextDto input)
	{
		var result = await service.DownloadAsync(id, input);
		Response.Headers.CacheControl = "private, no-store";
		Response.Headers["X-Content-Type-Options"] = "nosniff";
		return File(result.Content, result.ContentType);
	}
	[HttpPost("assets")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(6 * 1024 * 1024)]
	public async Task<SchoolIdentityAssetDto> UploadAsync([FromForm] SchoolIdentityUploadRequest input)
	{
		if (input.File == null || input.File.Length == 0 || input.File.Length > 5 * 1024 * 1024)
			throw new BusinessException(SchoolIdentityErrorCodes.ImageSizeInvalid);
		await using var stream = input.File.OpenReadStream();
		await using var memory = new MemoryStream();
		await stream.CopyToAsync(memory);
		return await service.UploadAsync(input.Kind, input.BoardId, input.MediumId, input.File.FileName, memory.ToArray());
	}
}

public sealed class SchoolIdentityUploadRequest
{
	public IFormFile? File { get; set; }
	public SchoolIdentityAssetKind Kind { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
}
