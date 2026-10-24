using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Finance;

public class FinanceTableQuery_Tests
{
    public sealed class Row
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string ClassDivision { get; set; } = "";
        public decimal Amount { get; set; }
        public int? RollNumber { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    private static readonly Dictionary<string, FinanceTableColumn> Columns = new()
    {
        ["name"] = new(FinanceTableValueType.Text), ["classDivision"] = new(FinanceTableValueType.Text),
        ["amount"] = new(FinanceTableValueType.Number), ["status"] = new(FinanceTableValueType.Enum, true, [0, 1, 2, 7]),
        ["createdAt"] = new(FinanceTableValueType.Date),
        ["rollNumber"] = new(FinanceTableValueType.Number)
    };
    private static readonly Dictionary<string, LambdaExpression> Fields = new()
    {
        ["name"] = (Expression<Func<Row, string>>)(r => r.Name), ["classDivision"] = (Expression<Func<Row, string>>)(r => r.ClassDivision),
        ["amount"] = (Expression<Func<Row, decimal>>)(r => r.Amount), ["status"] = (Expression<Func<Row, int>>)(r => r.Status),
        ["createdAt"] = (Expression<Func<Row, DateTime>>)(r => r.CreatedAt),
        ["rollNumber"] = (Expression<Func<Row, int?>>)(r => r.RollNumber)
    };
    private static FinanceTableQuery Query(string filters, string? sort = null, string? order = null) =>
        FinanceTableQuery.Parse(filters, null, sort, order, Columns);

    [Theory]
    [InlineData("asc", 0, 7)]
    [InlineData("desc", 7, 0)]
    public void Status_Sorting_Is_Applied_Before_Paging(string direction, int first, int last)
    {
        var rows = new[] { new Row { Status = 7 }, new Row { Status = 1 }, new Row { Status = 0 } }.AsQueryable();
        var sorted = Query("[]", "status", direction).Order(rows, Fields, "name", false, r => r.Id);
        sorted.Take(1).Single().Status.ShouldBe(first);
        sorted.Last().Status.ShouldBe(last);
    }

    [Fact]
    public void Filters_Use_Display_Values_And_Count_Before_Paging()
    {
        var query = Query("""
            [{"column":"classDivision","type":"contains","value":"B"},
             {"column":"name","type":"contains","value":"1001"},
             {"column":"name","type":"contains","value":"1003"},
             {"column":"amount","type":"between","value":{"min":100,"max":500}},
             {"column":"status","type":"equals","value":0}]
            """, "amount", "desc");
        var rows = new[] {
            new Row { Id = Guid.NewGuid(), Name = "Asha (1001)", ClassDivision = "Class 4 (B)", Amount = 100, Status = 0 },
            new Row { Id = Guid.NewGuid(), Name = "Ravi (1002)", ClassDivision = "Class 4 (B)", Amount = 300, Status = 1 },
            new Row { Id = Guid.NewGuid(), Name = "Maya (1003)", ClassDivision = "Class 4 (B)", Amount = 500, Status = 0 }
        }.AsQueryable();
        var filtered = query.Apply(rows, Fields);
        filtered.Count().ShouldBe(2);
        query.Order(filtered, Fields, "name", false, r => r.Id).Skip(1).Take(1).Single().Name.ShouldBe("Asha (1001)");
    }

    [Fact]
    public void Sorting_Uses_A_Stable_Identity_Tie_Breaker()
    {
        var ids = new[] { Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000001") };
        var rows = ids.Select(id => new Row { Id = id, Amount = 100 }).AsQueryable();
        Query("[]", "amount", "desc").Order(rows, Fields, "name", false, r => r.Id).Select(r => r.Id).ToArray().ShouldBe(ids.Reverse().ToArray());
    }

    [Fact]
    public void Date_Filter_Uses_The_Selected_Local_Day_In_UTC()
    {
        var rows = new[] { new Row { CreatedAt = DateTime.Parse("2026-10-13T18:29:59Z").ToUniversalTime() },
            new Row { CreatedAt = DateTime.Parse("2026-10-13T18:30:00Z").ToUniversalTime() },
            new Row { CreatedAt = DateTime.Parse("2026-10-14T18:29:59Z").ToUniversalTime() },
            new Row { CreatedAt = DateTime.Parse("2026-10-14T18:30:00Z").ToUniversalTime() } }.AsQueryable();
        Query("""[{"column":"createdAt","type":"equals","value":"2026-10-14T00:00:00+05:30"}]""").Apply(rows, Fields).Count().ShouldBe(2);
    }

    [Theory]
    [InlineData("pending", 6, 0)]
    [InlineData("approved", 1, 4)]
    [InlineData("rejected", 2, 3)]
    public void Operational_Tabs_Contain_Exact_Lifecycle_Statuses(string tab, int first, int second)
    {
        var statuses = ConcessionApplicationsTable.Statuses(tab);
        statuses.ShouldContain((ConcessionRequestStatus)first);
        statuses.ShouldContain((ConcessionRequestStatus)second);
        if (tab == "approved") statuses.ShouldContain(ConcessionRequestStatus.Reversed);
    }

    [Theory]
    [InlineData("[{\"column\":\"actions\",\"type\":\"equals\",\"value\":1}]")]
    [InlineData("[{\"column\":\"status\",\"type\":\"equals\",\"value\":99}]")]
    [InlineData("[{\"column\":\"amount\",\"type\":\"between\",\"value\":{\"min\":5,\"max\":1}}]")]
    [InlineData("[{\"column\":\"createdAt\",\"type\":\"contains\",\"value\":\"2026\"}]")]
    [InlineData("null")]
    public void Invalid_Filters_Are_Rejected(string filters) => Should.Throw<AbpValidationException>(() => Query(filters));

    [Fact]
    public void Invalid_Sort_And_Tab_Are_Rejected()
    {
        Should.Throw<AbpValidationException>(() => Query("[]", "actions", "asc"));
        Should.Throw<AbpValidationException>(() => Query("[]", "name", "unknown"));
        Should.Throw<AbpValidationException>(() => ConcessionApplicationsTable.Statuses("unknown"));
    }

    private sealed class ProbeDb(DbContextOptions<ProbeDb> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }
    [Theory]
    [InlineData("equals", "2", 1)]
    [InlineData("lessThan", "10", 1)]
    [InlineData("between", "{\"min\":2,\"max\":12}", 2)]
    public void Missing_Roll_Numbers_Do_Not_Match_Numeric_Filters(string operation, string value, int count)
    {
        var query = Query($$"""[{"column":"rollNumber","type":"{{operation}}","value":{{value}}}]""", "rollNumber", "asc");
        var rows = new[] { new Row { RollNumber = null }, new Row { RollNumber = 12 }, new Row { RollNumber = 2 } }.AsQueryable();
        var filtered = query.Apply(rows, Fields);
        filtered.Count().ShouldBe(count);
        query.Order(filtered, Fields, "name", false, r => r.Id).First().RollNumber.ShouldBe(2);
        using var db = new ProbeDb(new DbContextOptionsBuilder<ProbeDb>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        query.Apply(db.Rows, Fields).ToQueryString().ShouldContain("IS NOT NULL");
    }
    [Fact]
    public void PostgreSql_Translates_Filters_Sorting_And_Date_Bounds()
    {
        using var db = new ProbeDb(new DbContextOptionsBuilder<ProbeDb>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var query = Query("""[{"column":"name","type":"contains","value":"asha"},{"column":"amount","type":"between","value":{"min":100,"max":500}},{"column":"status","type":"equals","value":0},{"column":"createdAt","type":"equals","value":"2026-10-14T00:00:00+05:30"}]""", "amount", "desc");
        var sql = query.Order(query.Apply(db.Rows, Fields), Fields, "name", false, r => r.Id).Skip(10).Take(10).ToQueryString();
        sql.ShouldContain("ORDER BY"); sql.ShouldContain("DESC"); sql.ShouldContain("OFFSET");
        sql.ShouldContain("2026-10-13T18:30:00"); sql.ShouldContain("lower");
    }

    [Fact]
    public void Sqlite_Executes_Zero_Status_And_Nullable_Roll_Number_Filters()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new ProbeDb(new DbContextOptionsBuilder<ProbeDb>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Rows.AddRange(new Row { Id = Guid.NewGuid(), Status = 0, RollNumber = 2 },
            new Row { Id = Guid.NewGuid(), Status = 1, RollNumber = 12 },
            new Row { Id = Guid.NewGuid(), Status = 0, RollNumber = null });
        db.SaveChanges();
        Query("""[{"column":"status","type":"equals","value":0},{"column":"rollNumber","type":"between","value":{"min":1.5,"max":12}}]""")
            .Apply(db.Rows, Fields).Single().RollNumber.ShouldBe(2);
        Query("""[{"column":"rollNumber","type":"equals","value":2.5}]""").Apply(db.Rows, Fields).Count().ShouldBe(0);
    }
}
