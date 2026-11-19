using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Collections;

[Authorize]
[ApiController]
[Area("finance")]
[Route("api/fee-collections/admission-applications/{applicationId:guid}")]
public class AdmissionFeeCollectionController(IAdmissionFeeCollectionAppService service) : AbpControllerBase
{
	[HttpGet("summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public Task<AdmissionFeeCollectionDto> GetSummaryAsync(Guid applicationId) => service.GetSummaryAsync(applicationId);

	[HttpPost("prepare")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public Task<AdmissionFeeCollectionDto> PrepareAsync(Guid applicationId, [FromBody] PrepareAdmissionFeeCollectionDto input) => service.PrepareAsync(applicationId, input);

	[HttpGet("collect-form")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public Task<FeeCollectFormDto> GetCollectFormAsync(Guid applicationId) => service.GetCollectFormAsync(applicationId);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public Task<CollectFeeOutput> CollectAsync(Guid applicationId, [FromBody] CollectAdmissionFeeInput input) => service.CollectAsync(applicationId, input);
}
