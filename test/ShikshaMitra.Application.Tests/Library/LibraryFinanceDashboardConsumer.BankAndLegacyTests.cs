using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

// Uses the actual consumer fixture and shared Dashboard-only authority helpers.
public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Dashboard_Finance_Position_Should_Keep_Fully_Reserved_Bank_Money_Unsettled_With_Only_Dashboard_Authority()
	{
		var (context, first) = await CollectTypedBankInstrumentAsync();
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		first.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		var bankId = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BankAccount, Guid>>()
			.GetListAsync(row => row.TenantId == owner.TenantId)).ShouldHaveSingleItem().Id);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var reserved = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new()
			{
				PaymentMethod = PaymentMethodEnum.UPI, Amount = 60.50m, BankAccountId = bankId,
				Details = new() { ["upiTransactionId"] = "DASHBOARD-SECOND-RESERVED-UPI", ["upiId"] = "school@bank" }
			}]
		}));
		reserved.Receivable.PendingVerificationAmount.ShouldBe(100.75m);
		reserved.Receivable.SettledPaymentAmount.ShouldBe(0m);
		reserved.Receivable.FinancialOutstandingAmount.ShouldBe(100.75m);
		reserved.Receivable.OutstandingAmount.ShouldBe(0m);

		var unrelatedActions = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, unrelatedActions);
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(
				LibraryChargePositionReadPurpose.Dashboard, owner.YearId));
			var actual = await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query);
			actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
			actual.Accepted.ShouldBe(100.75m); actual.Settled.ShouldBe(0m); actual.Pending.ShouldBe(100.75m);
			actual.FinancialOutstanding.ShouldBe(100.75m); actual.Collectible.ShouldBe(0m);
		});
		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync(owner.YearId));
		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100.75m);
		overview.Charges.PaidCount.ShouldBe(0); overview.Charges.WaivedCount.ShouldBe(0);
		var json = JsonSerializer.SerializeToElement(overview, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		json.TryGetProperty("financePosition", out var summary).ShouldBeTrue();
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(1);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		var totals = summary.GetProperty("currencyTotals").EnumerateArray().ShouldHaveSingleItem();
		totals.GetProperty("currency").GetString().ShouldBe("INR");
		totals.GetProperty("originalAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("acceptedAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("settledAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("pendingVerificationAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("collectibleAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("netWaivedAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("refundedAmountText").GetString().ShouldBe("0.00");
		await WithUnitOfWorkAsync(async () =>
		{
			var headers = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetListAsync(row => row.TenantId == owner.TenantId);
			headers.Count.ShouldBe(2);
			headers.Sum(row => row.TotalPaidFee).ShouldBe(100.75m);
			headers.ShouldAllBe(row => row.Status == FeeCollectionStatus.InReconciliation);
			var receipts = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetListAsync(row => row.TenantId == owner.TenantId);
			receipts.Count.ShouldBe(2);
			receipts.ShouldAllBe(row => row.ReceiptKind == FeeReceiptKind.ProvisionalCharge &&
				row.VerificationStatus == FeeReceiptVerificationStatus.PendingVerification);
		});
	}

	[Fact]
	public async Task Dashboard_Finance_Position_Should_Retain_Unconfirmed_Legacy_Declarations_Without_Inventing_Currency_Or_Settlement()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var oldRawDate = new DateTime(2024, 6, 10);
		var oldRawDue = new DateTime(2024, 6, 24);
		var legacy = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var existing = await repository.GetAsync(owner.AssessmentId);
			existing.Amount.ShouldBe(100m);
			existing.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			existing.AcademicYearId.ShouldBeNull(); existing.Currency.ShouldBeNull(); existing.FinanceSourceLinkId.ShouldBeNull();
			var paid = new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, existing.ChargeCategoryId, null,
				20.25m, oldRawDate, oldRawDue, owner.TenantId);
			var waived = new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, existing.ChargeCategoryId, null,
				30.50m, oldRawDate, oldRawDue, owner.TenantId);
			// Historical declarations are imported directly; Update cannot declare
			// Paid/Waived and no Finance payment or waiver is manufactured here.
			typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(paid, ChargePaymentStatus.Paid);
			typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(waived, ChargePaymentStatus.Waived);
			await repository.InsertAsync(paid, true);
			await repository.InsertAsync(waived, true);
			return (PaidId: paid.Id, WaivedId: waived.Id, BaselineRawDate: existing.ChargeDate, BaselineRawDue: existing.DueDate);
		});
		var unrelatedActions = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, unrelatedActions);
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Dashboard));
			var actual = await GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query);
			actual.Count.ShouldBe(3);
			actual.ShouldAllBe(row => row.Evidence == FinanceExternalPositionEvidence.Unposted && row.Currency == null &&
				row.AcademicYearId == null && row.Settled == null && row.SourceLinkId == null);
		});
		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync());
		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100m);
		overview.Charges.PaidCount.ShouldBe(1); overview.Charges.PaidAmount.ShouldBe(20.25m);
		overview.Charges.WaivedCount.ShouldBe(1); overview.Charges.WaivedAmount.ShouldBe(30.50m);
		var json = JsonSerializer.SerializeToElement(overview, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		json.TryGetProperty("financePosition", out var summary).ShouldBeTrue();
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(0);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(3);
		summary.GetProperty("currencyTotals").GetArrayLength().ShouldBe(0,
			"No verified currency group exists; legacy declarations cannot become an INR group with fabricated zero settlement.");
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			foreach (var (id, amount, declaration) in new[] {
				(legacy.PaidId, 20.25m, ChargePaymentStatus.Paid), (legacy.WaivedId, 30.50m, ChargePaymentStatus.Waived) })
			{
				var stored = await repository.GetAsync(id);
				stored.Amount.ShouldBe(amount); stored.PaymentStatus.ShouldBe(declaration);
				stored.ChargeDate.ShouldBe(oldRawDate); stored.DueDate.ShouldBe(oldRawDue);
				stored.SchoolChargeDate.ShouldBeNull(); stored.SchoolDueDate.ShouldBeNull();
				stored.AcademicYearId.ShouldBeNull(); stored.Currency.ShouldBeNull(); stored.FinanceSourceLinkId.ShouldBeNull();
			}
			var baseline = await repository.GetAsync(owner.AssessmentId);
			baseline.Amount.ShouldBe(100m); baseline.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			baseline.ChargeDate.ShouldBe(legacy.BaselineRawDate); baseline.DueDate.ShouldBe(legacy.BaselineRawDue);
			baseline.AcademicYearId.ShouldBeNull(); baseline.Currency.ShouldBeNull(); baseline.FinanceSourceLinkId.ShouldBeNull();
			(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(0);
		});
	}
}
