using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Accounts.Activity;
using ShikshaMitra.Finance.Accounts.Incomes;
using Volo.Abp.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountsScalarPaging_Tests
{
    private static readonly Dictionary<string, FinanceTableColumn> Columns = new()
    {
        ["amount"] = new(FinanceTableValueType.Number),
        ["status"] = new(FinanceTableValueType.Enum, Values: [0, 1, 2]),
        ["receiptNumber"] = new(FinanceTableValueType.Text),
        ["receiptDate"] = new(FinanceTableValueType.Date),
        ["studentName"] = new(FinanceTableValueType.Text),
        ["board"] = new(FinanceTableValueType.Text),
        ["standardName"] = new(FinanceTableValueType.Text)
    };

    [Theory]
    [InlineData("""[{"column":"amount","type":"between","value":{"min":998,"max":1000}}]""")]
    [InlineData("""[{"column":"status","type":"equals","value":1}]""")]
    [InlineData("""[{"column":"receiptNumber","type":"endsWith","value":"-target"}]""")]
    [InlineData("""[{"column":"receiptDate","type":"equals","value":"2026-06-12T00:00:00Z"}]""")]
    [InlineData("""[{"column":"studentName","type":"contains","value":"Student (42)"}]""")]
    [InlineData("""[{"column":"board","type":"equals","value":"CBSE - (English)"}]""")]
    [InlineData("""[{"column":"standardName","type":"equals","value":"Class 1 - (A)"}]""")]
    public void Common_reconciliation_headers_remain_in_postgres_before_page_selection(string filters)
    {
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only").Options);
        var table = FinanceTableQuery.Parse(filters, null, null, null, Columns);
        var query = EfCoreFeePaymentReceiptRepository.ApplyReconciliationScalarHeaders(
            db.Set<FeePaymentReceipt>().IgnoreQueryFilters(), table, PaymentMethodEnum.Cheque);
        query = EfCoreFeePaymentReceiptRepository.ApplyReconciliationContextHeaders(query, db, table);
        var sql = query.OrderByDescending(r => r.CreationTime).ThenByDescending(r => r.Id)
            .Skip(25).Take(10).Select(r => r.Id).ToQueryString();
        sql.ShouldContain("WHERE");
        sql.ShouldContain("LIMIT");
        sql.ShouldContain("OFFSET");
        sql.ShouldNotContain("PaymentDetails");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Alternative_amount_equality_uses_native_numeric_SQL_for_each_provider(bool sqlite)
    {
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>();
        if (sqlite) options.UseSqlite("Data Source=:memory:");
        else options.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only");
        using var db = new ShikshaMitraDbContext(options.Options);
        var table = FinanceTableQuery.Parse("""
            [{"column":"amount","type":"equals","value":999},
             {"column":"amount","type":"equals","value":888},
             {"column":"status","type":"equals","value":0}]
            """, null, null, null, Columns);
        var sql = EfCoreFeePaymentReceiptRepository.ApplyReconciliationScalarHeaders(
            db.Set<FeePaymentReceipt>().IgnoreQueryFilters(), table, PaymentMethodEnum.Cheque)
            .Select(receipt => receipt.Id).ToQueryString();
        sql.ShouldContain("TotalPaidAmount");
        sql.ShouldContain("999");
        sql.ShouldContain("888");
        // Decimal equality on SQLite can become textual IN matching. The receipt
        // amount is Double and must remain numeric, including alternative values.
        sql.Contains("AS TEXT", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    private sealed class RegisterTimeRow
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
    }

    [Fact]
    public async Task Refund_utc_dates_compose_with_transaction_dates_before_postgres_paging()
    {
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only").Options);
        var contexts = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
        contexts.GetDbContextAsync().Returns(db);
        var times = await new AccountsRegisterEvidenceQuery(contexts).RefundTimesAsync(Guid.NewGuid(), Guid.NewGuid());
        times.ShouldNotBeNull();
        var transactions = db.Set<Transaction>().IgnoreQueryFilters()
            .Select(x => new RegisterTimeRow { Id = x.Id, Date = x.PaymentDate });
        var refunds = times!.Select(x => new RegisterTimeRow { Id = x.Id, Date = x.PostedAtUtc });
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var sql = transactions.Concat(refunds).Where(x => x.Date >= start && x.Date < end)
            .OrderBy(x => x.Date).ThenBy(x => x.Id).Skip(50).Take(25).ToQueryString();
        sql.ShouldContain("UNION ALL");
        sql.ShouldContain("PostedAtUtc");
        sql.ShouldContain("LIMIT");
        sql.ShouldContain("OFFSET");
        if (db.Model.FindEntityType(typeof(Transaction))!.FindProperty(nameof(Transaction.PaymentDate))!.GetColumnType() == "timestamp without time zone")
            sql.ShouldContain("AT TIME ZONE 'UTC'");
    }

    [Fact]
    public void Alternative_amounts_and_approved_cash_status_keep_exact_count_and_deep_page()
    {
        var rows = Enumerable.Range(0, 105).Select(index => new FeePaymentReceipt(Guid.NewGuid())
        {
            ReceiptNumber = $"R-{index:000}", TotalPaidAmount = index % 2 == 0 ? 999 : 100,
            VerificationStatus = FeeReceiptVerificationStatus.NotApplicable
        }).AsQueryable();
        var table = FinanceTableQuery.Parse("""
            [{"column":"amount","type":"equals","value":999},
             {"column":"amount","type":"equals","value":888},
             {"column":"status","type":"equals","value":1}]
            """, null, null, null, Columns);
        var matching = EfCoreFeePaymentReceiptRepository.ApplyReconciliationScalarHeaders(rows, table, PaymentMethodEnum.Cash);
        matching.Count().ShouldBe(53);
        matching.OrderBy(x => x.ReceiptNumber).Skip(50).Take(10).Count().ShouldBe(3);
    }
}
