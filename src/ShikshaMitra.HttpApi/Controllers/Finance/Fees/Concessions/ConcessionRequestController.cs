using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.ConcessionRequest;

/// <summary>
/// Core lifecycle operations for concession requests:
/// create, read, update, submit, and delete Draft requests.
///
/// <list type="bullet">
///   <item>Wizard steps (3–6) → <see cref="ConcessionRequestWizardController"/></item>
///   <item>Approval workflow → <see cref="ConcessionRequestApprovalController"/></item>
///   <item>Student / document queries → <see cref="ConcessionRequestQueryController"/></item>
/// </list>
/// </summary>
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

	/// <summary>Creates a new ConcessionRequest batch in Draft state (Steps 1 + 2).</summary>
	[HttpPost]
	[ProducesResponseType(204)]
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

	/// <summary>Deletes a Draft ConcessionRequest.</summary>
	[HttpDelete("{id:guid}")]
	[ProducesResponseType(204)]
	public virtual Task DeleteAsync(Guid id)
		=> _appService.DeleteAsync(id);
}
