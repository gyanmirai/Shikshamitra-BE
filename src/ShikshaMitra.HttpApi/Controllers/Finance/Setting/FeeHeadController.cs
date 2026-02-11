using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Setting;

[RemoteService]
[Area("Finance")]
[Route("api/fee-heads")]
[Authorize]
public class FeeHeadController : AbpController
{
    private readonly IFeeHeadAppService _feeHeadAppService;

    public FeeHeadController(IFeeHeadAppService feeHeadAppService)
    {
        _feeHeadAppService = feeHeadAppService;
    }

    /// <summary>
    /// Create a fee head under a fee category for a specific academic year
    /// </summary>
    /// <param name="academicYearId">Academic year identifier (UUID)</param>
    /// <param name="input">Fee head creation details</param>
    /// <returns>Success message</returns>
    [HttpPost]
    public Task<MessageResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeHeadCreateRequest input)
    {
        return _feeHeadAppService.CreateAsync(academicYearId, input);
    }

    /// <summary>
    /// Fetch fee heads by academic year and fee category
    /// </summary>
    /// <param name="academicYearId">Academic year identifier</param>
    /// <param name="feeCategoryId">Filter by fee category</param>
    /// <param name="search">Search by fee head name</param>
    /// <param name="page">Page number (1-indexed)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of fee heads</returns>
    [HttpGet]
    public Task<FeeHeadListResponse> GetListAsync(
        [FromQuery] Guid academicYearId, 
        [FromQuery] Guid? feeCategoryId = null, 
        [FromQuery] string search = null, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        return _feeHeadAppService.GetListAsync(academicYearId, feeCategoryId, search, page, pageSize);
    }

    /// <summary>
    /// Update fee head name, category, or description
    /// </summary>
    /// <param name="feeHeadId">Fee head identifier (UUID)</param>
    /// <param name="input">Fee head update details</param>
    /// <returns>Success message</returns>
    [HttpPut("{feeHeadId}")]
    public Task<MessageResponse> UpdateAsync(Guid feeHeadId, [FromBody] FeeHeadUpdateRequest input)
    {
        return _feeHeadAppService.UpdateAsync(feeHeadId, input);
    }

    /// <summary>
    /// Soft delete fee head
    /// </summary>
    /// <param name="feeHeadId">Fee head identifier (UUID)</param>
    /// <returns>Success message</returns>
    [HttpDelete("{feeHeadId}")]
    public Task<MessageResponse> DeleteAsync(Guid feeHeadId)
    {
        return _feeHeadAppService.DeleteAsync(feeHeadId);
    }

    /// <summary>
    /// Enable or disable a fee head
    /// </summary>
    /// <param name="feeHeadId">Fee head identifier (UUID)</param>
    /// <param name="input">Status update request</param>
    /// <returns>Success message</returns>
    [HttpPatch("{feeHeadId}/status")]
    public Task<MessageResponse> UpdateStatusAsync(Guid feeHeadId, [FromBody] FeeHeadStatusUpdateRequest input)
    {
        return _feeHeadAppService.UpdateStatusAsync(feeHeadId, input);
    }
}
