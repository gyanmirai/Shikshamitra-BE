using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Content;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/document-verification")]
public class AdmissionDocumentVerificationController(IAdmissionDocumentVerificationAppService service) : AbpController
{
	[HttpGet("applications/{applicationId:guid}/stages/{stageInstanceId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionDocumentVerificationDto>> GetAsync(Guid applicationId, Guid stageInstanceId) => Result.Success(await service.GetAsync(applicationId, stageInstanceId));
	[HttpGet("document-types"), Authorize(WorkflowPermissions.View)]
	public async Task<Result<List<AdmissionDocumentTypeDto>>> GetDocumentTypesAsync() => Result.Success(await service.GetDocumentTypesAsync());
	[HttpPost("applications/{applicationId:guid}/stages/{stageInstanceId:guid}/requirements/{code}/upload"), Authorize(Permissions.Upload)]
	[RequestSizeLimit(22 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 22 * 1024 * 1024)]
	public async Task<Result<AdmissionDocumentVerificationDto>> UploadAsync(Guid applicationId, Guid stageInstanceId, string code,
		IFormFile file, [FromForm] string concurrencyStamp, [FromForm] string? notes)
	{
		using var content = new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
		return Result.Success(await service.UploadAsync(applicationId, stageInstanceId, code,
			new() { File = content, ConcurrencyStamp = concurrencyStamp, Notes = notes }));
	}
	[HttpPost("applications/{applicationId:guid}/stages/{stageInstanceId:guid}/requirements/{code}/original"), Authorize(Permissions.RecordOriginal)]
	public async Task<Result<AdmissionDocumentVerificationDto>> RecordOriginalAsync(Guid applicationId, Guid stageInstanceId, string code, AdmissionDocumentActionInput input) => Result.Success(await service.RecordOriginalAsync(applicationId, stageInstanceId, code, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageInstanceId:guid}/requirements/{code}/waive"), Authorize(Permissions.Waive)]
	public async Task<Result<AdmissionDocumentVerificationDto>> WaiveAsync(Guid applicationId, Guid stageInstanceId, string code, AdmissionDocumentWaiverInput input) => Result.Success(await service.WaiveAsync(applicationId, stageInstanceId, code, input));
	[HttpPost("evidence/{evidenceId:guid}/review"), Authorize(Permissions.Review)]
	public async Task<Result<AdmissionDocumentVerificationDto>> ReviewAsync(Guid evidenceId, AdmissionDocumentReviewInput input) => Result.Success(await service.ReviewAsync(evidenceId, input));
	[HttpGet("evidence/{evidenceId:guid}/download"), Authorize(Permissions.Download)]
	public async Task<IActionResult> DownloadAsync(Guid evidenceId)
	{
		var download = await service.DownloadAsync(evidenceId);
		Response.Headers.CacheControl = "no-store, private";
		Response.Headers.XContentTypeOptions = "nosniff";
		return File(download.Content, download.ContentType, download.FileName);
	}
}
