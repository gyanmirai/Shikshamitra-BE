using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrReportingTableQuery_Tests
{
	private static readonly List<HrReportFieldDto> Columns = [
		new() { Id = "name", Type = "string" }, new() { Id = "amount", Type = "money" },
		new() { Id = "date", Type = "date" }, new() { Id = "at", Type = "datetime" }, new() { Id = "active", Type = "boolean" } ];
	private static HrReportTableFilterDto Filter(string column, string type, object value) => new() { Column = column, Type = type, Value = JsonSerializer.SerializeToElement(value) };
	private static List<Dictionary<string, object?>> Apply(HrReportTableQueryDto query, params Dictionary<string, object?>[] rows) => new HrReportingTableQuery(query, Columns, "Asia/Kolkata").Apply(rows, row => row).ToList();
	[Fact]
	public void Numeric_Sorting_And_Ranges_Should_Apply_Before_Paging_With_Stable_Ties()
	{
		var rows = Enumerable.Range(0, 301).Select(i => new Dictionary<string, object?> { ["name"] = i.ToString(), ["amount"] = i % 100 }).ToArray();
		var matching = Apply(new() { Filters = [Filter("amount", "between", new { min = 20, max = 29 })], Sorts = [new() { Column = "amount", Direction = "desc" }] }, rows);
		matching.Count.ShouldBe(30);
		matching.Take(3).Select(row => row["name"]).ShouldBe(new object?[] { "29", "129", "229" });
		matching.Skip(20).First()["amount"].ShouldBe(23);
	}
	[Fact]
	public void Filters_Should_Union_One_Column_And_Intersect_Different_Columns()
	{
		var query = new HrReportTableQueryDto { Search = "AL", Filters = [Filter("name", "startsWith", "a"), Filter("name", "equals", "Beta"), Filter("amount", "greaterThan", 5)] };
		Apply(query, new Dictionary<string, object?>() { ["name"] = "Alpha", ["amount"] = 10 }, new Dictionary<string, object?>() { ["name"] = "Almond", ["amount"] = 2 }, new Dictionary<string, object?>() { ["name"] = "Beta", ["amount"] = 10 }).Count.ShouldBe(1);
	}
	[Fact]
	public void False_And_Zero_Should_Remain_Distinct_From_Missing_Values()
	{
		var rows = new Dictionary<string, object?>[] { new Dictionary<string, object?>() { ["active"] = false, ["amount"] = 0 }, new Dictionary<string, object?>() { ["active"] = null, ["amount"] = null }, new Dictionary<string, object?>() { ["active"] = true, ["amount"] = 10 } };
		Apply(new() { Filters = [Filter("active", "equals", false), Filter("amount", "equals", 0)] }, rows).Count.ShouldBe(1);
		Apply(new() { Search = "No" }, rows).Count.ShouldBe(1);
	}
	[Theory]
	[InlineData("date", "equals", "21-10-2026")]
	[InlineData("at", "equals", "2026-10-21T04:00:45Z")]
	[InlineData("at", "equals", "21-10-2026 09:30")]
	[InlineData("at", "after", "21-10-2026 09:30")]
	[InlineData("at", "before", "21-10-2026 09:30")]
	public void Dates_Should_Use_School_Time_And_Shared_Table_Inclusive_Units(string column, string operation, string value)
	{
		Apply(new() { Filters = [Filter(column, operation, value)] }, new Dictionary<string, object?>() { ["date"] = "2026-10-21", ["at"] = "2026-10-21T04:00:45Z" }).Count.ShouldBe(1);
	}
	[Theory]
	[InlineData("amount", "contains", "1")]
	[InlineData("amount", "equals", "bad")]
	[InlineData("active", "equals", "")]
	[InlineData("hiddenSecret", "equals", "x")]
	[InlineData("date", "equals", "31-02-2026")]
	public void Invalid_Columns_Operators_And_Typed_Values_Should_Be_Rejected(string column, string operation, string value) =>
		Should.Throw<BusinessException>(() => Apply(new() { Filters = [Filter(column, operation, value)] }));
	[Fact]
	public void Malformed_Queries_And_Reversed_Ranges_Should_Be_Rejected()
	{
		Should.Throw<BusinessException>(() => HrReportTableQueryDto.Parse("{broken"));
		Should.Throw<BusinessException>(() => Apply(HrReportTableQueryDto.Parse("{\"filters\":null}")));
		Should.Throw<BusinessException>(() => Apply(new() { Sorts = [new() { Column = "name;drop table" }] }));
		Should.Throw<BusinessException>(() => Apply(new() { Filters = [Filter("amount", "between", new { min = 20, max = 10 })] }));
	}
	[Fact]
	public void Timestamp_Sorting_Should_Remain_Chronological_Across_Daylight_Saving_Changes()
	{
		var query = new HrReportingTableQuery(new() { Sorts = [new() { Column = "at" }] }, Columns, "Europe/London");
		var rows = new List<Dictionary<string, object?>> {
			new() { ["at"] = "2026-10-25T01:15:00Z" }, new() { ["at"] = "2026-10-25T00:30:00Z" } };
		query.Apply(rows, row => row).First()["at"].ShouldBe("2026-10-25T00:30:00Z");
	}
}
