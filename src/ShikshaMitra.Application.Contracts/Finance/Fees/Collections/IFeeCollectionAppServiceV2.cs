using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// V2 contract for the fee-collection summary screen.
/// <para>
/// Key improvements over V1:
/// <list type="bullet">
///   <item>Server-side filtering, sorting, and pagination — no in-memory hydration.</item>
///   <item>Support for <c>StandardId</c> and <c>DivisionId</c> filters.</item>
///   <item>Status filter pushed to the database query.</item>
///   <item>Export endpoint (CSV / Excel).</item>
///   <item>ABP standard <see cref="PagedResultDto{T}"/> response shape.</item>
/// </list>
/// </para>
/// </summary>
public interface IFeeCollectionAppServiceV2 : IApplicationService
{
	/// <summary>
	/// Returns a server-side filtered, sorted, and paginated summary of
	/// student-wise fee collection for the given academic context.
	/// </summary>
	/// <param name="input">Filter, sort, and pagination parameters.</param>
	/// <param name="cancellationToken">Propagates cancellation from the HTTP layer.</param>
	Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
		GetFeeCollectionSummaryInputV2 input,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Exports the full (unpaginated) fee-collection summary matching the
	/// supplied filters as a CSV or Excel file.
	/// </summary>
	/// <param name="input">Filter and format parameters.</param>
	/// <param name="cancellationToken">Propagates cancellation from the HTTP layer.</param>
	/// <returns>Raw file bytes ready to stream as a file response.</returns>
	Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default);
}
