using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionOffers;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/offers")]
public class AdmissionOffersController(IAdmissionOfferAppService service) : AbpController
{
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionOfferStageDto>> GetAsync(Guid applicationId, Guid stageId) =>
		Result.Success(await service.GetAsync(applicationId, stageId));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/issue"), Authorize(Permissions.Issue)]
	public async Task<Result<AdmissionOfferStageDto>> IssueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input) =>
		Result.Success(await service.IssueAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/respond"), Authorize(Permissions.Respond)]
	public async Task<Result<AdmissionOfferStageDto>> RespondAsync(Guid applicationId, Guid stageId, RespondAdmissionOfferDto input) =>
		Result.Success(await service.RespondAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/extend"), Authorize(Permissions.Extend)]
	public async Task<Result<AdmissionOfferStageDto>> ExtendAsync(Guid applicationId, Guid stageId, ExtendAdmissionOfferDto input) =>
		Result.Success(await service.ExtendAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/withdraw"), Authorize(Permissions.Withdraw)]
	public async Task<Result<AdmissionOfferStageDto>> WithdrawAsync(Guid applicationId, Guid stageId, AdmissionOfferReasonDto input) =>
		Result.Success(await service.WithdrawAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/reissue"), Authorize(Permissions.Reissue)]
	public async Task<Result<AdmissionOfferStageDto>> ReissueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input) =>
		Result.Success(await service.ReissueAsync(applicationId, stageId, input));
}
