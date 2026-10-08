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
[Route("api/fee-categories")]
[Authorize]
public class FeeCategoryController(
	IFeeCategoryAppService feeCategoryAppService
) : AbpController
{
    [HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
	public Task<FeeCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeCategoryCreateRequest input)
    {
        return feeCategoryAppService.CreateAsync(academicYearId, input);
    }

    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
    public Task<FeeCategoryListResponse> GetListAsync([FromQuery] Guid academicYearId, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool? isActive = null, [FromQuery] string? filters = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null)
    {
        return feeCategoryAppService.GetListAsync(academicYearId, search, page, pageSize, isActive, filters, sortBy, sortDirection);
    }

    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
    public Task<FeeCategoryResponse> GetByIdAsync(Guid id)
    {
        return feeCategoryAppService.GetByIdAsync(id);
    }

    [HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
    public Task<FeeCategoryResponse> UpdateAsync(Guid id, [FromBody] FeeCategoryUpdateRequest input)
    {
        return feeCategoryAppService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Delete)]
	public Task<MessageResponse> DeleteAsync(Guid id)
    {
        return feeCategoryAppService.DeleteAsync(id);
    }

    [HttpPatch("{id}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
    public Task<FeeCategoryResponse> UpdateStatusAsync(Guid id, [FromBody] FeeCategoryStatusUpdateRequest input)
    {
        return feeCategoryAppService.UpdateStatusAsync(id, input);
    }
}
