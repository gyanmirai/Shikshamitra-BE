using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Models;
using System.Threading.Tasks;
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
	public async Task<Result<string>> UploadAsync([FromForm] CurriculumDocumentUploadRequest file)
	{
		// convert IFormFile to Stream
		var fileStream = file.File.OpenReadStream();

		var result = await fileAppService.UploadAsync(file.FileName, fileStream);

		return Result<string>.Success(result);
	}
}
