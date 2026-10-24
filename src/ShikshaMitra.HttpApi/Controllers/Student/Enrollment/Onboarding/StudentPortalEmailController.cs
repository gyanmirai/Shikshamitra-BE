using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Portal;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[ApiController]
[Route("api/student/portal/email")]
public class StudentPortalEmailController(IStudentPortalEmailChangeAppService changes) : AbpController
{
	[HttpPost("confirm")]
	[AllowAnonymous]
	[DisableAuditing]
	public async Task<Result> ConfirmAsync(ConfirmStudentPortalEmailDto input)
	{
		await changes.ConfirmAsync(input);
		return Result.Success();
	}
}
