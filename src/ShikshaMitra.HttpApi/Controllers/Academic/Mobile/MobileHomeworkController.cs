using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;
using Volo.Abp.Content;

namespace ShikshaMitra.Controllers.Academic.Mobile;

[ApiController, Authorize]
[Route("api/mobile/v1/academics/homework")]
public class MobileHomeworkController(IMobileHomeworkAppService service) : ShikshaMitraController
{
	[HttpGet] public async Task<Result<MobilePage<MobileHomeworkDto>>> List([FromQuery] MobileListInput input) => Result.Success(await service.GetListAsync(input));
	[HttpGet("subjects")] public async Task<Result<List<MobileHomeworkSubjectDto>>> Subjects([FromQuery] MobileAcademicInput input) => Result.Success(await service.GetSubjectsAsync(input));
	[HttpGet("counts")] public async Task<Result<MobileHomeworkCountsDto>> Counts([FromQuery] MobileAcademicInput input) => Result.Success(await service.GetCountsAsync(input));
	[HttpGet("{id:guid}")] public async Task<Result<MobileHomeworkDto>> Detail(Guid id, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetAsync(id, input));
	[HttpPut("{id:guid}/submission/draft")] public async Task<Result<MobileHomeworkDto>> Draft(Guid id, [FromBody] MobileSubmissionInput input) => Result.Success(await service.SaveAsync(id, input, true, false));
	[HttpPost("{id:guid}/submission")] public async Task<Result<MobileHomeworkDto>> Submit(Guid id, [FromBody] MobileSubmissionInput input) => Result.Success(await service.SaveAsync(id, input, false, false));
	[HttpPut("{id:guid}/submission")] public async Task<Result<MobileHomeworkDto>> Resubmit(Guid id, [FromBody] MobileSubmissionInput input) => Result.Success(await service.SaveAsync(id, input, false, true));
	[HttpPost("{id:guid}/submission/attachments"), RequestSizeLimit(6 * 1024 * 1024)]
	public async Task<Result<MobileAttachmentDto>> Upload(Guid id, [FromQuery] MobileAcademicInput input, IFormFile file, [FromForm] Guid? clientUploadId = null)
	{
		using var stream = new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType);
		return Result.Success(await service.UploadAsync(id, input, stream, clientUploadId));
	}
	[HttpDelete("{id:guid}/submission/attachments/{attachmentId:guid}")]
	public async Task<IActionResult> Delete(Guid id, Guid attachmentId, [FromQuery] MobileAcademicInput input) { await service.DeleteAttachmentAsync(id, attachmentId, input); return NoContent(); }
	[HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
	public async Task<IActionResult> Download(Guid id, Guid attachmentId, [FromQuery] MobileAcademicInput input)
	{
		var file = await service.DownloadAsync(id, attachmentId, input);
		return File(file.Content, file.ContentType, file.Name);
	}
}
