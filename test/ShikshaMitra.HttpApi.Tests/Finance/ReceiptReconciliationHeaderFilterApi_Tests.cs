using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance;

// Query-string assertions deliberately compile against the pre-filter DTOs as well.
[Collection(FinanceRoutingApiTestCollection.Name)]
public class ReceiptReconciliationHeaderFilterApi_Tests(FinanceRoutingApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly DateOnly TargetDate = new(2026, 6, 12);
	private string ReceiptUrl(string search) => $"/api/v1/fee-receipts?academicYearId={Factory.SeededAcademicYearId}&boardId={Factory.SeededBoardId}&educationMediumId={Factory.SeededMediumId}&search={search}&maxResultCount=1";
	private string ReconciliationUrl(string search, PaymentMethodEnum method) => $"/api/v1/finance/reconciliations?academicYearId={Factory.SeededAcademicYearId}&paymentMethod={method}&search={search}&pageSize=1";
	private static object Filter(string column, string type, object value) => new { column, type, value };

	[Fact]
	public async Task Receipt_headers_filter_complete_scoped_results_before_count_and_page()
	{
		var (search, targetId) = await SeedAsync(PaymentMethodEnum.Cheque, 102);
		var url = ReceiptUrl(search);
		var target = await AssertListAsync(url, 1, targetId, Filter("totalPaidAmount", "greaterThan", 900));
		var row = target.GetProperty("items")[0];
		foreach (var criterion in new[]
		{
			Filter("receiptNumber", "equals", row.GetProperty("receiptNumber").GetString()!),
			Filter("receiptDate", "equals", "2026-06-12"),
			Filter("studentName", "contains", "Header Student"),
			Filter("receiptKind", "equals", "Provisional Receipt (Fee)"),
			Filter("paymentMethodLabel", "equals", "Cheque"),
			Filter("verificationStatus", "equals", "Pending"),
			Filter("rollNo", "equals", "—")
		})
			await AssertListAsync(url, 1, targetId, criterion, Filter("totalPaidAmount", "greaterThan", 900));
		await AssertListAsync(url, 0, null, Filter("totalPaidAmount", "lessThan", 1));
		await AssertListAsync(url, 0, null, Filter("receiptNumber", "contains", "missing"));
		await AssertListAsync(url, 0, null, Filter("receiptDate", "before", "2020-01-01"));
		await AssertListAsync(url, 0, null, Filter("verificationStatus", "equals", "Rejected"));
		await AssertListAsync(url, 1, targetId, Filter("receiptNumber", "contains", "missing"), Filter("receiptNumber", "endsWith", "-target"));
		await AssertListAsync(url + "&month=7", 0, null, Filter("totalPaidAmount", "greaterThan", 900));
		await AssertListAsync(url + "&skipCount=1", 1, null, Filter("totalPaidAmount", "greaterThan", 900));
		await AssertListAsync(url, 102, null);
		await AssertListAsync(url + "&filters=%5B%5D", 102, null);
	}

	[Fact]
	public async Task All_six_reconciliation_methods_filter_visible_columns_before_count_and_page()
	{
		foreach (var method in new[] { PaymentMethodEnum.Cheque, PaymentMethodEnum.DemandDraft, PaymentMethodEnum.BankTransfer,
			PaymentMethodEnum.CreditCard, PaymentMethodEnum.DebitCard, PaymentMethodEnum.UPI })
		{
			var (search, targetId) = await SeedAsync(method, 102);
			var url = ReconciliationUrl(search, method);
			var baseline = await GetAsync(url);
			var target = await AssertListAsync(url, 1, targetId, Filter("amount", "between", new { min = 998, max = 1000 }));
			target.GetProperty("tabCounts").GetRawText().ShouldBe(baseline.GetProperty("tabCounts").GetRawText());
			var row = target.GetProperty("items")[0];
			var criteria = new List<object>
			{
				Filter("studentName", "contains", "Header Student"),
				Filter("board", "equals", $"{row.GetProperty("board").GetString()} - ({row.GetProperty("medium").GetString()})"),
				Filter(method == PaymentMethodEnum.UPI ? "class" : "standardName", "equals", $"{row.GetProperty("class").GetString()} - ({row.GetProperty("division").GetString()})"),
				Filter("status", "equals", 0),
			};
			criteria.AddRange(method switch
			{
				PaymentMethodEnum.Cheque => [Filter("chequeNo", "equals", "000042"), Filter("bankName", "equals", "Header Bank"), Filter("receivedChequeDate", "equals", "2026-06-12"), Filter("remarks", "equals", "-")],
				PaymentMethodEnum.DemandDraft => [Filter("ddNumber", "equals", "000042"), Filter("bankName", "equals", "Branch fallback"), Filter("ddIssueDate", "equals", "2026-06-12"), Filter("paymentDate", "equals", "2026-06-13")],
				PaymentMethodEnum.BankTransfer => [Filter("transactionId", "equals", "000042"), Filter("accountHolderName", "equals", "Header Holder"), Filter("bankLabel", "equals", "Header Bank •••• 0042"), Filter("transferDate", "equals", "2026-06-12")],
				PaymentMethodEnum.CreditCard or PaymentMethodEnum.DebitCard => [Filter("last4Digits", "equals", "•••• 0042"), Filter("transactionId", "equals", "000042"), Filter("cardHolderName", "equals", "Header Holder"), Filter("paymentDate", "equals", "2026-06-12")],
				_ => [Filter("upiId", "equals", "header@upi"), Filter("upiTransactionId", "equals", "000042"), Filter("receiptDate", "equals", "2026-06-12"), Filter("remarks", "equals", "-")]
			});
			foreach (var criterion in criteria)
				await AssertListAsync(url, 1, targetId, criterion, Filter("amount", "greaterThan", 900));
			await AssertListAsync(url, 0, null, Filter("studentName", "contains", "missing"));
			await AssertListAsync(url, 0, null, Filter("amount", "lessThan", 1));
			await AssertListAsync(url, 0, null, Filter("status", "equals", 2));
			await AssertListAsync(url, 1, targetId, Filter("amount", "equals", 999), Filter("amount", "equals", 888), Filter("status", "equals", 0));
			await AssertListAsync(url + "&month=7&year=2026", 0, null, Filter("amount", "greaterThan", 900));
			await AssertListAsync(url + "&status=2", 0, null, Filter("amount", "greaterThan", 900));
			await AssertListAsync(url + "&page=2", 1, null, Filter("amount", "greaterThan", 900));
			await AssertListAsync(url, 102, null);
		}
	}

	[Fact]
	public async Task Malformed_and_unsupported_headers_are_rejected_even_when_parent_search_has_no_matches()
	{
		foreach (var url in new[] { ReceiptUrl("absent-" + Guid.NewGuid()), ReconciliationUrl("absent-" + Guid.NewGuid(), PaymentMethodEnum.Cheque) })
		foreach (var filters in new[] { "{", "null", "[null]", "[{\"column\":null,\"type\":\"equals\",\"value\":\"x\"}]", "[{\"column\":\"rawAccountNumber\",\"type\":\"equals\",\"value\":\"1234\"}]" })
		{
			var response = await Client.GetAsync(url + "&filters=" + Uri.EscapeDataString(filters));
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		}
	}

	private async Task<JsonElement> AssertListAsync(string url, int count, Guid? expectedId, params object[] filters)
	{
		var result = await GetAsync(url + (filters.Length == 0 ? "" : "&filters=" + Uri.EscapeDataString(JsonSerializer.Serialize(filters))));
		result.GetProperty("totalCount").GetInt32().ShouldBe(count, url + " " + JsonSerializer.Serialize(filters));
		if (expectedId.HasValue) result.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(expectedId.Value);
		else if (count == 0 || url.Contains("skipCount=1") || url.Contains("page=2")) result.GetProperty("items").GetArrayLength().ShouldBe(0);
		return result;
	}

	private async Task<JsonElement> GetAsync(string url)
	{
		var response = await Client.GetAsync(url);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
	}

	private async Task<(string Search, Guid TargetId)> SeedAsync(PaymentMethodEnum method, int count)
	{
		var search = "hdr" + Guid.NewGuid().ToString("N")[..12];
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), TenantId, "Header", null, "Student",
			Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
			Factory.SeededStandardId, Factory.SeededDivisionId, DateTimeOffset.UtcNow.AddYears(-12), GenderEnum.Male, "9876543210");
		await scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student);
		var targetId = Guid.NewGuid();
		for (var index = 0; index < count; index++)
		{
			var target = index == 0;
			var date = target ? TargetDate : TargetDate.AddDays(1);
			var amount = target ? 999m : 100m;
			var number = search + (target ? "-target" : $"-{index:000}");
			var transaction = new FeeRecordMetadata(Guid.NewGuid(), TenantId, student.Id, Factory.SeededAcademicYearId,
				(double)amount, number, FeeCollectionStatus.InReconciliation);
			await scope.ServiceProvider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(transaction);
			PaymentDetails details = method switch
			{
				PaymentMethodEnum.Cheque => new ChequeDetails("000042", "Header Bank", "Branch", date),
				PaymentMethodEnum.DemandDraft => new DemandDraftDetails("000042", "", "Branch fallback", date, date.AddDays(1)),
				PaymentMethodEnum.BankTransfer => new BankTransferDetails("000042", "Hidden payer bank", "HIDDEN1234", date, "Header Holder"),
				PaymentMethodEnum.CreditCard => new CreditCardDetails("000042", "0042", "Hidden payer bank", "Header Holder"),
				PaymentMethodEnum.DebitCard => new CreditCardDetails("000042", "0042", "Hidden payer bank", "Header Holder"),
				_ => new UpiDetails("000042", "header@upi")
			};
			var payment = PaymentMethodRecord.Create(student.Id, transaction.Id, amount,
				new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), method, details, TenantId);
			payment.SetReceivingBank(null, "Header Bank •••• 0042");
			await scope.ServiceProvider.GetRequiredService<IRepository<PaymentMethodRecord, Guid>>().InsertAsync(payment);
			await scope.ServiceProvider.GetRequiredService<IFeePaymentReceiptRepository>().InsertAsync(new FeePaymentReceipt(target ? targetId : Guid.NewGuid())
			{
				TenantId = TenantId, StudentId = student.Id, FeeCollectionTransactionId = transaction.Id,
				AcademicYearId = Factory.SeededAcademicYearId, BoardId = Factory.SeededBoardId, EducationMediumId = Factory.SeededMediumId,
				ReceiptKind = FeeReceiptKind.ProvisionalFee, ReceiptScope = FeeReceiptScope.Fee,
				VerificationStatus = FeeReceiptVerificationStatus.PendingVerification, ReceiptDate = date,
				TotalPaidAmount = (double)amount, ReceiptNumber = number, CreationTime = DateTime.UtcNow.AddDays(-count + index)
			});
		}
		await uow.CompleteAsync();
		return (search, targetId);
	}
}
