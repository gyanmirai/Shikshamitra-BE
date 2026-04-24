using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions;

/// <summary>
/// Core lifecycle operations for concession requests:
/// create, read, update, submit, and delete Draft requests.
///
/// <list type="bullet">
///   <item>Wizard steps (3–6) → <see cref="IConcessionRequestWizardAppService"/></item>
///   <item>Approval workflow → <see cref="IConcessionRequestApprovalAppService"/></item>
///   <item>Student / document queries → <see cref="IConcessionRequestQueryAppService"/></item>
/// </list>
/// </summary>
public interface IConcessionRequestAppService : IApplicationService
{
	/// <summary>
	/// Gets a single ConcessionRequest by its ID (Step 1 + Step 2 data).
	/// Returns the anchor request enriched with the student's enrollment context
	/// and all sibling requests that share the same lifecycle status.
	/// </summary>
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id);

	/// <summary>Returns a paged list of ConcessionRequests.</summary>
	Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input);

	/// <summary>Creates a new ConcessionRequest batch in Draft state (Steps 1 + 2).</summary>
	Task CreateAsync(CreateConcessionRequestDto input);

	/// <summary>
	/// Replaces the concession-type selection for a student's Pending request batch
	/// (batch replace: removes deselected types, adds newly selected ones).
	/// </summary>
	Task<ConcessionRequestDto> UpdateAsync(Guid id, UpdateConcessionRequestDto input);

	/// <summary>Submits a Draft request for approval (Draft → Pending).</summary>
	Task<ConcessionRequestDto> SubmitAsync(Guid id);

	/// <summary>Deletes a Draft ConcessionRequest.</summary>
	Task DeleteAsync(Guid id);
}

