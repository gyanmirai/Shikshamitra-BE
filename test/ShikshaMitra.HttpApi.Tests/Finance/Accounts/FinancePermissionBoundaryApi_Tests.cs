using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceRoutingApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FinancePermissionBoundaryApi_Tests(FinanceRoutingApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("ShikshaMitra.Module.Finance.Fees.Settings")]
	[InlineData("Finance.Settings.ReceiptBook.Add")]
	[InlineData("Finance.Settings.ReceiptBook.Delete")]
	public async Task ReceiptBook_Reads_Require_Exact_View(string permission)
	{
		using var client = RestrictedClient(permission);
		var list = await client.GetAsync($"/api/receipt-books?academicYearId={Factory.SeededAcademicYearId}");
		list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var detail = await client.GetAsync($"/api/receipt-books/{Guid.NewGuid()}");
		detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task ReceiptBook_View_Allows_Read_But_Does_Not_Grant_Writes()
	{
		using var client = RestrictedClient("Finance.Settings.ReceiptBook.View");
		var read = await client.GetAsync($"/api/receipt-books?academicYearId={Factory.SeededAcademicYearId}");
		read.StatusCode.ShouldBe(HttpStatusCode.OK);
		var id = Guid.NewGuid();
		var body = new {
			BoardId = Guid.NewGuid(), MediumId = Guid.NewGuid(), MediumStageId = Guid.NewGuid(),
			Name = "Permission test", Prefix = "QA", Suffix = "", StartingNumber = 1, EndingNumber = 100,
			Description = "Authorization must reject this before accessing a record."
		};
		var create = await client.PostAsJsonAsync($"/api/receipt-books?academicYearId={Factory.SeededAcademicYearId}", body);
		create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var edit = await client.PutAsJsonAsync($"/api/receipt-books/{id}", body);
		edit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var status = await client.PatchAsJsonAsync($"/api/receipt-books/{id}/status", new { isActive = false });
		status.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var delete = await client.DeleteAsync($"/api/receipt-books/{id}");
		delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("ShikshaMitra.Module.Finance.AuditTrail")]
	[InlineData("Finance.AuditTrail.View")]
	[InlineData("Finance.Fees.FeeCollections.View")]
	public async Task Other_Finance_Read_Grants_Do_Not_Grant_Audit_Access(string permission)
	{
		using var client = RestrictedClient(permission);
		var response = await client.GetAsync($"/api/finance/audit-events?AcademicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Exact_Audit_View_Allows_The_Audit_Workspace()
	{
		using var client = RestrictedClient("Finance.Auditing.View");
		var response = await client.GetAsync($"/api/finance/audit-events?AcademicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private HttpClient RestrictedClient(string permission)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		return client;
	}
}
