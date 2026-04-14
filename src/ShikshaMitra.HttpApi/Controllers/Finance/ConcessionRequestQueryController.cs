using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Controllers.Finance;

/// <summary>
/// Read-only projections for student-centric and per-request document views.
/// </summary>
[Authorize]
[Route("api/concession-requests")]
public class ConcessionRequestQueryController : AbpController
{
	private readonly IConcessionRequestQueryAppService _appService;

	public ConcessionRequestQueryController(IConcessionRequestQueryAppService appService)
	{
		_appService = appService;
	}

	/// <summary>
	/// Returns a paginated list of concession requests for a specific student,
	/// together with the student's basic profile and unfiltered application statistics
	/// (total / pending / approved / rejected counts across all statuses).
	/// </summary>
	[HttpGet("{studentId:guid}")]
	[ProducesResponseType(typeof(StudentConcessionRequestsDto), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<StudentConcessionRequestsDto> GetStudentConcessionRequestsAsync(
		Guid studentId,
		[FromQuery] GetStudentConcessionRequestsInput input)
		=> _appService.GetStudentConcessionRequestsAsync(studentId, input);

	/// <summary>
	/// Returns a paginated flat list of all document slots for the specified concession
	/// request, each merged with its upload state (documentName, url, status, uploadedAt).
	/// Optionally filtered by upload status: 0 = PENDING, 1 = UPLOADED.
	/// Slots that have never been submitted return url=null and status=0.
	/// </summary>
	[HttpGet("{studentConcessionTypeRequestId:guid}/documents")]
	[ProducesResponseType(typeof(ConcessionDocumentsDto), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<ConcessionDocumentsDto> GetConcessionDocumentsAsync(
		Guid studentConcessionTypeRequestId,
		[FromQuery] GetConcessionDocumentsInput input)
		=> _appService.GetConcessionDocumentsAsync(studentConcessionTypeRequestId, input);
}
