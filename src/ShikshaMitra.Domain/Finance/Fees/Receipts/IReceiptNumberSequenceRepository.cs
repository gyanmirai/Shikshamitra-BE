using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Repository for <see cref="ReceiptNumberSequence"/>.
/// The critical operation is <see cref="IncrementAndGetAsync"/>, which atomically
/// increments the next value and returns it — guaranteeing unique receipt numbers
/// even under concurrent collection requests.
/// </summary>
public interface IReceiptNumberSequenceRepository : IRepository<ReceiptNumberSequence, Guid>
{
	/// <summary>
	/// Atomically increments the sequence counter for the given context and returns
	/// the value <b>before</b> the increment (1-based, ready to embed in a receipt number).
	/// <para>
	/// Creates the sequence row if it does not yet exist for the
	/// (TenantId, Year, Kind) combination.
	/// This operation is safe under concurrent parallel calls.
	/// </para>
	/// </summary>
	/// <param name="year">Calendar year (e.g. 2026).</param>
	/// <param name="kind">
	/// Receipt kind. <c>null</c> selects the legacy <c>REC-</c> sequence.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The 1-based sequence number to embed in the receipt number.</returns>
	Task<long> IncrementAndGetAsync(
		int year,
		FeeReceiptKind? kind,
		CancellationToken cancellationToken = default);
}
