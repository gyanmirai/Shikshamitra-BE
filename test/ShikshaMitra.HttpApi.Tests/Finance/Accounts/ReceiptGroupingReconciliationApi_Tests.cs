using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ReceiptGroupingReconciliationApi_Tests : ShikshaMitraApiTestBase
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private const string BaseUrl = "/api/v1/finance/reconciliations";

	public ReceiptGroupingReconciliationApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory)
	{
		FeeCollectionTestHelper.DefaultAcademicYearId = ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		FeeCollectionTestHelper.DefaultMediumId = ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		FeeCollectionTestHelper.DefaultStandardId = ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		FeeCollectionTestHelper.DefaultDivisionId = ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	[Theory]
	[InlineData(CollectionReceiptGrouping.Combined, false)]
	[InlineData(CollectionReceiptGrouping.Separate, false)]
	[InlineData(CollectionReceiptGrouping.Combined, true)]
	[InlineData(CollectionReceiptGrouping.Separate, true)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Receipt_Count_And_Payment_Decision_Agree_Without_Double_Counting(CollectionReceiptGrouping grouping, bool reject)
	{
		var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
		var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, 100d);
		Guid transactionId;
		Guid receiptId;
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var charge = new Charge(Guid.NewGuid(), TenantId, Factory.SeededAcademicYearId, Guid.NewGuid(), "Test charge",
				Guid.NewGuid(), studentId, boardId, Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId,
				"Receipt grouping test", 25m, DateTimeOffset.UtcNow.AddDays(1), (ChargePriority)0, (ChargeStatus)0, Guid.NewGuid());
			await scope.ServiceProvider.GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(charge);
			var manager = scope.ServiceProvider.GetRequiredService<FeeCollectionManager>();
			var result = await manager.ExecuteCollectionTransactionAsync(new(
				studentId, Factory.SeededAcademicYearId, boardId, Factory.SeededMediumId,
				FeeCollectionStatus.InReconciliation, 135d, "Receipt grouping test", null,
				[(feeHeadId, 100d)], [(charge.Id, 25d)], [(dueFeeId, 100d)],
				[(PaymentMethodEnum.Cheque, 135d, new ChequeDetails("123456", "Test bank", "Test branch", DateOnly.FromDateTime(DateTime.UtcNow)))],
				LateFeeAllocations: [(dueFeeId, 10d)], ReceiptGrouping: grouping));
			transactionId = result.Metadata.Id;
			receiptId = result.Receipts.Last().Id; // Either separate row must operate on the whole instrument.
			await uow.CompleteAsync();
		}

		var listing = await Client.GetAsync($"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}&pageSize=100");
		listing.EnsureSuccessStatusCode();
		var json = await listing.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		var rows = json.GetProperty("items").EnumerateArray()
			.Where(row => row.GetProperty("feeCollectionTransactionId").GetGuid() == transactionId).ToList();
		rows.Count.ShouldBe(grouping == CollectionReceiptGrouping.Combined ? 1 : 2);
		rows.Sum(row => row.GetProperty("amount").GetDecimal()).ShouldBe(135m);
		rows.All(row => row.GetProperty("paymentReceiptCount").GetInt32() == rows.Count).ShouldBeTrue();
		var action = reject ? "reject" : "approve";
		var response = await Client.PatchAsJsonAsync($"{BaseUrl}/{receiptId}/{action}", new { remarks = "Bank decision for receipt grouping test" }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var receipts = await scope.ServiceProvider.GetRequiredService<IFeePaymentReceiptRepository>().GetListAsync(r => r.FeeCollectionTransactionId == transactionId);
			var originals = receipts.Where(r => r.LinkedProvisionalReceiptId == null).ToList();
			originals.All(r => r.VerificationStatus == (reject ? FeeReceiptVerificationStatus.Rejected : FeeReceiptVerificationStatus.Verified)).ShouldBeTrue();
			receipts.Count.ShouldBe(reject ? originals.Count : originals.Count * 2);
			if (!reject)
			{
				receipts.Where(r => r.LinkedProvisionalReceiptId != null).Sum(r => (decimal)r.TotalPaidAmount).ShouldBe(135m);
				foreach (var final in receipts.Where(r => r.LinkedProvisionalReceiptId != null))
				{
					var original = originals.Single(r => r.Id == final.LinkedProvisionalReceiptId);
					final.LateFeePaidAmount.ShouldBe(original.LateFeePaidAmount);
				}
				if (grouping == CollectionReceiptGrouping.Combined)
				{
					var final = receipts.Single(r => r.LinkedProvisionalReceiptId != null);
					final.FeePaidAmount.ShouldBe(100m);
					final.ChargePaidAmount.ShouldBe(35m);
				}
			}
			var metadata = await scope.ServiceProvider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(transactionId);
			metadata.Status.ShouldBe(reject ? FeeCollectionStatus.Reversed : FeeCollectionStatus.Paid);
			var due = await scope.ServiceProvider.GetRequiredService<IDueFeeRepository>().GetAsync(dueFeeId);
			due.PaidAmount.ShouldBe(reject ? 0m : 100m);
			var charges = await scope.ServiceProvider.GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().GetListAsync(r => r.FeeRecordMetadataId == transactionId);
			charges.Count.ShouldBe(reject ? 0 : 1);
			await uow.CompleteAsync();
		}
		var duplicate = await Client.PatchAsJsonAsync($"{BaseUrl}/{receiptId}/{action}", new { remarks = "Duplicate decision" }, JsonOptions);
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}
}
