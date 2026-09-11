using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionOffers;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionOfferAppService(AdmissionOfferReadService reader, AdmissionOfferCommandService commands)
	: ApplicationService, IAdmissionOfferAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionOfferStageDto> GetAsync(Guid applicationId, Guid stageId) => reader.GetAsync(applicationId, stageId);
	[Authorize(Permissions.Issue)]
	public Task<AdmissionOfferStageDto> IssueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input) => commands.IssueAsync(applicationId, stageId, input);
	[Authorize(Permissions.Respond)]
	public Task<AdmissionOfferStageDto> RespondAsync(Guid applicationId, Guid stageId, RespondAdmissionOfferDto input) => commands.RespondAsync(applicationId, stageId, input);
	[Authorize(Permissions.Extend)]
	public Task<AdmissionOfferStageDto> ExtendAsync(Guid applicationId, Guid stageId, ExtendAdmissionOfferDto input) => commands.ExtendAsync(applicationId, stageId, input);
	[Authorize(Permissions.Withdraw)]
	public Task<AdmissionOfferStageDto> WithdrawAsync(Guid applicationId, Guid stageId, AdmissionOfferReasonDto input) => commands.WithdrawAsync(applicationId, stageId, input);
	[Authorize(Permissions.Reissue)]
	public Task<AdmissionOfferStageDto> ReissueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input) => commands.ReissueAsync(applicationId, stageId, input);
}
