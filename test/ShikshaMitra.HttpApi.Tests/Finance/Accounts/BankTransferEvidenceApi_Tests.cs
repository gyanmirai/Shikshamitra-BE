using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class BankTransferEvidenceApi_Tests : ShikshaMitraApiTestBase
{
	public BankTransferEvidenceApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory)
	{
		FeeCollectionTestHelper.DefaultAcademicYearId = ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		FeeCollectionTestHelper.DefaultMediumId = ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		FeeCollectionTestHelper.DefaultStandardId = ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		FeeCollectionTestHelper.DefaultDivisionId = ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	[Theory]
	[InlineData("transactionId")]
	[InlineData("transactionReferenceNumber")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Collection_Verification_And_Both_Receipts_Preserve_Entered_Bank_Evidence(string referenceKey)
	{
		var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
		var (receiptId, transactionId, _) = await ReconciliationTestHelper.SeedProvisionalReceiptAsync(
			Client, Factory.Services, Factory.SeededAcademicYearId, feeHeadId, method: PaymentMethodEnum.BankTransfer,
			details: new() { [referenceKey] = "N123456789876", ["transferDate"] = "2026-10-20",
				["accountHolderName"] = "Rahul Sharma" });
		receiptId.ShouldNotBe(Guid.Empty);
		var inboxUrl = $"/api/v1/finance/reconciliations?academicYearId={Factory.SeededAcademicYearId}&paymentMethod=BankTransfer&pageSize=100";
		async Task<JsonElement> GetRow()
		{
			var listing = await Client.GetFromJsonAsync<JsonElement>(inboxUrl, JsonOptions);
			return listing.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("feeCollectionTransactionId").GetGuid() == transactionId);
		}
		void AssertEvidence(JsonElement instrument)
		{
			instrument.GetProperty("transactionReferenceNumber").GetString().ShouldBe("N123456789876");
			instrument.GetProperty("transferDate").GetString().ShouldBe("2026-10-20");
			instrument.GetProperty("accountHolderName").GetString().ShouldBe("Rahul Sharma");
		}
		var pending = await GetRow();
		AssertEvidence(pending.GetProperty("instrumentDetails"));
		pending.GetProperty("bankAccountId").GetGuid().ShouldNotBe(Guid.Empty);
		pending.GetProperty("bankLabel").GetString().ShouldStartWith("Synthetic Test Bank");
		await SavePdf(receiptId, $"bank-transfer-pending-{referenceKey}.pdf");
		var decision = await Client.PatchAsJsonAsync($"/api/v1/finance/reconciliations/{receiptId}/approve",
			new { remarks = "Bank statement confirms entered transfer evidence" }, JsonOptions);
		decision.StatusCode.ShouldBe(HttpStatusCode.OK, await decision.Content.ReadAsStringAsync());
		var approved = await GetRow();
		AssertEvidence(approved.GetProperty("instrumentDetails"));
		approved.GetProperty("bankAccountId").GetGuid().ShouldBe(pending.GetProperty("bankAccountId").GetGuid());
		var finalId = approved.GetProperty("finalReceiptId").GetGuid();
		finalId.ShouldNotBe(Guid.Empty);
		foreach (var id in new[] { receiptId, finalId })
		{
			var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/fee-receipts/{id}", JsonOptions);
			var payment = detail.GetProperty("paymentInstruments").EnumerateArray().Single();
			AssertEvidence(payment.GetProperty("instrumentDetails"));
			payment.GetProperty("bankAccountId").GetGuid().ShouldBe(pending.GetProperty("bankAccountId").GetGuid());
		}
		await SavePdf(finalId, $"bank-transfer-final-{referenceKey}.pdf");
	}

	private async Task SavePdf(Guid id, string filename)
	{
		var response = await Client.GetAsync($"/api/v1/fee-receipts/{id}/pdf");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var bytes = await response.Content.ReadAsByteArrayAsync();
		System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
		var directory = Environment.GetEnvironmentVariable("RECEIPT_E2E_PDF_DIRECTORY");
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
			await File.WriteAllBytesAsync(Path.Combine(directory, filename), bytes);
		}
		var text = ReceiptPdfTextReader.Read(bytes);
		text.ShouldContain("N123456789876");
		text.ShouldContain("20-Oct-2026");
		text.ShouldContain("Rahul Sharma");
		text.ShouldContain("Synthetic Test Bank");
	}
}
