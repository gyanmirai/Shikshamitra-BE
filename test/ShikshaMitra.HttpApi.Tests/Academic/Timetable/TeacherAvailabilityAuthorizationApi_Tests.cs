using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Controllers.Academic.Timetable;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
public class TeacherAvailabilityAuthorizationApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/academics/timetable/teacher-availability";

	public TeacherAvailabilityAuthorizationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	public static IEnumerable<object[]> ProtectedRoutes()
	{
		yield return [HttpMethod.Get, BaseUrl];
		yield return [HttpMethod.Post, BaseUrl];
		yield return [HttpMethod.Post, $"{BaseUrl}/affected-lectures"];
		yield return [HttpMethod.Get, $"{BaseUrl}/affected-lectures?availabilityDate=2026-07-26"];
		yield return [HttpMethod.Post, $"{BaseUrl}/{Guid.NewGuid():D}/cancel"];
	}

	[Theory]
	[MemberData(nameof(ProtectedRoutes))]
	public async Task Teacher_Availability_Routes_Should_Reject_Users_Without_Timetable_Permission(
		HttpMethod method,
		string route)
	{
		using var client = CreateClientWithPermissions("__none__");
		using var request = new HttpRequestMessage(method, route);
		if (method == HttpMethod.Post && route == BaseUrl)
		{
			request.Content = JsonContent.Create(new CreateTeacherAvailabilityDto());
		}
		else if (method == HttpMethod.Post && route.EndsWith("/affected-lectures", StringComparison.Ordinal))
		{
			request.Content = JsonContent.Create(new CreateTeacherAvailabilityDto());
		}

		var response = await client.SendAsync(request);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Teacher_Availability_Read_Routes_Should_Require_View_Permission()
	{
		using var addOnlyClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add);
		using var viewClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View);

		var denied = await addOnlyClient.GetAsync(BaseUrl);
		var allowed = await viewClient.GetAsync(BaseUrl);

		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		allowed.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, await allowed.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Teacher_Availability_Mutations_Should_Reject_View_Only_Users()
	{
		using var viewOnlyClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View);

		var deniedCreate = await viewOnlyClient.PostAsJsonAsync(BaseUrl, new CreateTeacherAvailabilityDto());
		var deniedCancel = await viewOnlyClient.PostAsync($"{BaseUrl}/{Guid.NewGuid():D}/cancel", null);

		deniedCreate.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await deniedCreate.Content.ReadAsStringAsync());
		deniedCancel.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await deniedCancel.Content.ReadAsStringAsync());
	}

	[Fact]
	public void Teacher_Availability_Actions_Should_Use_The_Canonical_Timetable_Permissions()
	{
		var expectedPolicies = new Dictionary<string, string>
		{
			[nameof(TeacherAvailabilityController.GetListAsync)] =
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View,
			[nameof(TeacherAvailabilityController.CreateAsync)] =
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add,
			[nameof(TeacherAvailabilityController.GetAffectedLecturesAsync)] =
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View,
			[nameof(TeacherAvailabilityController.GetAffectedLecturesFromQueryAsync)] =
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View,
			[nameof(TeacherAvailabilityController.CancelAsync)] =
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Delete
		};

		foreach (var (methodName, expectedPolicy) in expectedPolicies)
		{
			var method = typeof(TeacherAvailabilityController).GetMethod(methodName);
			method.ShouldNotBeNull();
			var authorize = method.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
			authorize.ShouldNotBeNull();
			authorize.Policy.ShouldBe(expectedPolicy);
		}
	}

	[Fact]
	public void Teacher_Availability_App_Service_Should_Not_Expose_A_Second_Conventional_Api()
	{
		var remoteService = typeof(TeacherAvailabilityAppService)
			.GetCustomAttribute<RemoteServiceAttribute>(inherit: true);

		remoteService.ShouldNotBeNull();
		remoteService.IsEnabled.ShouldBeFalse();
	}

	private HttpClient CreateClientWithPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", permissions));
		return client;
	}
}
