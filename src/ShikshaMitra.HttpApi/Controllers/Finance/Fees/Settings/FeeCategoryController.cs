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
	public Task<MessageResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeCategoryCreateRequest input)
    {
        return feeCategoryAppService.CreateAsync(academicYearId, input);
    }

    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
    public Task<FeeCategoryListResponse> GetListAsync([FromQuery] Guid academicYearId, [FromQuery] string search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return feeCategoryAppService.GetListAsync(academicYearId, search, page, pageSize);
    }

    [HttpPut("{feeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add)]
    public Task<MessageResponse> UpdateAsync(Guid feeCategoryId, [FromBody] FeeCategoryUpdateRequest input)
    {
        return feeCategoryAppService.UpdateAsync(feeCategoryId, input);
    }

    [HttpDelete("{feeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Delete)]
	public Task<MessageResponse> DeleteAsync(Guid feeCategoryId)
    {
        return feeCategoryAppService.DeleteAsync(feeCategoryId);
    }
}
