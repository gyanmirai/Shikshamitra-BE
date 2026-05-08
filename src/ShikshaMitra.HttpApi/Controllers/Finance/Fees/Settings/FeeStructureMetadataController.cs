using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("Finance")]
[Route("api/fee-structures-metadata")]
[Authorize]
public class FeeStructureMetadataController(
	IFeeStructureMetadataAppService feeStructureMetadataAppService
) : AbpControllerBase
{
	/// <summary>
	/// Get Fee Structures list
	/// </summary>
	[HttpGet]
	public async Task<FeeStructureMetadataListResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string search = null)
	{
		return await feeStructureMetadataAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	// create API to generate fee structures for all the mediums in given academic year based on the default fee structure of the school
	/// <summary>
	/// Generate Fee Structures for all Mediums in a Given Academic Year
	/// </summary>
	[HttpPost("generate")]
	public async Task GenerateFeeStructuresAsync([FromQuery] Guid academicYearId)
	{
		await feeStructureMetadataAppService.GenerateFeeStructuresAsync(academicYearId);
	}
}
