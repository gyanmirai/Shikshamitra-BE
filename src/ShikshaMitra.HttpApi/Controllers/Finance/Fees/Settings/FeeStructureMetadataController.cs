using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("finance")]
[Route("api/fee-structures-metadata")]
[Authorize]
public class FeeStructureMetadataController(
	IFeeStructureMetadataAppService feeStructureMetadataAppService
) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<FeeStructureMetadataListResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null,
		[FromQuery] string? filters = null)
	{
		return await feeStructureMetadataAppService.GetListAsync(academicYearId, page, pageSize, search, filters);
	}

	[HttpPost("generate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task GenerateFeeStructuresAsync([FromQuery] Guid academicYearId)
	{
		await feeStructureMetadataAppService.GenerateFeeStructuresAsync(academicYearId);
	}
}
