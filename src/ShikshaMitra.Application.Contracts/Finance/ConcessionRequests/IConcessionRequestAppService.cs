using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

public interface IConcessionRequestAppService : IApplicationService
{
    /// <summary>Gets a single ConcessionRequest with its fee-head mappings.</summary>
    Task<ConcessionRequestDto> GetAsync(Guid id);

    /// <summary>Returns a paged list of ConcessionRequests.</summary>
    Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input);

    /// <summary>Creates a new ConcessionRequest in Draft state.</summary>
    Task<ConcessionRequestDto> CreateAsync(CreateConcessionRequestDto input);

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

