using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Fine_Collection_Should_Keep_Recorded_Status_And_Canonical_Cash_Waiver_Money_Distinct()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var context = fixture.Settlement;
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var waiver = await WithUnitOfWorkAsync(() => TypedService(owner).RequestWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
			Reason = "Actual reviewed assistance retained in the Fine Collection register.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		var applicationId = waiver.ApplicationId ?? throw new InvalidOperationException("Expected a real waiver application.");
		form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		await WithUnitOfWorkAsync(() => TypedService(owner).ReverseWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, WaiverApplicationId = applicationId, Amount = 4.05m,
			Reason = "Actual partial reversal, not a rewritten original waiver.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		var retained = await CaptureDashboardClockSourceAsync(owner);
		var denied = await RestrictFineReportReadAsync(owner);
		await AssertFineReportAuthorityAsync(owner, denied);
		var actual = await FineReportPositionAsync(owner);
		actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		actual.Settled.ShouldBe(40.25m); actual.NetWaived.ShouldBe(6.05m); actual.Collectible.ShouldBe(54.45m);
		var services = FineReportServices(owner);
		var request = FineReportRequest(owner);
		request.Filters.ChargeStatuses = [(int)ChargePaymentStatus.Pending];

		var report = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));

		report.TotalCount.ShouldBe(1);
		var row = report.Rows.ShouldHaveSingleItem();
		row["amount"].ShouldBe("100.75"); row["paymentStatus"].ShouldBe("Pending"); row["paidDate"].ShouldBeNullOrEmpty();
		row["financeEvidence"].ShouldBe("Verified"); row["currency"].ShouldBe("INR");
		row["originalAmountText"].ShouldBe("100.75"); row["acceptedAmountText"].ShouldBe("40.25");
		row["settledAmountText"].ShouldBe("40.25"); row["pendingVerificationAmountText"].ShouldBe("0.00");
		row["netWaivedAmountText"].ShouldBe("6.05"); row["financialOutstandingAmountText"].ShouldBe("54.45");
		row["collectibleAmountText"].ShouldBe("54.45"); row["refundedAmountText"].ShouldBe("0.00");
		row["schoolChargeDate"].ShouldBe(owner.AssessmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		row["schoolDueDate"].ShouldBe(owner.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); row["dateEvidence"].ShouldBe("Confirmed school dates");
		report.Columns.Count.ShouldBeLessThanOrEqualTo(13);
		report.Columns.ShouldContain(column => column.Id == "amount" && column.Label == "Original assessment amount");
		report.Columns.ShouldContain(column => column.Id == "paymentStatus" && column.Label == "Recorded local status");
		report.Columns.ShouldContain(column => column.Id == "financeEvidence");
		report.Columns.ShouldNotContain(column => column.Id == "chargeDate" || column.Id == "paidDate");
		request.Filters.FromDate = owner.AssessmentDate.ToDateTime(TimeOnly.MinValue);
		request.Filters.ToDate = owner.AssessmentDate.ToDateTime(TimeOnly.MinValue);
		(await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request))).TotalCount.ShouldBe(1);
		request.Columns = ["chargeDate", "dueDate", "amount", "paymentStatus", "paidDate", "remarks"];
		var compatibility = await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request));
		compatibility.Columns.Select(column => column.Id).ShouldBe(request.Columns);
		compatibility.Columns.Single(column => column.Id == "chargeDate").Label.ShouldBe("Legacy charge date component");
		compatibility.Columns.Single(column => column.Id == "dueDate").Label.ShouldBe("Legacy due date component");
		request.Filters.ChargeStatuses = [(int)ChargePaymentStatus.Paid];
		(await WithUnitOfWorkAsync(() => services.Reports.PreviewAsync(request))).TotalCount.ShouldBe(0);
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
	}
}
