using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionOfferingColumnFilters_Tests
{
	private static IQueryable<AdmissionOfferingColumnFilterRow> Rows => new[]
	{
		new AdmissionOfferingColumnFilterRow { Id = Guid.NewGuid(), Name = "Spring intake", EffectiveOpenDate = new(2027, 1, 1), EffectiveCloseDate = new(2027, 3, 31), ApplicationCount = 0 },
		new AdmissionOfferingColumnFilterRow { Id = Guid.NewGuid(), Name = "Spring special", EffectiveOpenDate = new(2027, 2, 1), EffectiveCloseDate = new(2027, 4, 30), ApplicationCount = 12 },
		new AdmissionOfferingColumnFilterRow { Id = Guid.NewGuid(), Name = "Autumn intake", EffectiveOpenDate = new(2027, 8, 1), EffectiveCloseDate = new(2027, 9, 30), ApplicationCount = 40 }
	}.AsQueryable();

	[Theory]
	[InlineData("name", "contains", "SPRING", "string", 2)]
	[InlineData("name", "equals", "Spring intake", "string", 1)]
	[InlineData("name", "startsWith", "Spring", "string", 2)]
	[InlineData("name", "endsWith", "intake", "string", 2)]
	[InlineData("effectiveOpenDate", "equals", "01-02-2027", "date", 1)]
	[InlineData("effectiveOpenDate", "after", "2027-01-01", "date", 2)]
	[InlineData("effectiveOpenDate", "before", "01-02-2027", "date", 1)]
	[InlineData("effectiveCloseDate", "equals", "31-03-2027", "date", 1)]
	[InlineData("effectiveCloseDate", "after", "30-04-2027", "date", 1)]
	[InlineData("effectiveCloseDate", "before", "30-04-2027", "date", 1)]
	[InlineData("applicationCount", "equals", "12", "number", 1)]
	[InlineData("applicationCount", "greaterThan", "12", "number", 1)]
	[InlineData("applicationCount", "lessThan", "12", "number", 1)]
	public void Applies_supported_predicates(string column, string operation, string value, string valueType, int count)
	{
		var filters = System.Text.Json.JsonSerializer.Serialize(new[] { new { column, type = operation, value, valueType } });
		AdmissionOfferingColumnFilters.Apply(Rows, filters).Count().ShouldBe(count);
	}

	[Fact]
	public void Combines_date_name_and_numeric_range_before_counting_and_paging()
	{
		var filtered = AdmissionOfferingColumnFilters.Apply(Rows, """
			[{"column":"name","type":"startsWith","value":"Spring","valueType":"string"},
			 {"column":"effectiveOpenDate","type":"before","value":"01-08-2027","valueType":"date"},
			 {"column":"applicationCount","type":"between","value":{"min":0,"max":12},"valueType":"number"}]
			""");
		filtered.Count().ShouldBe(2);
		filtered.OrderBy(row => row.EffectiveOpenDate).Skip(1).Take(1).Single().Name.ShouldBe("Spring special");
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("[null]")]
	[InlineData("[{\"column\":\"actions\",\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"greaterThan\",\"value\":\"x\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"name\",\"type\":\"contains\",\"value\":\"\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"effectiveOpenDate\",\"type\":\"equals\",\"value\":\"31-02-2027\",\"valueType\":\"date\"}]")]
	[InlineData("[{\"column\":\"effectiveOpenDate\",\"type\":\"contains\",\"value\":\"01-02-2027\",\"valueType\":\"date\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":1.5,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":-1,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":2147483648,\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"between\",\"value\":{\"min\":3,\"max\":2},\"valueType\":\"number\"}]")]
	[InlineData("[{\"column\":\"applicationCount\",\"type\":\"equals\",\"value\":2,\"valueType\":\"string\"}]")]
	public void Rejects_malformed_or_unsupported_criteria(string filters) =>
		Should.Throw<BusinessException>(() => AdmissionOfferingColumnFilters.Apply(Rows, filters)).Code.ShouldBe("Students:Admissions:InvalidOfferingFilter");

	[Fact]
	public void Rejects_unbounded_input_and_preserves_unfiltered_results()
	{
		AdmissionOfferingColumnFilters.Apply(Rows, null).Count().ShouldBe(3);
		AdmissionOfferingColumnFilters.Apply(Rows, "[]").Count().ShouldBe(3);
		var criteria = string.Join(",", Enumerable.Repeat("{\"column\":\"name\",\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}", 21));
		Should.Throw<BusinessException>(() => AdmissionOfferingColumnFilters.Apply(Rows, "[" + criteria + "]"));
	}

	[Fact]
	public void Every_predicate_translates_to_postgres_without_client_evaluation()
	{
		using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseNpgsql("Host=localhost;Database=translation_only").Options);
		var query = AdmissionOfferingColumnFilters.Apply(context.Set<AdmissionOfferingColumnFilterRow>(), """
			[{"column":"name","type":"contains","value":"Spring","valueType":"string"},
			 {"column":"effectiveOpenDate","type":"after","value":"01-01-2027","valueType":"date"},
			 {"column":"effectiveCloseDate","type":"before","value":"01-01-2028","valueType":"date"},
			 {"column":"applicationCount","type":"greaterThan","value":0,"valueType":"number"}]
			""");
		query.OrderBy(row => row.Id).Skip(1).Take(1).ToQueryString().ShouldContain("WHERE");
	}

	private sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<AdmissionOfferingColumnFilterRow>().HasKey(row => row.Id);
			modelBuilder.Entity<OfferingSource>().HasKey(row => row.Id);
		}
	}

	[Fact]
	public void Inherited_projection_filters_fallback_name_and_effective_calendar_dates_in_sql()
	{
		using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseNpgsql("Host=localhost;Database=translation_only").Options);
		var projected = context.Set<OfferingSource>().Select(source => new OfferingProjection
		{
			Id = source.Id,
			Name = source.Name == "" ? source.Code : source.Name,
			EffectiveOpenDate = source.OpenDate.HasValue ? source.OpenDate.Value.Date : source.CycleStartDate.Date,
			EffectiveCloseDate = source.CycleEndDate.Date,
			ApplicationCount = source.ApplicationCount
		});
		var filtered = AdmissionOfferingColumnFilters.Apply(projected, """
			[{"column":"name","type":"startsWith","value":"General","valueType":"string"},
			 {"column":"effectiveOpenDate","type":"equals","value":"01-01-2027","valueType":"date"},
			 {"column":"applicationCount","type":"between","value":{"min":0,"max":12},"valueType":"number"}]
			""");
		var sql = filtered.OrderBy(row => row.Id).Skip(1).Take(1).ToQueryString();
		sql.ShouldContain("WHERE");
		sql.ShouldContain("CASE");
		sql.ShouldContain("date_trunc");
		sql.ShouldContain("LIMIT");
	}

	private sealed class OfferingProjection : AdmissionOfferingColumnFilterRow;
	private sealed class OfferingSource
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = "";
		public string Code { get; set; } = "";
		public DateTimeOffset? OpenDate { get; set; }
		public DateTimeOffset CycleStartDate { get; set; }
		public DateTimeOffset CycleEndDate { get; set; }
		public int ApplicationCount { get; set; }
	}
}
