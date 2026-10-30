using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Dashboard;

public record DashboardCollectionReadResult(List<FeePaymentReceipt> Receipts, string? UnavailableReason);

[RemoteService(false)]
public class DashboardCollectionsFocusReader(FinanceScopeAccessService scope, IRepository<Student, Guid> students,
	StudentScopeAccessService studentScope, IRepository<FeePaymentReceipt, Guid> receipts,
	IRepository<FeeRecordMetadata, Guid> transactions) : ApplicationService
{
	public async Task FillAsync(DashboardFocusDto focus, DashboardSummaryDto summary, Guid year)
	{
		var from = DateOnly.ParseExact(summary.DateRange.From!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
		var to = DateOnly.ParseExact(summary.DateRange.To!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
		var evidence = await ReadConfirmedAsync(year, from, to);
		var rows = evidence.Receipts;
		focus.PeriodLabel = $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";
		if (evidence.UnavailableReason != null) { focus.Message = evidence.UnavailableReason; return; }
		focus.Status = rows.Count == 0 ? "empty" : "ready";
		focus.Description = "Confirmed final fee receipts, excluding reversed transactions, superseded documents and pending verification. Charges are excluded.";
		focus.Series = [new("confirmed", "Confirmed fee collection", rows.GroupBy(x => x.ReceiptDate).OrderBy(x => x.Key)
			.Select(g => new DashboardFocusPointDto(g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Key.ToString("dd MMM", CultureInfo.InvariantCulture), g.Sum(x => x.FeePaidAmount))).ToList())];
		focus.Metrics = [new("collected", "Confirmed fee amount", rows.Sum(x => x.FeePaidAmount), "INR"), new("receipts", "Final fee receipts", rows.Count, "count")];
	}

	public async Task<DashboardCollectionReadResult> ReadConfirmedAsync(Guid year, DateOnly from, DateOnly to)
	{
		await scope.EnsureCollectionYearAccessibleAsync(year);
		var studentIds = (await studentScope.ApplyCurrentUserScopeAsync(await students.GetQueryableAsync())).Select(x => x.Id);
		var accountIds = await scope.GetScopedFeeAccountIdsQueryAsync();
		var validTransactions = (await transactions.GetQueryableAsync()).Where(DashboardCollectionEvidence.ConfirmedTransaction)
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == year);
		var query = (await receipts.GetQueryableAsync()).Where(DashboardCollectionEvidence.ConfirmedReceipt).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == year && x.ReceiptDate >= from && x.ReceiptDate <= to &&
			validTransactions.Any(t => t.Id == x.FeeCollectionTransactionId && t.StudentId == x.StudentId && t.FinanceFeeAccountId == x.FinanceFeeAccountId) &&
			(x.FinanceFeeAccountId.HasValue ? accountIds.Contains(x.FinanceFeeAccountId.Value) : x.StudentId.HasValue && studentIds.Contains(x.StudentId.Value)));
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.ReceiptDate).ThenBy(x => x.Id).Take(2001));
		if (rows.Count > 2000) return new([], "More than 2,000 receipts match. Select a shorter date range or open Finance Reports.");
		if (rows.Any(x => x.ReceiptScope == FeeReceiptScope.Combined && !x.ExtraProperties.ContainsKey(nameof(FeePaymentReceipt.FeePaidAmount))))
			return new([], "Some older combined receipts have no recorded fee breakdown. Review them in Finance.");
		return new(rows, null);
	}
}
