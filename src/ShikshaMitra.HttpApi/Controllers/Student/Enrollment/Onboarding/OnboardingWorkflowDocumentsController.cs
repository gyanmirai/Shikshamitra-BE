using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[ApiController]
[Authorize(Permissions.StudentOnboarding.View)]
[Route("api/student/onboarding/{id:guid}/workflow-documents")]
public class OnboardingWorkflowDocumentsController(IOnboardingWorkflowDocumentsAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<List<AdmissionDocumentVerificationDto>>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));

	[HttpPost("{stageId:guid}/{code}/upload")]
	[Consumes("multipart/form-data")]
	[Authorize(Permissions.StudentOnboarding.Edit)]
	[Authorize(Permissions.AdmissionDocuments.Upload)]
	public async Task<Result<AdmissionDocumentVerificationDto>> UploadAsync(Guid id, Guid stageId, string code, [FromForm] AdmissionDocumentUploadInput input) => Result.Success(await service.UploadAsync(id, stageId, code, input));

	[HttpPost("{stageId:guid}/{code}/original")]
	[Authorize(Permissions.StudentOnboarding.Edit)]
	[Authorize(Permissions.AdmissionDocuments.RecordOriginal)]
	public async Task<Result<AdmissionDocumentVerificationDto>> RecordOriginalAsync(Guid id, Guid stageId, string code, AdmissionDocumentActionInput input) => Result.Success(await service.RecordOriginalAsync(id, stageId, code, input));

	[HttpPost("evidence/{evidenceId:guid}/review")]
	[Authorize(Permissions.StudentOnboarding.Edit)]
	[Authorize(Permissions.AdmissionDocuments.Review)]
	public async Task<Result<AdmissionDocumentVerificationDto>> ReviewAsync(Guid id, Guid evidenceId, AdmissionDocumentReviewInput input) => Result.Success(await service.ReviewAsync(id, evidenceId, input));
}
