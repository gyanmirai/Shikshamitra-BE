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
public class SubstitutionAuthorizationApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/academics/timetable/substitutions";

	public SubstitutionAuthorizationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	public static IEnumerable<object[]> ProtectedRoutes()
	{
		var id = Guid.NewGuid().ToString("D");
		yield return [HttpMethod.Get, BaseUrl, null!];
		yield return [HttpMethod.Post, BaseUrl, new CreateSubstitutionDto()];
		yield return [HttpMethod.Put, $"{BaseUrl}/{id}", new UpdateSubstitutionDto()];
		yield return [HttpMethod.Post, $"{BaseUrl}/{id}/acknowledge", null!];
		yield return [HttpMethod.Post, $"{BaseUrl}/{id}/cancel", new SubstitutionActionDto()];
		yield return [HttpMethod.Put, $"{BaseUrl}/settings", new SubstitutionSettingsDto()];
	}

	[Theory]
	[MemberData(nameof(ProtectedRoutes))]
	public async Task Substitution_Routes_Should_Reject_Users_Without_Timetable_Permission(
		HttpMethod method,
		string route,
		object? body)
	{
		using var client = CreateClientWithPermissions("__none__");
		using var request = new HttpRequestMessage(method, route);
		if (body != null)
		{
			request.Content = JsonContent.Create(body);
		}

		var response = await client.SendAsync(request);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Substitution_Reads_Should_Reject_Add_Only_Users()
	{
		using var client = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add);

		var response = await client.GetAsync(BaseUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Substitution_Mutations_Should_Reject_View_Only_Users()
	{
		using var client = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View);
		var id = Guid.NewGuid().ToString("D");

		var createResponse = await client.PostAsJsonAsync(BaseUrl, new CreateSubstitutionDto());
		var acknowledgeResponse = await client.PostAsync($"{BaseUrl}/{id}/acknowledge", null);
		var cancelResponse = await client.PostAsJsonAsync(
			$"{BaseUrl}/{id}/cancel",
			new SubstitutionActionDto());

		createResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await createResponse.Content.ReadAsStringAsync());
		acknowledgeResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await acknowledgeResponse.Content.ReadAsStringAsync());
		cancelResponse.StatusCode.ShouldBe(
			HttpStatusCode.Forbidden,
			await cancelResponse.Content.ReadAsStringAsync());
	}

	[Fact]
	public void Substitution_Actions_Should_Use_The_Canonical_Timetable_Permissions()
	{
		var view = ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View;
		var add = ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add;
		var delete = ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Delete;
		var expectedPolicies = new Dictionary<string, string>
		{
			[nameof(SubstitutionController.GetListAsync)] = view,
			[nameof(SubstitutionController.GetDashboardAsync)] = view,
			[nameof(SubstitutionController.GetMyDutiesAsync)] = view,
			[nameof(SubstitutionController.GetCreateFormMetadataAsync)] = view,
			[nameof(SubstitutionController.GetLecturesAsync)] = view,
			[nameof(SubstitutionController.GetAsync)] = view,
			[nameof(SubstitutionController.CreateAsync)] = add,
			[nameof(SubstitutionController.UpdateAsync)] = add,
			[nameof(SubstitutionController.AssignAsync)] = add,
			[nameof(SubstitutionController.SendReminderAsync)] = add,
			[nameof(SubstitutionController.CompleteAsync)] = add,
			[nameof(SubstitutionController.CancelAsync)] = delete,
			[nameof(SubstitutionController.RejectAsync)] = delete,
			[nameof(SubstitutionController.AcknowledgeAsync)] = add,
			[nameof(SubstitutionController.RequestChangeAsync)] = add,
			[nameof(SubstitutionController.RejectDutyAsync)] = add,
			[nameof(SubstitutionController.GetSuggestedTeachersAsync)] = view,
			[nameof(SubstitutionController.GetAuditLogsAsync)] = view,
			[nameof(SubstitutionController.GetDailySheetAsync)] = view,
			[nameof(SubstitutionController.GetDailySheetPdfAsync)] = view,
			[nameof(SubstitutionController.GetDailySheetExcelAsync)] = view,
			[nameof(SubstitutionController.GetAnalyticsAsync)] = view,
			[nameof(SubstitutionController.GetSettingsAsync)] = view,
			[nameof(SubstitutionController.UpdateSettingsAsync)] = add
		};

		var methods = typeof(SubstitutionController).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		methods.Length.ShouldBe(25);

		foreach (var method in methods)
		{
			expectedPolicies.TryGetValue(method.Name, out var expectedPolicy).ShouldBeTrue(
				$"No canonical policy expectation was defined for {method.Name}.");
			var authorize = method.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
			authorize.ShouldNotBeNull();
			authorize.Policy.ShouldBe(expectedPolicy);
		}
	}

	[Fact]
	public void Substitution_App_Service_Should_Not_Expose_A_Second_Conventional_Api()
	{
		var remoteService = typeof(SubstitutionAppService)
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
