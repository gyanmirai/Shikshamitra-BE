using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Setting;

[RemoteService]
[Area("Finance")]
[Route("api/fee-structures")]
[Authorize]
public class FeeStructureController : AbpController
{
	private readonly IFeeStructureAppService _feeStructureAppService;

	public FeeStructureController(IFeeStructureAppService feeStructureAppService)
	{
		_feeStructureAppService = feeStructureAppService;
	}

	/// <summary>
	/// Create a new Fee Structure
	/// </summary>
	[HttpPost]
	public Task<FeeStructureDetailsResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeStructureCreateRequest input)
	{
		return _feeStructureAppService.CreateAsync(academicYearId, input);
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
		return _feeStructureAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	/// <summary>
	/// Get Fee Structure Details
	/// </summary>
	[HttpGet("{feeStructureId}")]
	public Task<FeeStructureDetailsResponse> GetAsync(Guid feeStructureId)
	{
		return _feeStructureAppService.GetAsync(feeStructureId);
	}

	/// <summary>
	/// Update Fee Structure Status
	/// </summary>
	[HttpPatch("{feeStructureId}/status")]
	public Task<MessageResponse> UpdateStatusAsync(Guid feeStructureId, [FromBody] FeeStructureStatusUpdateRequest input)
	{
		return _feeStructureAppService.UpdateStatusAsync(feeStructureId, input);
	}

	/// <summary>
	/// Update Fee Structure
	/// </summary>
	[HttpPut("{feeStructureId}")]
	public Task<MessageResponse> UpdateAsync(Guid feeStructureId, [FromBody] FeeStructureUpdateRequest input)
	{
		return _feeStructureAppService.UpdateAsync(feeStructureId, input);
	}

	/// <summary>
	/// Save Standard Fee Structures (Bulk)
	/// </summary>
	[HttpPut("{feeStructureId}/standard-fee-structures")]
	public Task<MessageResponse> SaveStandardFeeStructuresAsync(Guid feeStructureId, [FromBody] List<StandardFeeStructureSaveRequest> input)
	{
		return _feeStructureAppService.SaveStandardFeeStructuresAsync(feeStructureId, input);
	}

	/// <summary>
	/// Get Fee Structure for a Particular Standard
	/// </summary>
	[HttpGet("{feeStructureId}/standards/{standardId}")]
	public Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid feeStructureId, Guid standardId)
	{
		return _feeStructureAppService.GetStandardFeeStructureAsync(feeStructureId, standardId);
	}
}
