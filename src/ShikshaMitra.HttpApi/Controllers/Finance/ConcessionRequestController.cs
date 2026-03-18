using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Controllers.Finance;

[Authorize]
[Route("api/app/finance/concession-requests")]
public class ConcessionRequestController : AbpController, IConcessionRequestAppService
{
    private readonly IConcessionRequestAppService _appService;

    public ConcessionRequestController(IConcessionRequestAppService appService)
    {
        _appService = appService;
    }

    /// <summary>Gets a single ConcessionRequest with its fee-head mappings.</summary>
    [HttpGet("{id:guid}")]
    public virtual Task<ConcessionRequestDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>Returns a paged list of ConcessionRequests.</summary>
    [HttpGet]
    public virtual Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(
        [FromQuery] GetConcessionRequestListRequestDto input)
        => _appService.GetListAsync(input);

    /// <summary>Creates a new ConcessionRequest in Draft state.</summary>
    [HttpPost]
    public virtual Task<ConcessionRequestDto> CreateAsync([FromBody] CreateConcessionRequestDto input)
        => _appService.CreateAsync(input);

    /// <summary>Updates a Draft ConcessionRequest.</summary>
    [HttpPut("{id:guid}")]
    public virtual Task<ConcessionRequestDto> UpdateAsync(Guid id, [FromBody] UpdateConcessionRequestDto input)
        => _appService.UpdateAsync(id, input);

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
