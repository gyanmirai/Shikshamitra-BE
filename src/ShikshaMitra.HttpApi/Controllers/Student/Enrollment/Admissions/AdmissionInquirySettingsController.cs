using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;

[ApiController, Authorize, Route("api/student/enrollment/admissions/inquiry-settings")]
public class AdmissionInquirySettingsController(IAdmissionInquirySettingsAppService service) : AbpController
{
	[HttpGet("counsellors"), Authorize(WorkflowPermissions.View)]
	public async Task<Result<List<AdmissionInquiryCounsellorDto>>> GetCounsellorsAsync(string? filter = null) =>
		Result.Success(await service.GetCounsellorsAsync(filter));
}
