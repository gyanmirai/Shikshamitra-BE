using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// V2 contract for all fee-collection operations.
/// <para>
/// Key improvements over V1:
/// <list type="bullet">
///   <item>Server-side filtering, sorting, and pagination — no in-memory hydration.</item>
///   <item>Support for <c>StandardId</c> and <c>DivisionId</c> filters on summary.</item>
///   <item>Status filter pushed to the database query.</item>
///   <item>Export endpoint (CSV / Excel).</item>
///   <item>ABP standard <see cref="PagedResultDto{T}"/> response shape.</item>
///   <item>Month-wise DueFee ledger for the collect-fee dialog.</item>
/// </list>
/// </para>
/// </summary>
public interface IFeeCollectionAppServiceV2 : IApplicationService
{
	/// <summary>
	/// Returns a server-side filtered, sorted, and paginated summary of
	/// student-wise fee collection for the given academic context.
	/// </summary>
	Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
		GetFeeCollectionSummaryInputV2 input,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Exports the full (unpaginated) fee-collection summary matching the
	/// supplied filters as a CSV or Excel file.
	/// </summary>
	/// <returns>Raw file bytes ready to stream as a file response.</returns>
	Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Records a fee payment for a student with support for multiple payment methods,
	/// fee heads, and optional per-month DueFee allocations.
	/// </summary>
	Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input);

	/// <summary>
	/// Returns the data needed to populate the collect-fee dialog for a student:
	/// student context, month-wise DueFee ledger, and approved charges.
	/// </summary>
	Task<FeeCollectFormDto> GetCollectFormAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns complete fee information for a student in an academic year,
	/// including fee-head breakdown, payment history, and applied concessions.
	/// </summary>
	Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
}
