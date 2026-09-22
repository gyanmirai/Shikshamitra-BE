using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[Area("finance")]
[Route("api/charge-categories")]
public class ChargeCategoryController(
	IChargeCategoryAppService chargeCategoryAppService
) : AbpController
{
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Add)]
	public Task<ChargeCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateChargeCategoryRequest input)
	{
		return chargeCategoryAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.View)]
	public Task<PaginatedChargeCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		return chargeCategoryAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	[HttpGet("{chargeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.View)]
	public Task<ChargeCategoryResponse> GetAsync(Guid chargeCategoryId)
	{
		return chargeCategoryAppService.GetAsync(chargeCategoryId);
	}

	[HttpPut("{chargeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Edit)]
	public Task<ChargeCategoryResponse> UpdateAsync(Guid chargeCategoryId, [FromBody] UpdateChargeCategoryRequest input)
	{
		return chargeCategoryAppService.UpdateAsync(chargeCategoryId, input);
	}

	[HttpPatch("{chargeCategoryId}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.ChangeStatus)]
	public Task<ChargeCategoryResponse> UpdateStatusAsync(
		Guid chargeCategoryId, [FromBody] UpdateChargeCategoryStatusRequest input)
	{
		return chargeCategoryAppService.UpdateStatusAsync(chargeCategoryId, input);
	}

	[HttpDelete("{chargeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Delete)]
	public Task DeleteAsync(Guid chargeCategoryId)
	{
		return chargeCategoryAppService.DeleteAsync(chargeCategoryId);
	}
}
