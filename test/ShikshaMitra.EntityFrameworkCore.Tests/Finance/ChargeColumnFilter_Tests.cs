using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Finance;

public class ChargeColumnFilter_Tests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string ChargeName { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string Description { get; set; } = "";
        public int StudentCount { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset DueDate { get; set; }
    }

    private static readonly Dictionary<string, LambdaExpression> Fields = new()
    {
        ["chargeName"] = (Expression<Func<Row, string>>)(r => r.ChargeName),
        ["categoryName"] = (Expression<Func<Row, string>>)(r => r.CategoryName),
        ["description"] = (Expression<Func<Row, string>>)(r => r.Description),
        ["studentCount"] = (Expression<Func<Row, int>>)(r => r.StudentCount),
        ["amount"] = (Expression<Func<Row, decimal>>)(r => r.Amount),
        ["dueDate"] = (Expression<Func<Row, DateTimeOffset>>)(r => r.DueDate)
    };

    [Fact]
    public void Text_Alternatives_And_Numeric_Range_Apply_Before_Paging()
    {
        var filters = ChargeColumnFilter.Parse("""
            [{"column":"chargeName","type":"contains","value":"Trip"},
             {"column":"chargeName","type":"contains","value":"Exam"},
             {"column":"studentCount","type":"between","value":{"min":2,"max":4}}]
            """);
        var data = new[] {
            new Row { Id = 1, ChargeName = "School Trip", StudentCount = 1 },
            new Row { Id = 2, ChargeName = "Exam fee", StudentCount = 2 },
            new Row { Id = 3, ChargeName = "SCHOOL TRIP", StudentCount = 4 },
            new Row { Id = 4, ChargeName = "Uniform", StudentCount = 3 }
        }.AsQueryable();
        var query = ChargeColumnFilter.Apply(data, filters, Fields);
        query.Count().ShouldBe(2);
        query.OrderBy(r => r.Id).Skip(1).Take(1).Single().Id.ShouldBe(3);
    }

    [Fact]
    public void Calendar_Day_Uses_Explicit_Offset_And_Excludes_Next_Day()
    {
        var filters = ChargeColumnFilter.Parse("""
            [{"column":"dueDate","type":"equals","value":"2026-10-14T00:00:00+05:30"}]
            """);
        var rows = new[] {
            new Row { Id = 1, DueDate = DateTimeOffset.Parse("2026-10-13T18:29:59Z") },
            new Row { Id = 2, DueDate = DateTimeOffset.Parse("2026-10-13T18:30:00Z") },
            new Row { Id = 3, DueDate = DateTimeOffset.Parse("2026-10-14T18:29:59Z") },
            new Row { Id = 4, DueDate = DateTimeOffset.Parse("2026-10-14T18:30:00Z") }
        }.AsQueryable();
        ChargeColumnFilter.Apply(rows, filters, Fields).Select(r => r.Id).ToArray().ShouldBe(new[] { 2, 3 });
    }

    [Fact]
    public void Status_Filter_Uses_The_Batch_Workflow()
    {
        var filters = ChargeColumnFilter.Parse("""[{"column":"workflowStatus","type":"equals","value":0}]""");
        ChargeColumnFilter.MatchesStatus(filters, ChargeWorkflowStatus.Pending).ShouldBeTrue();
        ChargeColumnFilter.MatchesStatus(filters, ChargeWorkflowStatus.Completed).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"column\":\"actions\",\"type\":\"equals\",\"value\":0}]")]
    [InlineData("[{\"column\":\"workflowStatus\",\"type\":\"equals\",\"value\":9}]")]
    [InlineData("[{\"column\":\"amount\",\"type\":\"between\",\"value\":{\"min\":10,\"max\":1}}]")]
    [InlineData("[{\"column\":\"dueDate\",\"type\":\"equals\",\"value\":\"bad\"}]")]
    public void Invalid_Filters_Are_Rejected(string json) =>
        Should.Throw<AbpValidationException>(() => ChargeColumnFilter.Parse(json));

    [Theory]
    [InlineData("[{\"column\":\"categoryName\",\"type\":\"startsWith\",\"value\":\"Trip\"}]")]
    [InlineData("[{\"column\":\"studentCount\",\"type\":\"between\",\"value\":{\"min\":2,\"max\":10}}]")]
    [InlineData("[{\"column\":\"amount\",\"type\":\"greaterThan\",\"value\":1000}]")]
    [InlineData("[{\"column\":\"dueDate\",\"type\":\"before\",\"value\":\"2026-10-14T00:00:00+05:30\"}]")]
    public void Predicates_Translate_To_Postgres(string json)
    {
        using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only").Options);
        var query = ChargeColumnFilter.Apply(context.Set<Row>(), ChargeColumnFilter.Parse(json), Fields);
        var sql = query.OrderBy(r => r.Id).Skip(20).Take(20).ToQueryString();
        sql.ShouldContain("WHERE");
        sql.ShouldContain("LIMIT");
        sql.ShouldContain("OFFSET");
    }

    private sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder) => builder.Entity<Row>().HasKey(r => r.Id);
    }
}
