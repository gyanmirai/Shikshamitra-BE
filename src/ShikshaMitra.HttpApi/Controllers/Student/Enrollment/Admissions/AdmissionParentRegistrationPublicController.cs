using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.Content;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Area("app"), RemoteService(Name = "Default"), AllowAnonymous, DisableAuditing]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/public/admission-registration"), EnableRateLimiting("public-admission-registration")]
public class AdmissionParentRegistrationPublicController(IAdmissionParentRegistrationPublicAppService service) : AbpController
{
	public const string TokenHeader = "X-Admission-Registration-Token";
	private string Token()
	{
		Response.Headers.CacheControl = "no-store, private";
		Response.Headers.XContentTypeOptions = "nosniff";
		Response.Headers["Referrer-Policy"] = "no-referrer";
		var values = Request.Headers[TokenHeader];
		if (values.Count != 1) throw new BusinessException(AdmissionParentRegistrationErrors.Unavailable);
		return values[0] ?? "";
	}
	[HttpGet] public Task<AdmissionParentPublicContextDto> GetAsync() => service.GetAsync(Token());
	[HttpPut("draft"), RequestSizeLimit(12 * 1024 * 1024)] public Task<AdmissionParentPublicContextDto> SaveAsync(AdmissionParentDraftInput input) => service.SaveAsync(Token(), input);
	[HttpPost("hand-in")] public Task<AdmissionParentPublicContextDto> HandInAsync(AdmissionParentPublicCommand input) => service.HandInAsync(Token(), input);
	[HttpPost("documents/{stageInstanceId:guid}/{requirementCode}")]
	[RequestSizeLimit(22 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 22 * 1024 * 1024)]
	public async Task<AdmissionParentPublicContextDto> UploadAsync(Guid stageInstanceId, string requirementCode, IFormFile file,
		[FromForm] string snapshotToken, [FromForm] string idempotencyKey, [FromForm] string? notes)
	{
		var token = Token();
		using var content = new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
		return await service.UploadAsync(token, stageInstanceId, requirementCode, new() { File = content, SnapshotToken = snapshotToken, IdempotencyKey = idempotencyKey, Notes = notes });
	}
	[HttpGet("files/{evidenceId:guid}")]
	public async Task<IActionResult> DownloadAsync(Guid evidenceId)
	{
		var file = await service.DownloadAsync(Token(), evidenceId);
		return File(file.Content, file.ContentType, file.FileName);
	}
}
