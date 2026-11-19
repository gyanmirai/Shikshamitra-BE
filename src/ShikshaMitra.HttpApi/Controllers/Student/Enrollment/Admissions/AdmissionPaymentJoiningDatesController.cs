using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/applications/{applicationId:guid}/joining-date")]
public class AdmissionPaymentJoiningDatesController(IAdmissionPaymentJoiningDateAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<AdmissionPaymentJoiningDateDto>> GetAsync(Guid applicationId) => Result.Success(await service.GetAsync(applicationId));
	[HttpPost("preview")]
	public async Task<Result<AdmissionPaymentJoiningDatePreviewDto>> PreviewAsync(Guid applicationId, PreviewAdmissionPaymentJoiningDateDto input) => Result.Success(await service.PreviewAsync(applicationId, input));
	[HttpPut]
	public async Task<Result<AdmissionPaymentJoiningDateDto>> SaveAsync(Guid applicationId, SaveAdmissionPaymentJoiningDateDto input) => Result.Success(await service.SaveAsync(applicationId, input));
}
