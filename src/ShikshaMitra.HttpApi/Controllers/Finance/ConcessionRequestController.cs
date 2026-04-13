using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Controllers.Finance;

[Authorize]
[Route("api/concession-request")]
public class ConcessionRequestController : AbpController
{
    private readonly IConcessionRequestAppService _appService;

    public ConcessionRequestController(IConcessionRequestAppService appService)
    {
        _appService = appService;
    }

    /// <summary>
    /// Gets a concession request by ID (Step 1 + Step 2 data).
    /// Returns the anchor request enriched with the student's enrollment context
    /// (board, medium, standard, division) and all concession-type selections
    /// that share the same lifecycle status.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public virtual Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>Returns a paged list of ConcessionRequests.</summary>
    [HttpGet]
    public virtual Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(
        [FromQuery] GetConcessionRequestListRequestDto input)
        => _appService.GetListAsync(input);

    /// <summary>Creates a new ConcessionRequest in Draft state.</summary>
    [HttpPost]
    public async Task CreateAsync([FromBody] CreateConcessionRequestDto input)
        => await _appService.CreateAsync(input);

    /// <summary>
    /// Replaces the concession-type selection for a student's Pending request batch.
    /// The <paramref name="id"/> must identify a Pending request owned by the student
    /// in the body; all Pending requests for that student are then replaced with the
    /// supplied <c>concessionTypeIds</c>.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ConcessionRequestDto> UpdateAsync(Guid id, [FromBody] UpdateConcessionRequestDto input)
        => await _appService.UpdateAsync(id, input);

    /// <summary>Submits a Draft request for approval (Draft → Pending).</summary>
    [HttpPost("{id:guid}/submit")]
    public virtual Task<ConcessionRequestDto> SubmitAsync(Guid id)
        => _appService.SubmitAsync(id);

    /// <summary>Approves a Pending request (Pending → Approved).</summary>
    [HttpPost("{id:guid}/approve")]
    public virtual Task<ConcessionRequestDto> ApproveAsync(Guid id)
        => _appService.ApproveAsync(id);

    /// <summary>Rejects a Pending request (Pending → Rejected).</summary>
    [HttpPost("{id:guid}/reject")]
    public virtual Task<ConcessionRequestDto> RejectAsync(Guid id, [FromBody] RejectConcessionRequestDto input)
        => _appService.RejectAsync(id, input);

    /// <summary>Deletes a Draft ConcessionRequest.</summary>
    [HttpDelete("{id:guid}")]
    public virtual Task DeleteAsync(Guid id)
        => _appService.DeleteAsync(id);
}
