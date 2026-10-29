using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Documents;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Employees;

public class EmployeeDocumentProjection_Tests(ITestOutputHelper output) : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Metadata_and_selected_download_translate_without_the_employee_graph_in_PostgreSQL()
	{
		// Translation only: this unusable connection is never opened.
		var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only;Timeout=1")
			.Options;
		using var context = new ShikshaMitraDbContext(options)
		{
			LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
		};
		var employeeId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var definitionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
		var baselineSql = context.Set<Employee>().Where(employee => employee.Id == employeeId).ToQueryString();
		var documents = EmployeeDocumentReadItem.Query(context.Set<Employee>(), employeeId);
		var selectedSql = documents.Where(document => document.Id == documentId).Take(1).ToQueryString();
		var checklistSql = documents.Where(document => document.DocumentNameId.HasValue &&
			Enumerable.Contains(definitionIds, document.DocumentNameId.Value)).ToQueryString();
		output.WriteLine($"Post-authorization metadata SQL: full employee {baselineSql.Length} characters; selected document {selectedSql.Length}; checklist {checklistSql.Length}.");
		baselineSql.ShouldContain("WorkExperiences");
		foreach (var sql in new[] { selectedSql, checklistSql })
		{
			sql.ShouldContain("EmployeeDocuments");
			sql.ShouldContain("TenantId");
			sql.ShouldContain("IsDeleted");
			foreach (var unused in new[] { "WorkExperiences", "BankDetails", "PanNo", "AdharNo", "ExtraProperties", "ConcurrencyStamp", "JOIN \"AbpRoles\"", "JOIN \"AppShift" })
				sql.ShouldNotContain(unused);
		}
		selectedSql.ShouldContain("LIMIT");
		selectedSql.ShouldContain(documentId.ToString());
		checklistSql.ShouldContain("DocumentNameId");
		checklistSql.ShouldContain("ANY");
	}
}
