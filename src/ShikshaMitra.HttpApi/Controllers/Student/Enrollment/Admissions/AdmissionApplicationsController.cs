using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/applications")]
public class AdmissionApplicationsController(IAdmissionApplicationAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<Volo.Abp.Application.Dtos.PagedResultDto<AdmissionApplicationDto>>> GetListAsync([FromQuery] AdmissionApplicationListInput input) => Result.Success(await service.GetListAsync(input));

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionApplicationDto>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));

	[HttpPost("start")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public async Task<Result<AdmissionApplicationDto>> StartAsync([FromQuery] Guid applicantId, [FromQuery] Guid offeringId, [FromQuery] Guid? inquiryId = null) => Result.Success(await service.StartAsync(applicantId, offeringId, inquiryId));

	[HttpPost("/api/student/enrollment/admissions/applicants/{applicantId:guid}/applications/start")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public async Task<Result<AdmissionApplicationDto>> StartForApplicantAsync(Guid applicantId, [FromQuery] Guid offeringId, [FromQuery] Guid? inquiryId = null) => Result.Success(await service.StartAsync(applicantId, offeringId, inquiryId));

	[HttpGet("{id:guid}/workflow")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionApplicationWorkflowDto>> GetWorkflowAsync(Guid id) => Result.Success(await service.GetWorkflowAsync(id));

	[HttpGet("{id:guid}/form")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionApplicationFormDto>> GetFormAsync(Guid id) => Result.Success(await service.GetFormAsync(id));

	[HttpPatch("{id:guid}/form-values")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public async Task<Result<AdmissionApplicationFormDto>> UpdateFormValuesAsync(Guid id, UpdateAdmissionApplicationFormValuesDto input) => Result.Success(await service.UpdateFormValuesAsync(id, input));

	[HttpPost("{id:guid}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Submit)]
	public async Task<Result<AdmissionApplicationSubmitResultDto>> SubmitAsync(Guid id) => Result.Success(await service.SubmitAsync(id));
}
