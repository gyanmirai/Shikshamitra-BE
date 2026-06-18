using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(ApiTestCollection.Name)]
public class LibraryReleaseGateApi_Tests : ShikshaMitraApiTestBase
{
	public LibraryReleaseGateApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	public static IEnumerable<object[]> CanonicalGetRoutes()
	{
		yield return ["/api/library/dashboard/overview?academicYearId={academicYearId}"];
		yield return ["/api/library/books?academicYearId={academicYearId}&page=1&pageSize=5"];
		yield return ["/api/library/issue-returns?page=1&pageSize=5"];
		yield return ["/api/library/members?page=1&pageSize=5"];
		yield return ["/api/library/charges?page=1&pageSize=5"];
		yield return ["/api/library/reports/options"];
		yield return ["/api/library/reports/exports?skipCount=0&maxResultCount=5"];
		yield return ["/api/library/notifications/settings"];
		yield return ["/api/library/notifications/history?page=1&pageSize=5"];
		yield return ["/api/library/book-categories?academicYearId={academicYearId}&page=1&pageSize=5"];
		yield return ["/api/library/charge-categories?academicYearId={academicYearId}&page=1&pageSize=5"];
		yield return ["/api/library/library-hours?academicYearId={academicYearId}&page=1&pageSize=10"];
		yield return ["/api/library/borrowing-rules?academicYearId={academicYearId}&page=1&pageSize=5"];
		yield return ["/api/library/sections?academicYearId={academicYearId}&page=1&pageSize=5"];
	}

	[Theory]
	[MemberData(nameof(CanonicalGetRoutes))]
	public async Task Canonical_Library_Get_Routes_Should_Return_Success(string routeTemplate)
	{
		var route = ResolveRoute(routeTemplate);

		var response = await Client.GetAsync(route);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Library_Book_List_Should_Require_View_Permission()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");

		var response = await client.GetAsync(ResolveRoute("/api/library/books?academicYearId={academicYearId}&page=1&pageSize=5"));

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Delete_Reservation_Should_Return_Library_Problem_For_Invalid_Book_Reservation_Pair()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",",
				ShikshaMitraSubmodulePermissions.Library.AllBooks.View,
				ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve));

		var response = await client.DeleteAsync($"/api/library/books/{Guid.NewGuid():D}/reservations/{Guid.NewGuid():D}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(body);
		document.RootElement.GetProperty("errorCode").GetString().ShouldBe(LibraryErrorCodes.ReservationNotFound);
	}

	private string ResolveRoute(string routeTemplate)
	{
		return routeTemplate.Replace("{academicYearId}", Factory.SeededAcademicYearId.ToString("D"));
	}
}
