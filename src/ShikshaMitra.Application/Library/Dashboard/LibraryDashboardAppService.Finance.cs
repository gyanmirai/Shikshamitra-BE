using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Dashboard.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Dashboard;

public partial class LibraryDashboardAppService
{
	private async Task<(LibraryChargeTotalsDto Compatibility, LibraryFinancePositionSummaryDto Finance, int CollectibleCount, int OverdueCount)>
		ReadDashboardChargesAsync(IQueryable<FinanceExternalPositionQueryRow> positions, DateOnly? schoolDay)
	{
		var sourceIds = positions.Select(position => position.SourceId);
		var assessments = await chargeRepository.GetQueryableAsync();
		var scoped = from charge in assessments
			join id in sourceIds on charge.Id equals id
			where charge.TenantId == CurrentTenant.Id &&
				(charge.PaymentStatus == ChargePaymentStatus.Pending || charge.PaymentStatus == ChargePaymentStatus.Paid ||
				 charge.PaymentStatus == ChargePaymentStatus.Waived)
			select charge;
		var declarations = await AsyncExecuter.ToListAsync(scoped.GroupBy(charge => charge.PaymentStatus)
			.Select(group => new DashboardDeclarationGroup
			{
				Status = group.Key, Count = group.Count(), Amount = group.Sum(charge => charge.Amount)
			}));
		var compatibility = new LibraryChargeTotalsDto();
		foreach (var declaration in declarations)
		{
			switch (declaration.Status)
			{
				case ChargePaymentStatus.Pending:
					compatibility.PendingCount = declaration.Count; compatibility.PendingAmount = declaration.Amount; break;
				case ChargePaymentStatus.Paid:
					compatibility.PaidCount = declaration.Count; compatibility.PaidAmount = declaration.Amount; break;
				case ChargePaymentStatus.Waived:
					compatibility.WaivedCount = declaration.Count; compatibility.WaivedAmount = declaration.Amount; break;
			}
		}
		var evidence = await AsyncExecuter.ToListAsync(positions.GroupBy(position => position.Evidence)
			.Select(group => new DashboardEvidenceGroup { Evidence = group.Key, Count = group.Count() }));
		var finance = new LibraryFinancePositionSummaryDto
		{
			VerifiedCount = evidence.SingleOrDefault(group => group.Evidence == FinanceExternalPositionEvidence.Verified)?.Count ?? 0,
			NeedsReviewCount = evidence.SingleOrDefault(group => group.Evidence == FinanceExternalPositionEvidence.NeedsReview)?.Count ?? 0,
			UnpostedCount = evidence.SingleOrDefault(group => group.Evidence == FinanceExternalPositionEvidence.Unposted)?.Count ?? 0
		};
		var verified = from position in positions
			join charge in assessments on position.SourceId equals charge.Id
			where charge.TenantId == CurrentTenant.Id && position.Evidence == FinanceExternalPositionEvidence.Verified
			select new { position, charge };
		var currencies = await AsyncExecuter.ToListAsync(verified
			.GroupBy(row => row.position.Currency).Select(group => new DashboardFinanceGroup
			{
				Currency = group.Key, Original = group.Sum(row => row.position.Original!.Value),
				Accepted = group.Sum(row => row.position.Accepted!.Value), Settled = group.Sum(row => row.position.Settled!.Value),
				Pending = group.Sum(row => row.position.Pending!.Value), NetWaived = group.Sum(row => row.position.NetWaived!.Value),
				Outstanding = group.Sum(row => row.position.FinancialOutstanding!.Value),
				Collectible = group.Sum(row => row.position.Collectible!.Value), Refunded = group.Sum(row => row.position.Refunded!.Value),
				CollectibleCount = group.Count(row => row.position.Collectible > 0m),
				OverdueCount = group.Count(row => row.position.Collectible > 0m && schoolDay.HasValue &&
					row.charge.SchoolDueDate.HasValue && row.charge.SchoolDueDate < schoolDay &&
					(row.charge.DueDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
					 row.charge.DueDateProvenance == LibraryCivilDateProvenance.ReviewedLegacy))
			}));
		finance.CurrencyTotals = currencies.OrderBy(group => group.Currency, StringComparer.Ordinal).Select(group => new LibraryFinanceCurrencyTotalsDto
		{
			Currency = group.Currency ?? throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource),
			OriginalAmountText = FinanceAmountText(group.Original), AcceptedAmountText = FinanceAmountText(group.Accepted),
			SettledAmountText = FinanceAmountText(group.Settled), PendingVerificationAmountText = FinanceAmountText(group.Pending),
			NetWaivedAmountText = FinanceAmountText(group.NetWaived), FinancialOutstandingAmountText = FinanceAmountText(group.Outstanding),
			CollectibleAmountText = FinanceAmountText(group.Collectible), RefundedAmountText = FinanceAmountText(group.Refunded)
		}).ToList();
		return (compatibility, finance, currencies.Sum(group => group.CollectibleCount), currencies.Sum(group => group.OverdueCount));
	}

	private static string FinanceAmountText(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

	private static void AppendFinancialAlerts(List<LibraryDashboardAlertDto> alerts,
		LibraryFinancePositionSummaryDto finance, int collectibleCount, int overdueCount)
	{
		if (collectibleCount > 0)
			alerts.Add(new()
			{
				Type = "pendingCharges", Severity = "warning", Title = "Collectible Library charges",
				Message = $"{collectibleCount} Library assessments have a collectible Finance balance.",
				Count = collectibleCount, Link = "/library/charges-management"
			});
		if (overdueCount > 0)
			alerts.Add(new()
			{
				Type = "overdueCharges", Severity = "error", Title = "Overdue collectible Library charges",
				Message = $"{overdueCount} collectible assessments are past their confirmed school due date.",
				Count = overdueCount, Link = "/library/charges-management"
			});
		if (finance.UnpostedCount > 0)
			alerts.Add(new()
			{
				Type = "unpostedCharges", Severity = "warning", Title = "Review unposted Library assessments",
				Message = $"{finance.UnpostedCount} assessment records have no accepted Finance posting. Review their recorded context and declarations.",
				Count = finance.UnpostedCount, Link = "/library/charges-management"
			});
		if (finance.NeedsReviewCount > 0)
			alerts.Add(new()
			{
				Type = "reviewCharges", Severity = "warning", Title = "Review Library financial evidence",
				Message = $"{finance.NeedsReviewCount} assessment records need source or settlement evidence review before financial action.",
				Count = finance.NeedsReviewCount, Link = "/library/charges-management"
			});
	}

	private sealed class DashboardDeclarationGroup
	{
		public ChargePaymentStatus Status { get; init; }
		public int Count { get; init; }
		public decimal Amount { get; init; }
	}
	private sealed class DashboardEvidenceGroup
	{
		public FinanceExternalPositionEvidence Evidence { get; init; }
		public int Count { get; init; }
	}
	private sealed class DashboardFinanceGroup
	{
		public string? Currency { get; init; }
		public decimal Original { get; init; }
		public decimal Accepted { get; init; }
		public decimal Settled { get; init; }
		public decimal Pending { get; init; }
		public decimal NetWaived { get; init; }
		public decimal Outstanding { get; init; }
		public decimal Collectible { get; init; }
		public decimal Refunded { get; init; }
		public int CollectibleCount { get; init; }
		public int OverdueCount { get; init; }
	}
}
