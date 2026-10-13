using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Finance;

public class FeeRefundColumnFilter_Tests
{
    [Theory]
    [InlineData("_voucherNumber", "contains", "REF-")]
    [InlineData("name", "equals", "Diya Patel (10092)")]
    [InlineData("classDivision", "startsWith", "Class 1 / A")]
    [InlineData("_receiptNumber", "endsWith", "0004")]
    [InlineData("refundMethod", "contains", "Cash")]
    [InlineData("refundAmount", "greaterThan", "1000.50")]
    [InlineData("refundAmount", "lessThan", "2000")]
    [InlineData("refundAmount", "equals", "2000")]
    [InlineData("status", "equals", "0")]
    [InlineData("requestDate", "equals", "2026-10-02T00:00:00+05:30")]
    [InlineData("requestDate", "after", "2026-10-02T00:00:00+05:30")]
    [InlineData("requestDate", "before", "2026-10-02T00:00:00+05:30")]
    public void Every_Filter_Translates_To_Postgres_Before_Pagination(string column, string type, string value)
    {
        using var context = CreateContext();
        var filters = FeeRefundColumnFilter.Parse(JsonSerializer.Serialize(new[] { new { column, type, value } }));
        var requests = context.Set<FeeRefundRequest>().Include(r => r.Lines)
            .Include(r => r.Reservations).Include(r => r.Settlements);
        var query = FeeRefundColumnFilterQuery.Apply(requests, context.Set<Student>(), filters);
        var sql = query.OrderBy(r => r.Id).Skip(20).Take(20).ToQueryString();
        sql.ShouldContain("WHERE");
        sql.ShouldContain("LIMIT");
        sql.ShouldContain("OFFSET");
    }

    [Fact]
    public void Alternatives_Use_Or_And_Different_Columns_Use_And()
    {
        using var context = CreateContext();
        var filters = FeeRefundColumnFilter.Parse("""
            [{"column":"refundMethod","type":"equals","value":"Cash"},
             {"column":"refundMethod","type":"equals","value":"UPI"},
             {"column":"refundAmount","type":"greaterThan","value":1000}]
            """);
        var sql = FeeRefundColumnFilterQuery.Apply(context.Set<FeeRefundRequest>(), context.Set<Student>(), filters).ToQueryString();
        (sql.Contains(" OR ") || sql.Contains(" IN (")).ShouldBeTrue();
        sql.ShouldContain(" AND ");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"column\":\"actions\",\"type\":\"equals\",\"value\":0}]")]
    [InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":99}]")]
    [InlineData("[{\"column\":\"refundAmount\",\"type\":\"equals\",\"value\":\"bad\"}]")]
    [InlineData("[{\"column\":\"requestDate\",\"type\":\"equals\",\"value\":\"bad\"}]")]
    public void Invalid_Filters_Are_Rejected(string json) =>
        Should.Throw<AbpValidationException>(() => FeeRefundColumnFilter.Parse(json));

    private static ShikshaMitraDbContext CreateContext()
    {
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        return new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only").Options);
    }
}
