using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController][Authorize][Route("api/students/records/guardian-access")]
public class StudentGuardianAccessController(IStudentGuardianAccessAppService service) : AbpController
{
	[HttpGet("students/{studentId:guid}/links")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.View)]
	public async Task<Result<List<StudentGuardianPortalLinkDto>>> LinksAsync(Guid studentId) => Result.Success(await service.GetLinksAsync(studentId));
	[HttpGet("students/{studentId:guid}/reviews")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.View)]
	public async Task<Result<List<StudentGuardianAccessReviewDto>>> ReviewsAsync(Guid studentId) => Result.Success(await service.GetReviewsAsync(studentId));
	[HttpGet("students/{studentId:guid}/candidate")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.Manage)]
	public async Task<Result<StudentGuardianPortalLinkDto?>> CandidateAsync(Guid studentId, [FromQuery] string relationship) => Result.Success(await service.GetVerifiedGuardianCandidateAsync(studentId, relationship));
	[HttpPost("reviews/{reviewId:guid}/resolve")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.Manage)]
	public async Task<Result<StudentGuardianAccessReviewDto>> ResolveAsync(Guid reviewId, [FromBody] ResolveStudentGuardianAccessDto input) => Result.Success(await service.ResolveAsync(reviewId, input));
}
