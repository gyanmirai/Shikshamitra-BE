using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
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
	public async Task<IActionResult> UploadAsync([FromForm] IFormFile file)
	{
		using var stream = file.OpenReadStream();

		var blobName = await fileAppService.UploadAsync(
			file.FileName,
			file.ContentType,
			file.Length,
			stream
		);

		return Ok(new { blobName });
	}


}
