using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("finance")]
[Route("api/fee-heads")]
[Authorize]
public class FeeHeadController(
	IFeeHeadAppService feeHeadAppService
) : AbpController
{
    [HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
	public Task<FeeHeadResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeHeadCreateRequest input)
    {
        return feeHeadAppService.CreateAsync(academicYearId, input);
    }

    [HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
    public Task<FeeHeadListResponse> GetListAsync(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid? feeCategoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeSuperseded = false,
		[FromQuery] bool effectiveOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return feeHeadAppService.GetListAsync(academicYearId, feeCategoryId, search, includeSuperseded, effectiveOnly, page, pageSize);
    }

    [HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
    public Task<FeeHeadResponse> GetByIdAsync(Guid id)
    {
        return feeHeadAppService.GetByIdAsync(id);
    }

    [HttpPut("{feeHeadId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
	public Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, [FromBody] FeeHeadUpdateRequest input)
    {
        return feeHeadAppService.UpdateAsync(feeHeadId, input);
    }

    [HttpDelete("{feeHeadId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Delete)]
	public Task<MessageResponse> DeleteAsync(Guid feeHeadId)
    {
        return feeHeadAppService.DeleteAsync(feeHeadId);
    }

    [HttpPatch("{feeHeadId}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
    public Task<FeeHeadResponse> UpdateStatusAsync(Guid feeHeadId, [FromBody] FeeHeadStatusUpdateRequest input)
    {
        return feeHeadAppService.UpdateStatusAsync(feeHeadId, input);
    }

    [HttpGet("{feeHeadId}/impact")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
    public Task<FeeHeadImpactResponse> GetImpactAsync(Guid feeHeadId) => feeHeadAppService.GetImpactAsync(feeHeadId);

    [HttpPost("{feeHeadId}/revisions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
    public Task<FeeHeadResponse> CreateRevisionAsync(Guid feeHeadId, [FromBody] FeeHeadRevisionRequest input) =>
        feeHeadAppService.CreateRevisionAsync(feeHeadId, input);
}
