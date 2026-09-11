using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionOfferAppService : IApplicationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.View)]
	Task<AdmissionOfferStageDto> GetAsync(Guid applicationId, Guid stageId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Issue)]
	Task<AdmissionOfferStageDto> IssueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Respond)]
	Task<AdmissionOfferStageDto> RespondAsync(Guid applicationId, Guid stageId, RespondAdmissionOfferDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Extend)]
	Task<AdmissionOfferStageDto> ExtendAsync(Guid applicationId, Guid stageId, ExtendAdmissionOfferDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Withdraw)]
	Task<AdmissionOfferStageDto> WithdrawAsync(Guid applicationId, Guid stageId, AdmissionOfferReasonDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Reissue)]
	Task<AdmissionOfferStageDto> ReissueAsync(Guid applicationId, Guid stageId, IssueAdmissionOfferDto input);
}
