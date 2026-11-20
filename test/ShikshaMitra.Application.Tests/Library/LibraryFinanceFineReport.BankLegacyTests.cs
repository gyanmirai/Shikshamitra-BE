using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Fine_Collection_Should_Not_Turn_Reserved_Or_Unverified_Evidence_Into_Settled_Money()
	{
		var (context, first) = await CollectTypedBankInstrumentAsync();
		var owner = context.Owner;
		using (GetRequiredService<ICurrentTenant>().Change(owner.TenantId))
		using (SignIn(owner))
		{
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
					Details = new() { ["upiTransactionId"] = "FINE-SECOND-RESERVATION", ["upiId"] = "school@bank" }
				}]
			}));
			reserved.Receivable.PendingVerificationAmount.ShouldBe(100.75m);
			reserved.Receivable.SettledPaymentAmount.ShouldBe(0m);
			await SeedFineReportLegacyDeclarationsAsync(owner);
			var retained = await CaptureDashboardClockSourceAsync(owner);
			var denied = await RestrictFineReportReadAsync(owner);
			await AssertFineReportAuthorityAsync(owner, denied);
			var actual = await FineReportPositionAsync(owner);
			actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
			actual.Pending.ShouldBe(100.75m); actual.FinancialOutstanding.ShouldBe(100.75m); actual.Collectible.ShouldBe(0m);
			await WithUnitOfWorkAsync(async () =>
			{
				var sources = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Reports));
				(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(sources)).ShouldBe(3,
					"The existing canonical source scope must already authorize all three real sources, including stored Staff2.");
			});
			var services = FineReportServices(owner);
			var request = FineReportRequest(owner);
			request.Filters.AcademicYearId = null;
			request.Filters.MemberRole = 1;

			var report = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));

			report.TotalCount.ShouldBe(3);
			var bank = report.Rows.Single(row => row["amount"] == "100.75");
			bank["paymentStatus"].ShouldBe("Pending"); bank["financeEvidence"].ShouldBe("Verified");
			bank["currency"].ShouldBe("INR"); bank["settledAmountText"].ShouldBe("0.00");
			bank["pendingVerificationAmountText"].ShouldBe("100.75");
			bank["financialOutstandingAmountText"].ShouldBe("100.75"); bank["collectibleAmountText"].ShouldBe("0.00");
			foreach (var legacy in report.Rows.Where(row => row["amount"] != "100.75"))
			{
				legacy["financeEvidence"].ShouldBe("Unposted"); legacy["currency"].ShouldBeNullOrEmpty();
				legacy["schoolChargeDate"].ShouldBeNullOrEmpty(); legacy["dateEvidence"].ShouldBe("Unconfirmed");
				foreach (var field in FineReportAmountFields) legacy[field].ShouldBeNull();
			}
			report.Rows.Single(row => row["amount"] == "20.25")["paymentStatus"].ShouldBe("Paid");
			report.Rows.Single(row => row["amount"] == "20.25")["memberRole"].ShouldBe("Staff");
			report.Rows.Single(row => row["amount"] == "20.25")["recordedMemberRole"].ShouldBe("2");
			report.Rows.Single(row => row["amount"] == "30.5")["paymentStatus"].ShouldBe("Waived");
			request.Filters.ChargeStatuses = [(int)ChargePaymentStatus.Paid];
			var localPaid = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));
			localPaid.Rows.ShouldHaveSingleItem()["financeEvidence"].ShouldBe("Unposted");
			(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
		}
		// A real old producer omitted reason-origin metadata. A supported category
		// rename must remain review; neither parser nor original snapshot is changed.
		var old = await SeedLegacyOriginPostAsync();
		using (GetRequiredService<ICurrentTenant>().Change(old.Owner.TenantId))
		using (SignIn(old.Owner))
		{
			await RenameLegacyOriginCategoryAsync(old.Owner);
			var retained = await CaptureDashboardClockSourceAsync(old.Owner);
			var denied = await RestrictFineReportReadAsync(old.Owner);
			await AssertFineReportAuthorityAsync(old.Owner, denied);
			(await FineReportPositionAsync(old.Owner)).Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
			var report = await WithUnitOfWorkAsync(() => FineReportServices(old.Owner).Reports.PreviewAsync(FineReportRequest(old.Owner)));
			var row = report.Rows.ShouldHaveSingleItem();
			row["amount"].ShouldBe("100"); row["paymentStatus"].ShouldBe("Pending"); row["financeEvidence"].ShouldBe("NeedsReview");
			foreach (var field in FineReportAmountFields) row[field].ShouldBeNull();
			(await CaptureDashboardClockSourceAsync(old.Owner)).ShouldBe(retained);
			await AssertNoTypedCollectionsAsync();
		}
	}
}
