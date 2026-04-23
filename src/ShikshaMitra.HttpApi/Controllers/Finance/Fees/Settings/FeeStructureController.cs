using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings.FeeCategory.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructure;
using ShikshaMitra.Finance.Fees.Settings.FeeStructure.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("Finance")]
[Route("api/fee-structures")]
[Authorize]
public class FeeStructureController(IFeeStructureAppService feeStructureAppService) : AbpController
{
	/// <summary>
	/// Create a new Fee Structure
	/// </summary>
	[HttpPost]
	public Task<FeeStructureDetailsResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeStructureCreateRequest input)
	{
		return feeStructureAppService.CreateAsync(academicYearId, input);
	}

	/// <summary>
	/// Get Fee Structures list
	/// </summary>
	[HttpGet]
	public Task<FeeStructureListResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string search = null)
	{
		return feeStructureAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	/// <summary>
	/// Update Fee Structure Status
	/// </summary>
	[HttpPatch("{feeStructureId}/status")]
	public async Task UpdateStatusAsync(Guid feeStructureId, [FromBody] FeeStructureStatusUpdateRequest input)
	{
		await feeStructureAppService.UpdateStatusAsync(feeStructureId, input);
	}

	/// <summary>
	/// Update Fee Structure
	/// </summary>
	[HttpPut("{feeStructureId}")]
	public async Task<MessageResponse> UpdateAsync(Guid feeStructureId, [FromBody] FeeStructureUpdateRequest input)
	{
		return await feeStructureAppService.UpdateAsync(feeStructureId, input);
	}

	/// <summary>
	/// Save Standard Fee Structures (Bulk)
	/// </summary>
	[HttpPut("{feeStructureId}/standard-fee-structures")]
	public async Task SaveStandardFeeStructuresAsync(Guid feeStructureId, [FromBody] StandardFeeStructureSaveRequest input)
	{
		await feeStructureAppService.SaveStandardFeeStructuresAsync(feeStructureId, input);
	}

	/// <summary>
	/// Get Fee Structure for a Particular Standard
	/// </summary>
	[HttpGet("standards/{standardId}")]
	public async Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid standardId)
	{
		return await feeStructureAppService.GetStandardFeeStructureAsync(standardId);
	}
}
