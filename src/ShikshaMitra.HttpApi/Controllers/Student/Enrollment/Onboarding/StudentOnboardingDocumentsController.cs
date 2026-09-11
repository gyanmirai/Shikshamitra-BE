using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Content;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.StudentOnboarding;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[ApiController, Authorize(Permissions.View)]
[Route("api/student/onboarding/{caseId:guid}/documents")]
public class StudentOnboardingDocumentsController(IStudentOnboardingDocumentAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<StudentOnboardingDocumentsDto>> GetAsync(Guid caseId) => Result.Success(await service.GetAsync(caseId));

	[HttpPost, Authorize(Permissions.Edit)]
	[RequestSizeLimit(22 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 22 * 1024 * 1024)]
	public async Task<Result<StudentOnboardingDocumentsDto>> UploadAsync(Guid caseId, IFormFile file,
		[FromForm] Guid documentTypeId, [FromForm] string concurrencyStamp)
	{
		using var content = new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
		return Result.Success(await service.UploadAsync(caseId,
			new() { DocumentTypeId = documentTypeId, ConcurrencyStamp = concurrencyStamp, File = content }));
	}
}
