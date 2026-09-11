using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionPayments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/payments")]
public class AdmissionPaymentsController(IAdmissionPaymentAppService service) : AbpController
{
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionPaymentStageDto>> GetAsync(Guid applicationId, Guid stageId) =>
		Result.Success(await service.GetAsync(applicationId, stageId));

	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/transactions"), Authorize(Permissions.Record)]
	public async Task<Result<AdmissionPaymentStageDto>> RecordAsync(
		Guid applicationId, Guid stageId, RecordAdmissionPaymentDto input) =>
		Result.Success(await service.RecordAsync(applicationId, stageId, input));

	[HttpPost("transactions/{transactionId:guid}/review"), Authorize(Permissions.Verify)]
	public async Task<Result<AdmissionPaymentStageDto>> ReviewAsync(Guid transactionId, ReviewAdmissionPaymentDto input) =>
		Result.Success(await service.ReviewAsync(transactionId, input));

	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/waive"), Authorize(Permissions.Waive)]
	public async Task<Result<AdmissionPaymentStageDto>> WaiveAsync(
		Guid applicationId, Guid stageId, AdmissionPaymentReasonDto input) =>
		Result.Success(await service.WaiveAsync(applicationId, stageId, input));

	[HttpPost("transactions/{transactionId:guid}/reverse"), Authorize(Permissions.Reverse)]
	public async Task<Result<AdmissionPaymentStageDto>> ReverseAsync(Guid transactionId, AdmissionPaymentReasonDto input) =>
		Result.Success(await service.ReverseAsync(transactionId, input));
}
