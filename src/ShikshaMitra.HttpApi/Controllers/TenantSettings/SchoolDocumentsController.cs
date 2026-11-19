using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Models;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings;

[Authorize]
[Route("api/institute-setup/school-documents")]
public class SchoolDocumentsController(ISchoolDocumentAppService service) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<SchoolDocumentDto>> ListAsync([FromQuery] SchoolDocumentListInput input) => service.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<SchoolDocumentDto> GetAsync(Guid id) => service.GetAsync(id);

	[HttpPost]
	[Authorize(SchoolDocumentPermissions.View), Authorize(SchoolDocumentPermissions.Add)]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(30 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
	public async Task<SchoolDocumentDto> CreateAsync([FromForm] SchoolDocumentWriteRequest input) =>
		await service.CreateAsync(await ReadAsync(input));

	[HttpPut("{id:guid}")]
	[Authorize(SchoolDocumentPermissions.View), Authorize(SchoolDocumentPermissions.Add)]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(30 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
	public async Task<SchoolDocumentDto> UpdateAsync(Guid id, [FromForm] SchoolDocumentWriteRequest input) =>
		await service.UpdateAsync(id, await ReadAsync(input));

	[HttpDelete("{id:guid}")]
	public Task DeleteAsync(Guid id) => service.DeleteAsync(id);

	[HttpGet("{id:guid}/files/{fileId:guid}")]
	public async Task<IActionResult> FileAsync(Guid id, Guid fileId, [FromQuery] bool download = false)
	{
		var result = await service.GetFileAsync(id, fileId);
		Response.Headers.CacheControl = "private, no-store";
		Response.Headers["X-Content-Type-Options"] = "nosniff";
		return download ? File(result.Content, result.ContentType, result.FileName) : File(result.Content, result.ContentType);
	}

	private async Task<SchoolDocumentWriteDto> ReadAsync(SchoolDocumentWriteRequest input)
	{
		if (input.Files.Count > SchoolDocumentConsts.MaxFiles) throw new BusinessException(SchoolDocumentConsts.InvalidFiles);
		var result = new SchoolDocumentWriteDto
		{
			Name = input.Name, Category = input.Category ?? throw new BusinessException(SchoolDocumentConsts.InvalidDetails), Notes = input.Notes,
			ConcurrencyStamp = input.ConcurrencyStamp, RetainedFileIds = input.RetainedFileIds
		};
		foreach (var file in input.Files)
		{
			if (file.Length is <= 0 or > SchoolDocumentConsts.MaxFileBytes) throw new BusinessException(SchoolDocumentConsts.InvalidFile);
			await using var stream = file.OpenReadStream();
			await using var memory = new MemoryStream();
			await stream.CopyToAsync(memory, HttpContext.RequestAborted);
			result.Files.Add(new() { FileName = file.FileName, ContentType = file.ContentType, Content = memory.ToArray() });
		}
		return result;
	}
}
