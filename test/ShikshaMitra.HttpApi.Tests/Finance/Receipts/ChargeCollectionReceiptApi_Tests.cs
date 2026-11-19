using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeReceiptTests;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeCollectionReceiptApi_Tests : ShikshaMitraApiTestBase
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	public ChargeCollectionReceiptApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory)
	{
		ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Charge_Only_Collection_Downloads_And_Regenerates_Pdf_Without_Recollecting()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
		var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
		var chargeId = Guid.NewGuid();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var charge = new Charge(chargeId, TenantId, Factory.SeededAcademicYearId, Guid.NewGuid(),
				"Charge-only receipt regression", Guid.NewGuid(), studentId, boardId,
				Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId,
				"Synthetic charge collection", 1000m, DateTimeOffset.UtcNow.AddDays(1),
				ChargePriority.Medium, ChargeStatus.Pending, Guid.NewGuid());
			var head = await scope.ServiceProvider.GetRequiredService<IRepository<FeeHead, Guid>>().GetAsync(feeHeadId);
			charge.SetAccountingSnapshot(head.IncomeCategoryId);
			charge.Approve(Guid.NewGuid(), "Synthetic approval");
			await scope.ServiceProvider.GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(charge);
			await uow.CompleteAsync();
		}

		var input = new CollectFeeInput
		{
			IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = studentId,
			AcademicYearId = Factory.SeededAcademicYearId,
			ChargePayments = [new() { ChargeId = chargeId, Amount = 1000d }],
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1000d }],
		};
		var collected = await Client.PostAsJsonAsync("/api/fee-collections", input, JsonOptions);
		collected.StatusCode.ShouldBe(HttpStatusCode.Created, await collected.Content.ReadAsStringAsync());
		var output = (await collected.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions))!;
		output.Status.ShouldBe(FeeCollectionStatus.Paid);
		output.TotalAmount.ShouldBe(1000d);
		output.FeeReceiptId.ShouldBeNull();
		output.ChargeReceiptId.ShouldNotBeNull();
		output.PaidCharges!.Single().AmountPaid.ShouldBe(1000d);
		var receiptId = output.ChargeReceiptId.Value;
		var detail = await GetAndReadAsync<FeeReceiptDetailDto>($"/api/v1/fee-receipts/{receiptId}");
		detail.ReceiptScope.ShouldBe(FeeReceiptScope.Charge);
		detail.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
		detail.HasPdf.ShouldBeTrue();
		detail.TotalPaidAmount.ShouldBe(1000d);
		await AssertPdfAsync(receiptId, output.ChargeReceiptPdfFileName!);

		// Simulate a lost storage object while retaining the immutable payment and receipt.
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var receipt = await scope.ServiceProvider.GetRequiredService<IFeePaymentReceiptRepository>().GetAsync(receiptId);
			var blobs = scope.ServiceProvider.GetRequiredService<FeeReceiptBlobManager>();
			receipt.PdfBlobName.ShouldNotBeNull();
			await blobs.DeletePdfIfExistsAsync(receipt.PdfBlobName!);
			(await blobs.GetPdfOrNullAsync(receipt.PdfBlobName!)).ShouldBeNull();
			await uow.CompleteAsync();
		}
		await AssertPdfAsync(receiptId, output.ChargeReceiptPdfFileName!);

		var replay = await Client.PostAsJsonAsync("/api/fee-collections", input, JsonOptions);
		replay.StatusCode.ShouldBe(HttpStatusCode.Created);
		var replayOutput = (await replay.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions))!;
		replayOutput.Id.ShouldBe(output.Id);
		replayOutput.ChargeReceiptId.ShouldBe(receiptId);
		replayOutput.TotalAmount.ShouldBe(1000d);
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var records = await scope.ServiceProvider.GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
				.GetListAsync(record => record.ChargeId == chargeId);
			records.Count.ShouldBe(1);
			records.Single().Amount.ShouldBe(1000d);
			await uow.CompleteAsync();
		}
	}

	private async Task AssertPdfAsync(Guid receiptId, string filename)
	{
		var response = await Client.GetAsync($"/api/v1/fee-receipts/{receiptId}/pdf");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
		var disposition = response.Content.Headers.ContentDisposition!;
		(disposition.FileNameStar ?? disposition.FileName?.Trim('"')).ShouldBe(filename);
		var bytes = await response.Content.ReadAsByteArrayAsync();
		Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
	}
}
