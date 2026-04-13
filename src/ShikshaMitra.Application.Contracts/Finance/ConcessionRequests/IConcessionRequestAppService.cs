using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

public interface IConcessionRequestAppService : IApplicationService
{
    /// <summary>
    /// Gets a single ConcessionRequest by its ID.
    /// The response includes the student's enrollment context and all sibling
    /// requests that share the same lifecycle status as the anchor.
    /// </summary>
    Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id);

    /// <summary>Returns a paged list of ConcessionRequests.</summary>
    Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input);

    /// <summary>Creates a new ConcessionRequest in Draft state.</summary>
    Task CreateAsync(CreateConcessionRequestDto input);

    /// <summary>Updates a Draft ConcessionRequest (metadata + fee-head mappings).</summary>
    Task<ConcessionRequestDto> UpdateAsync(Guid id, UpdateConcessionRequestDto input);

    /// <summary>Submits a Draft request for approval (Draft → Pending).</summary>
    Task<ConcessionRequestDto> SubmitAsync(Guid id);

    /// <summary>Approves a Pending request (Pending → Approved).</summary>
    Task<ConcessionRequestDto> ApproveAsync(Guid id);

    /// <summary>Rejects a Pending request with a mandatory reason (Pending → Rejected).</summary>
    Task<ConcessionRequestDto> RejectAsync(Guid id, RejectConcessionRequestDto input);

    /// <summary>Deletes a Draft ConcessionRequest.</summary>
    Task DeleteAsync(Guid id);
}

