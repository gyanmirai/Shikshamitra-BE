using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("Finance")]
[Route("api/fee-categories")]
[Authorize]
public class FeeCategoryController : AbpController
{
    private readonly IFeeCategoryAppService _feeCategoryAppService;

    public FeeCategoryController(IFeeCategoryAppService feeCategoryAppService)
    {
        _feeCategoryAppService = feeCategoryAppService;
    }

    /// <summary>
    /// Create a fee category for a specific academic year.
    /// </summary>
    /// <param name="academicYearId">Academic year identifier (UUID).</param>
    /// <param name="input">Fee category creation details.</param>
    /// <returns>Success message.</returns>
    [HttpPost]
    public Task<MessageResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] FeeCategoryCreateRequest input)
    {
        return _feeCategoryAppService.CreateAsync(academicYearId, input);
    }

    /// <summary>
    /// Fetch fee categories for a specific academic year with pagination and search.
    /// </summary>
    /// <param name="academicYearId">Academic year identifier.</param>
    /// <param name="search">Search by fee category name.</param>
    /// <param name="page">Page number (0-indexed).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <returns>Paginated list of fee categories.</returns>
    [HttpGet]
    public Task<FeeCategoryListResponse> GetListAsync([FromQuery] Guid academicYearId, [FromQuery] string search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return _feeCategoryAppService.GetListAsync(academicYearId, search, page, pageSize);
    }

    /// <summary>
    /// Update fee category name or description.
    /// </summary>
    /// <param name="feeCategoryId">Fee category identifier (UUID).</param>
    /// <param name="input">Fee category update details.</param>
    /// <returns>Success message.</returns>
    [HttpPut("{feeCategoryId}")]
    public Task<MessageResponse> UpdateAsync(Guid feeCategoryId, [FromBody] FeeCategoryUpdateRequest input)
    {
        return _feeCategoryAppService.UpdateAsync(feeCategoryId, input);
    }

    /// <summary>
    /// Soft delete fee category (academic year scoped).
    /// </summary>
    /// <param name="feeCategoryId">Fee category identifier (UUID).</param>
    /// <returns>Success message.</returns>
    [HttpDelete("{feeCategoryId}")]
    public Task<MessageResponse> DeleteAsync(Guid feeCategoryId)
    {
        return _feeCategoryAppService.DeleteAsync(feeCategoryId);
    }
}
