using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Models;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/academics/curriculum/files")]
[Authorize]
[ApiController]
public class CurriculumFileController(
	ICurriculumFileManagementAppService fileAppService
) : AbpController
{
	[HttpPost]
	[Consumes("multipart/form-data")]
	[DisableRequestSizeLimit]
	[RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
	public async Task<Result<string>> UploadAsync([FromForm] CurriculumDocumentUploadRequest file)
	{
		await using var fileStream = file.File.OpenReadStream();

		var result = await fileAppService.UploadAsync(file.File.FileName, fileStream);

		return Result.Success(result);
	}
}
