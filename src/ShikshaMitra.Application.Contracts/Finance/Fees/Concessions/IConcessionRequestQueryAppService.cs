using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions;

/// <summary>
/// Read-only projections for student-centric and per-request document views.
/// </summary>
public interface IConcessionRequestQueryAppService : IApplicationService
{
	/// <summary>
	/// Returns a paginated list of concession requests for a specific student,
	/// together with the student's basic profile and unfiltered application statistics
	/// (total / pending / approved / rejected counts across all statuses).
	/// </summary>
	Task<StudentConcessionRequestsDto> GetStudentConcessionRequestsAsync(
		Guid studentId,
		GetStudentConcessionRequestsInput input);

	/// <summary>
	/// Returns a paginated flat list of all document slots for the specified concession
	/// request, each merged with its upload state (documentName, url, status, uploadedAt).
	/// Optionally filtered by upload status: 0 = PENDING, 1 = UPLOADED.
	/// Slots never submitted return url=null and status=0.
	/// </summary>
	Task<ConcessionDocumentsDto> GetConcessionDocumentsAsync(
		Guid studentConcessionTypeRequestId,
		GetConcessionDocumentsInput input);
}
