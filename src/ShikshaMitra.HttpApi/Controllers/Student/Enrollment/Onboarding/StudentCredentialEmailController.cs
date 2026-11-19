using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[ApiController]
[Authorize]
[Route("api/student/onboarding/{caseId:guid}/credential-emails")]
public class StudentCredentialEmailController(IStudentCredentialEmailAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<Result<IReadOnlyList<StudentCredentialEmailDto>>> GetAsync(Guid caseId) => Result.Success(await service.GetAsync(caseId));
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<StudentCredentialEmailDto>> RequestAsync(Guid caseId, RequestStudentCredentialEmailDto input) =>
		Result.Success(await service.RequestAsync(caseId, input));
}
