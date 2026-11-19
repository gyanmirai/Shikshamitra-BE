using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCycleColumnFilters_Tests
{
	private static IQueryable<AdmissionCycleColumnFilterRow> Rows => new[]
	{
		new AdmissionCycleColumnFilterRow { Id = Guid.NewGuid(), Name = "Spring intake", StartDate = new(2027, 1, 1), EndDate = new(2027, 3, 31), LinkedOfferingCount = 0, ApplicationCount = 0 },
		new AdmissionCycleColumnFilterRow { Id = Guid.NewGuid(), Name = "Spring special", StartDate = new(2027, 2, 1), EndDate = new(2027, 4, 30), LinkedOfferingCount = 2, ApplicationCount = 12 },
		new AdmissionCycleColumnFilterRow { Id = Guid.NewGuid(), Name = "Autumn intake", StartDate = new(2027, 8, 1), EndDate = new(2027, 9, 30), LinkedOfferingCount = 5, ApplicationCount = 40 }
	}.AsQueryable();

	[Theory]
	[InlineData("name", "contains", "SPRING", "string", 2)]
	[InlineData("name", "equals", "Spring intake", "string", 1)]
	[InlineData("name", "startsWith", "Spring", "string", 2)]
	[InlineData("name", "endsWith", "intake", "string", 2)]
	[InlineData("startDate", "equals", "01-02-2027", "date", 1)]
	[InlineData("startDate", "after", "2027-01-01", "date", 2)]
	[InlineData("startDate", "before", "01-02-2027", "date", 1)]
	[InlineData("endDate", "equals", "31-03-2027", "date", 1)]
	[InlineData("endDate", "after", "30-04-2027", "date", 1)]
	[InlineData("endDate", "before", "30-04-2027", "date", 1)]
	[InlineData("linkedOfferingCount", "equals", "0", "number", 1)]
	[InlineData("linkedOfferingCount", "greaterThan", "2", "number", 1)]
	[InlineData("linkedOfferingCount", "lessThan", "2", "number", 1)]
	[InlineData("applicationCount", "equals", "12", "number", 1)]
	[InlineData("applicationCount", "greaterThan", "12", "number", 1)]
	[InlineData("applicationCount", "lessThan", "12", "number", 1)]
	public void Applies_supported_predicates(string column, string operation, string value, string valueType, int count)
	{
		var filters = System.Text.Json.JsonSerializer.Serialize(new[] { new { column, type = operation, value, valueType } });
		AdmissionCycleColumnFilters.Apply(Rows, filters).Count().ShouldBe(count);
	}

	[Fact]
	public void Combines_date_name_and_numeric_range_before_counting_and_paging()
	{
		var filtered = AdmissionCycleColumnFilters.Apply(Rows, """
			[{"column":"name","type":"startsWith","value":"Spring","valueType":"string"},
			 {"column":"startDate","type":"before","value":"01-08-2027","valueType":"date"},
			 {"column":"applicationCount","type":"between","value":{"min":0,"max":12},"valueType":"number"}]
			""");
		filtered.Count().ShouldBe(2);
		filtered.OrderBy(row => row.StartDate).Skip(1).Take(1).Single().Name.ShouldBe("Spring special");
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("[null]")]
	[InlineData("[{\"column\":\"actions\",\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"greaterThan\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"contains\",\"value\":\"\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"startDate\",\"type\":\"equals\",\"value\":\"31-02-2027\",\"valueType\":\"date\"}]")]
	[InlineData("[{\"column\":\"startDate\",\"type\":\"contains\",\"value\":\"01-02-2027\",\"valueType\":\"date\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":1.5,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":-1,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":2147483648,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"between\",\"value\":{\"min\":3,\"max\":2},\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":2,\"valueType\":\"string\"}]")]
	public void Rejects_malformed_or_unsupported_criteria(string filters) =>
		Should.Throw<BusinessException>(() => AdmissionCycleColumnFilters.Apply(Rows, filters)).Code.ShouldBe("Students:Admissions:InvalidCycleFilter");

	[Fact]
	public void Rejects_unbounded_input_and_preserves_unfiltered_results()
	{
		AdmissionCycleColumnFilters.Apply(Rows, null).Count().ShouldBe(3);
		AdmissionCycleColumnFilters.Apply(Rows, "[]").Count().ShouldBe(3);
		var criteria = string.Join(",", Enumerable.Repeat("{\"column\":\"name\",\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}", 21));
		Should.Throw<BusinessException>(() => AdmissionCycleColumnFilters.Apply(Rows, "[" + criteria + "]"));
	}

	[Fact]
	public void Every_predicate_translates_to_postgres_without_client_evaluation()
	{
		using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseNpgsql("Host=localhost;Database=translation_only").Options);
		var query = AdmissionCycleColumnFilters.Apply(context.Set<AdmissionCycleColumnFilterRow>(), """
			[{"column":"name","type":"contains","value":"Spring","valueType":"string"},
			 {"column":"startDate","type":"after","value":"01-01-2027","valueType":"date"},
			 {"column":"endDate","type":"before","value":"01-01-2028","valueType":"date"},
			 {"column":"linkedOfferingCount","type":"between","value":{"min":0,"max":2},"valueType":"number"},
			 {"column":"applicationCount","type":"greaterThan","value":0,"valueType":"number"}]
			""");
		query.OrderBy(row => row.Id).Skip(1).Take(1).ToQueryString().ShouldContain("WHERE");
	}

	private sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<AdmissionCycleColumnFilterRow>().HasKey(row => row.Id);
	}
}
