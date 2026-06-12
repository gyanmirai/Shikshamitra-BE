using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Files;

[Route("api/files")]
[Authorize]
[ApiController]
public class FileStorageController(
	IFileStorageAppService fileAppService
) : AbpController
{
	[HttpPost]
	[Consumes("multipart/form-data")]
	[DisableRequestSizeLimit]
	[RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
	public async Task<Result<string>> UploadAsync([FromForm] CurriculumDocumentUploadRequest file)
	{
		if (file?.File == null || file.File.Length == 0)
			throw new UserFriendlyException("File is required");

		using var ms = new MemoryStream();
		await file.File.CopyToAsync(ms);

		var uniqueFileName = await fileAppService.UploadAsync(file.File.FileName, ms.ToArray());

		return Result.Success(uniqueFileName);
	}

	[HttpGet("{fileName}")]
	public async Task<IActionResult> GetAsync(string fileName)
	{
		var result = await fileAppService.GetAsync(fileName);
		return File(result.Content, result.ContentType, result.FileName);
	}
}
