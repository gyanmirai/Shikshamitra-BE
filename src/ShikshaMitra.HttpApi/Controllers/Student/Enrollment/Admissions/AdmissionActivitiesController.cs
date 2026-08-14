using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/activities")]
public class AdmissionActivitiesController(IAdmissionActivityAppService service) : AbpController
{
	[HttpGet("applicant/{applicantId:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<List<AdmissionActivityDto>>> GetForApplicantAsync(Guid applicantId) => Result.Success(await service.GetForApplicantAsync(applicantId));
	[HttpGet("application/{applicationId:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<List<AdmissionActivityDto>>> GetForApplicationAsync(Guid applicationId) => Result.Success(await service.GetForApplicationAsync(applicationId));
}
