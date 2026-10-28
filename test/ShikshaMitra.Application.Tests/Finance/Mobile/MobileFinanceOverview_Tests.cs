using System;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Fact]
	public Task Finance_overview_uses_owned_principal_records_instead_of_mixed_transaction_totals() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.View);
		var category = await Insert(new FeeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Overview fees", null));
		var income = await Insert(new TransactionCategory(Guid.NewGuid(), tenantId, f.Year.Id, TransactionType.Income, "Overview income", null));
		var head = await Insert(new FeeHead(Guid.NewGuid(), tenantId, f.Year.Id, category.Id, "Tuition", null, true, null, income.Id));
		var due = new DueFee(Guid.NewGuid(), tenantId, f.Student.Id, head.Id, MonthEnum.October, 2026, false, 1000m, DateTime.Today.AddDays(2));
		due.ApplyPayment(500m); await Insert(due);
		// Mixed principal/charge transactions: the principal record is only 300 of the 1000 total.
		foreach (var spec in new[] { (f.Year.Id, f.Student.Id, FeeCollectionStatus.InReconciliation, 1000d, 300d),
			(Guid.NewGuid(), f.Student.Id, FeeCollectionStatus.InReconciliation, 900d, 900d),
			(f.Year.Id, Guid.NewGuid(), FeeCollectionStatus.InReconciliation, 800d, 800d),
			(f.Year.Id, f.Student.Id, FeeCollectionStatus.Reversed, 700d, 700d),
			(f.Year.Id, f.Student.Id, FeeCollectionStatus.Paid, 1000d, 200d) })
		{
			var tx = await Insert(new FeeRecordMetadata(Guid.NewGuid(), tenantId, spec.Item2, spec.Item1, spec.Item4, Guid.NewGuid().ToString("N")[..18], spec.Item3));
			await Insert(new FeeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = spec.Item2, FeeHeadId = head.Id, FeeRecordMetadataId = tx.Id, Amount = spec.Item5, PaymentDate = DateTimeOffset.UtcNow });
			await Insert(PaymentMethodRecord.Create(spec.Item2, tx.Id, (decimal)spec.Item4, DateTimeOffset.UtcNow, PaymentMethodEnum.Cash, tenantId: tenantId));
   await Insert(new FeeCollectionDueFeeAllocation(Guid.NewGuid(),tenantId,tx.Id,due.Id,spec.Item5));
		}
		var result = await RealFinanceReads().OverviewAsync(f.Student.Id, f.Year.Id);
		WriteFamilyChargeWireSample("finance-overview.json", result);
		result.FeeOverview.TotalFee.ShouldBe("1000.00"); result.FeeOverview.NetFee.ShouldBe("1000.00");
		result.FeeOverview.PaidFee.ShouldBe("500.00"); result.FeeOverview.PendingFee.ShouldBe("500.00");
		result.FeeOverview.ReconciliationAmount.ShouldBe("300.00");
  var installments=await RealFinanceReads().InstallmentsAsync(f.Student.Id,f.Year.Id);
  var installment=installments.ShouldHaveSingleItem();installment.DueFeeId.ShouldBe(due.Id);installment.InstallmentId.ShouldBeNull();
  installment.Paid.ShouldBe("500.00");installment.Due.ShouldBe("500.00");installment.ReconciliationAmount.ShouldBe("300.00");
  WriteFamilyChargeWireSample("installments-persisted.json",installments);
		var payments=await RealFinanceReads().PaymentsAsync(f.Student.Id,f.Year.Id,skipCount:1,maxResultCount:1,status:"Reversed");
		var paymentJson=System.Text.Json.JsonSerializer.SerializeToElement(payments,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
		paymentJson.TryGetProperty("paymentSummary",out var paymentSummary).ShouldBeTrue("History requires a whole-year summary independent of status filter and page.");
		paymentSummary.GetProperty("paidAmount").GetString().ShouldBe("1000.00");
		paymentSummary.GetProperty("successfulPaymentCount").GetInt32().ShouldBe(1);
		paymentSummary.GetProperty("studentId").GetGuid().ShouldBe(f.Student.Id);
		paymentSummary.GetProperty("academicYearId").GetGuid().ShouldBe(f.Year.Id);
		payments.Items.ShouldBeEmpty();payments.TotalCount.ShouldBe(1);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.ViewConcessions);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.ViewRefunds);
		var concessionDue=new DueFee(Guid.NewGuid(),tenantId,f.Student.Id,head.Id,MonthEnum.November,2026,false,200m,DateTime.Today.AddMonths(1));
		concessionDue.ApplyConcession(75.25m);await Insert(concessionDue);
		var foreignDue=new DueFee(Guid.NewGuid(),tenantId,Guid.NewGuid(),head.Id,MonthEnum.November,2026,false,1000m,DateTime.Today.AddMonths(1));
		foreignDue.ApplyConcession(900m);await Insert(foreignDue);
		foreach(var kind in new[]{"concession","refund"})
		{
			var requests=await RealFinanceReads().RequestsAsync(f.Student.Id,f.Year.Id,kind,skipCount:20);
			var requestJson=System.Text.Json.JsonSerializer.SerializeToElement(requests,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
			requestJson.TryGetProperty("requestSummary",out var summary).ShouldBeTrue();
			summary.GetProperty("totalAmount").GetString().ShouldBe(kind=="concession"?"75.25":"0.00");
			summary.GetProperty("kind").GetString().ShouldBe(kind);
		}
	});
}
