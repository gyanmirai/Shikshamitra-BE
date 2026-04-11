using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Models;
using Volo.Abp;
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
		if (file?.File == null || file.File.Length == 0)
			throw new UserFriendlyException("File is required");

		using var memoryStream = new MemoryStream();
		await file.File.CopyToAsync(memoryStream);

		var result = await fileAppService.UploadAsync(
			file.File.FileName,
			memoryStream.ToArray()
		);

		return Result.Success(result);
	}

}
