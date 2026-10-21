using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Records;

public class StudentDirectoryColumnFilters_Tests
{
	private static IQueryable<StudentDirectoryFilterRow> Rows => new[]
	{
		new StudentDirectoryFilterRow { StudentId = Guid.NewGuid(), Name = "Riya Shah", GrNumber = "GR-000001", ClassDivision = "Class 1 / A", RollNo = 2, Status = StudentLifeCycleStatusEnum.Enrolled },
		new StudentDirectoryFilterRow { StudentId = Guid.NewGuid(), Name = "Riya Patel", GrNumber = "GR-000002", ClassDivision = "Class 1 / B", RollNo = 7, Status = StudentLifeCycleStatusEnum.Left },
		new StudentDirectoryFilterRow { StudentId = Guid.NewGuid(), Name = "Aarav Shah", GrNumber = "GR-000003", ClassDivision = "Class 2 / A", RollNo = null, Status = StudentLifeCycleStatusEnum.InActive }
	}.AsQueryable();

	[Theory]
	[InlineData("name", "contains", "RIYA", 2)]
	[InlineData("name", "equals", "Riya Shah", 1)]
	[InlineData("name", "startsWith", "Riya", 2)]
	[InlineData("name", "endsWith", "Shah", 2)]
	[InlineData("grNumber", "equals", "GR-000002", 1)]
	[InlineData("classDivision", "contains", "Class 1", 2)]
	[InlineData("classDivision", "endsWith", "/ A", 2)]
	[InlineData("rollNo", "equals", "2", 1)]
	[InlineData("rollNo", "greaterThan", "2.5", 1)]
	[InlineData("rollNo", "lessThan", "7", 1)]
	[InlineData("status", "equals", "6", 1)]
	[InlineData("status", "equals", "7", 1)]
	[InlineData("status", "equals", "8", 1)]
	public void Applies_supported_operators(string column, string operation, string value, int count) =>
		StudentDirectoryColumnFilters.Apply(Rows, [new() { Column = column, Operator = operation, Value = value }]).Count().ShouldBe(count);

	[Fact]
	public void Combines_criteria_before_counting_and_paging_and_excludes_missing_rolls()
	{
		var filtered = StudentDirectoryColumnFilters.Apply(Rows, [
			new() { Column = "name", Operator = "startsWith", Value = "Riya" },
			new() { Column = "rollNo", Operator = "between", Min = 2, Max = 7 }
		]);
		filtered.Count().ShouldBe(2);
		filtered.OrderBy(row => row.GrNumber).Skip(1).Take(1).Single().Name.ShouldBe("Riya Patel");
	}

	[Theory]
	[InlineData("actions", "contains", "Riya")]
	[InlineData("name", "greaterThan", "Riya")]
	[InlineData("rollNo", "equals", "NaN")]
	[InlineData("rollNo", "equals", "2147483648")]
	[InlineData("status", "contains", "6")]
	[InlineData("status", "equals", "99")]
	public void Rejects_unsupported_columns_operators_and_values(string column, string operation, string value) =>
		Should.Throw<BusinessException>(() => StudentDirectoryColumnFilters.Apply(Rows, [new() { Column = column, Operator = operation, Value = value }]))
			.Code.ShouldBe("Students:Records:InvalidDirectoryFilter");

	[Fact]
	public void Rejects_invalid_ranges_and_unbounded_filter_lists()
	{
		Should.Throw<BusinessException>(() => StudentDirectoryColumnFilters.Apply(Rows, [new() { Column = "rollNo", Operator = "between", Min = 8, Max = 2 }]));
		Should.Throw<BusinessException>(() => StudentDirectoryColumnFilters.Validate(Enumerable.Range(0, 21).Select(_ => new StudentDirectoryColumnFilterDto()).ToArray()));
	}

	[Fact]
	public void Every_filter_translates_to_postgres_sql_without_client_evaluation()
	{
		using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseNpgsql("Host=localhost;Database=translation_only").Options);
		var query = StudentDirectoryColumnFilters.Apply(context.Set<StudentDirectoryFilterRow>(), [
			new() { Column = "name", Operator = "contains", Value = "Riya" },
			new() { Column = "grNumber", Operator = "startsWith", Value = "GR" },
			new() { Column = "classDivision", Operator = "endsWith", Value = "A" },
			new() { Column = "rollNo", Operator = "between", Min = 2, Max = 7 },
			new() { Column = "status", Operator = "equals", Value = "6" }
		]);
		query.OrderBy(row => row.StudentId).Skip(1).Take(1).ToQueryString().ShouldContain("WHERE");
	}

	private sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<StudentDirectoryFilterRow>().HasKey(row => row.StudentId);
	}
}
