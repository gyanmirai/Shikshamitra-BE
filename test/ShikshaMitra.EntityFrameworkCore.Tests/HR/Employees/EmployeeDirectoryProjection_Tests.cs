using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Search;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Employees;

public class EmployeeDirectoryProjection_Tests(ITestOutputHelper output) : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Directory_page_should_select_display_columns_without_employee_graphs_in_PostgreSQL()
	{
		var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only;Timeout=1")
			.Options;
		using var context = new ShikshaMitraDbContext(options)
		{
			LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
		};
		var page = context.Set<Employee>().OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
			.ThenBy(e => e.Id).Skip(10).Take(10);
		var baselineSql = page.ToQueryString();
		var directorySql = page.Select(EmployeeSearchProjection.Summary).ToQueryString();
		output.WriteLine($"Full employee SQL: {baselineSql.Length} characters; directory SQL: {directorySql.Length} characters.");
		baselineSql.ShouldContain("EmployeeDocuments");
		foreach (var unusedData in new[] { "EmployeeDocuments", "WorkExperiences", "BankDetails", "PanNo", "AdharNo", "ExtraProperties", "ConcurrencyStamp" })
		{
			directorySql.ShouldNotContain(unusedData);
		}
		directorySql.ShouldContain("LIMIT");
		directorySql.ShouldContain("OFFSET");
		directorySql.ShouldContain("ShiftName");
		directorySql.ShouldContain("StartTime");
		directorySql.ShouldContain("EndTime");
		directorySql.ShouldContain("Roles");
		directorySql.ShouldContain("TenantId");
		directorySql.ShouldContain("IsDeleted");
	}
}
