using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Finance.Mobile;

public partial class MobileFinanceReadService
{
	[Authorize(Permissions.View)]
	public async Task<MobileFinanceList<MobileFinanceTransaction>> TransactionsAsync(Guid studentId, Guid academicYearId,
		int skipCount = 0, int maxResultCount = 20, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId, academicYearId, cancellationToken: cancellationToken);
		ValidateOffset(skipCount, maxResultCount);
		if (skipCount > int.MaxValue - maxResultCount) throw new BusinessException("Finance:SelfService:InvalidRequest");
		var take = skipCount + maxResultCount;
		var tenantId = CurrentTenant.Id;
		var paymentQuery = (await transactions.GetQueryableAsync()).Where(x => x.TenantId == tenantId
			&& x.StudentId == studentId && x.AcademicYearId == academicYearId && x.TotalPaidFee > 0);
		var total = await AsyncExecuter.CountAsync(paymentQuery, cancellationToken);
		var paymentRows = await AsyncExecuter.ToListAsync(paymentQuery.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Take(take), cancellationToken);
		var paymentDtos = await PaymentDtosAsync(paymentRows, studentId, academicYearId, cancellationToken);
		var dates = paymentRows.ToDictionary(x => x.Id, x => Utc(x.CreationTime));
		var candidates = paymentDtos.Select(x => new MobileFinanceTransaction($"payment:{x.PaymentId:D}", "payment", x.Date,
			dates[x.PaymentId], x.Amount, "in", x.Status, x.PaymentMethods, x.Receipts, x.PaymentId)).ToList();
		if (await access.IsGrantedAsync(Permissions.ViewRefunds))
			total += await AddRefundTransactionsAsync(candidates, studentId, academicYearId, take, cancellationToken);
		return new(candidates.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.TransactionId, StringComparer.Ordinal)
			.Skip(skipCount).Take(maxResultCount).ToList(), total)
		{
			PaymentSummary = await PaymentSummaryAsync(studentId, academicYearId, cancellationToken)
		};
	}

	private async Task<int> AddRefundTransactionsAsync(List<MobileFinanceTransaction> result, Guid studentId, Guid yearId, int take, CancellationToken ct)
	{
		var tenantId = CurrentTenant.Id;
		var requests = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundRequest, Guid>>().GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == yearId);
		var requestIds = requests.Select(x => x.Id);
		var entries = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && requestIds.Contains(x.RefundRequestId));
		var settlements = requests.SelectMany(x => x.Settlements);
		var validEntries = entries.Where(x => x.Amount > 0 && x.PostedAt != default);
		var count = await AsyncExecuter.CountAsync(validEntries, ct);
		// Bound each source before merging. The local tie keys match the ordinal namespaced wire IDs.
		var payouts = await AsyncExecuter.ToListAsync(validEntries.Where(x => !x.IsCorrection)
			.OrderByDescending(x => x.PostedAt).ThenBy(x => x.SettlementId).Take(take)
			.Select(x => new { x.SettlementId, x.Amount, x.Method, x.PostedAt,
				SettlementDate = settlements.Where(s => s.Id == x.SettlementId && s.SettlementDate != default).Select(s => (DateTimeOffset?)s.SettlementDate).FirstOrDefault(),
				Status = settlements.Where(s => s.Id == x.SettlementId).Select(s => (FeeRefundSettlementStatus?)s.Status).FirstOrDefault() }), ct);
		foreach (var x in payouts)
			result.Add(RefundTransaction($"refund:{x.SettlementId:D}", "refund", x.SettlementDate ?? x.PostedAt, x.PostedAt,
				x.Amount, "out", (x.Status ?? FeeRefundSettlementStatus.Processed).ToString(), x.Method));
		var returns = await AsyncExecuter.ToListAsync(validEntries.Where(x => x.IsCorrection)
			.OrderByDescending(x => x.PostedAt).ThenBy(x => x.Id).Take(take)
			.Select(x => new { x.Id, x.Amount, x.Method, x.PostedAt }), ct);
		foreach (var x in returns)
			result.Add(RefundTransaction($"refundReturn:{x.Id:D}", "refundReturn", x.PostedAt, x.PostedAt, x.Amount, "in", "Reversed", x.Method));
		// A dated successful legacy settlement is cash evidence; request approval and aggregate amounts are not.
		// A legacy reversal has no independent immutable posting/date, so never invent a second incoming event.
		var legacy = settlements.Where(x => (x.Status == FeeRefundSettlementStatus.Processed || x.Status == FeeRefundSettlementStatus.Reversed)
			&& x.Amount > 0 && x.SettlementDate != default && x.CompletedAt.HasValue && x.CompletedAt.Value != default
			&& !entries.Any(e => !e.IsCorrection && e.SettlementId == x.Id));
		count += await AsyncExecuter.CountAsync(legacy, ct);
		var older = await AsyncExecuter.ToListAsync(legacy.OrderByDescending(x => x.CompletedAt).ThenBy(x => x.Id).Take(take)
			.Select(x => new { x.Id, x.Amount, x.Method, x.SettlementDate, x.CompletedAt, x.Status }), ct);
		foreach (var x in older)
			result.Add(RefundTransaction($"refund:{x.Id:D}", "refund", x.SettlementDate, x.CompletedAt!.Value, x.Amount, "out", x.Status.ToString(), x.Method));
		return count;
	}

	private static MobileFinanceTransaction RefundTransaction(string id, string kind, DateTimeOffset date, DateTimeOffset at,
		decimal amount, string direction, string status, string method) =>
		new(id, kind, DateOnly.FromDateTime(date.Date), at.ToUniversalTime(), MobileFinanceProjection.Money(amount), direction, status, [method], []);
}
