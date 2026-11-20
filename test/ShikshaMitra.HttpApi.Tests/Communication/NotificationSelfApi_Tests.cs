using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.BackgroundWorkers;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[Collection(ApiTestCollection.Name)]
public partial class NotificationSelfApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/communication/notifications/me";

	[Fact]
	public void The_host_container_resolves_academic_workers_and_their_tenant_services()
	{
		using var scope = Factory.Services.CreateScope();
		scope.ServiceProvider.GetRequiredService<AcademicNotificationDispatchWorker>().ShouldNotBeNull();
		scope.ServiceProvider.GetRequiredService<AcademicNotificationReminderWorker>().ShouldNotBeNull();
		scope.ServiceProvider.GetRequiredService<NotificationTenantQueueRunner>().ShouldNotBeNull();
		scope.ServiceProvider.GetRequiredService<INotificationQueueRepository>().ShouldNotBeNull();
		scope.ServiceProvider.GetRequiredService<INotificationDueScheduler>().ShouldNotBeNull();
		scope.ServiceProvider.GetRequiredService<WhatsAppProviderClient>().ShouldNotBeNull();
	}

	[Fact]
	public void The_host_resolves_the_academic_smtp_and_inbox_senders()
	{
		using var scope = Factory.Services.CreateScope();
		scope.ServiceProvider.GetServices<INotificationChannelSender>().Single(x => x.Channel == CommunicationChannelType.Email)
			.ShouldBeOfType<SmtpNotificationChannelSender>();
		scope.ServiceProvider.GetServices<INotificationChannelSender>().Single(x => x.Channel == CommunicationChannelType.InApp)
			.ShouldBeOfType<InAppNotificationChannelSender>();
		scope.ServiceProvider.GetServices<INotificationChannelSender>().Single(x => x.Channel == CommunicationChannelType.WhatsApp)
			.ShouldBeOfType<WhatsAppNotificationChannelSender>();
	}

	[Fact]
	public async Task Anonymous_inbox_is_denied_and_missing_push_configuration_is_reported_without_claiming_readiness()
	{
		using var anonymous = CreateAnonymousClient();
		using var denied = await anonymous.GetAsync($"{Route}/inbox"); denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var client = CreateClientAs(await CreatePersonalUserAsync());
		using var key = await client.GetAsync($"{Route}/push/public-key");
		key.StatusCode.ShouldBe(HttpStatusCode.OK, await key.Content.ReadAsStringAsync());
		var body = await key.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("isConfigured").GetBoolean().ShouldBeFalse();
		body.GetProperty("publicKey").ValueKind.ShouldBe(JsonValueKind.Null);
		body.GetProperty("reasonCode").GetString().ShouldBe("Communication:Notification:ProviderUnavailable");
	}

	[Theory]
	[InlineData("channel")]
	[InlineData("language")]
	[InlineData("quiet-hours")]
	[InlineData("category")]
	public async Task Unsupported_personal_preferences_are_rejected_before_creating_a_row(string field)
	{
		using var client = CreateClientAs(await CreatePersonalUserAsync());
		var input = new Dictionary<string, object?> { ["category"] = "Academic.Homework", ["enabledChannels"] = new[] { 2, 3 },
			["languageCode"] = "en", ["quietHoursEnabled"] = true, ["quietHoursStart"] = "20:00", ["quietHoursEnd"] = "07:00" };
		switch (field) {
			case "channel": input["enabledChannels"] = new[] { 1 }; break;
			case "language": input["languageCode"] = ""; break;
			case "quiet-hours": input["quietHoursEnd"] = "20:00"; break;
			case "category": input["category"] = "Unregistered.Category"; break;
		}
		using var response = await client.PutAsJsonAsync($"{Route}/preferences", input);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/preferences")).GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task Preference_options_return_registered_categories_and_current_linked_students_without_staff_access()
	{
		var owner = await CreatePersonalUserAsync();
		var child = await SeedLinkedStudentAsync(owner, "guardian", "mother");
		var revoked = await SeedLinkedStudentAsync(owner, "guardian", "father");
		var unrelated = await SeedLinkedStudentAsync(await CreatePersonalUserAsync(), "student", "self");
		await RevokeLinkAsync(owner, revoked);
		using var client = CreateClientAs(owner);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		using var response = await client.GetAsync($"{Route}/preferences/options");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		response.Headers.CacheControl?.NoStore.ShouldBe(true);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		var categories = body.GetProperty("categories").EnumerateArray().ToArray();
		categories.Single(x => x.GetProperty("category").GetString() == "Academic.Homework")
			.GetProperty("channels").EnumerateArray().Select(x => x.GetInt32()).ShouldBe([0, 2, 3, 4], ignoreOrder: true);
		categories.Single(x => x.GetProperty("category").GetString() == "Academic.Settings")
			.GetProperty("channels").EnumerateArray().Select(x => x.GetInt32()).ShouldBe([3, 4], ignoreOrder: true);
		var students = body.GetProperty("students").EnumerateArray().ToArray();
		students.ShouldHaveSingleItem().GetProperty("studentId").GetGuid().ShouldBe(child);
		students[0].GetProperty("displayName").GetString().ShouldBe("Notification Fixture");
		(await response.Content.ReadAsStringAsync()).ShouldNotContain(unrelated.ToString());
		using var anonymous = CreateAnonymousClient();
		using var denied = await anonymous.GetAsync($"{Route}/preferences/options");
		denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Personal_inbox_and_unread_count_need_active_identity_but_no_staff_permissions_and_are_not_cacheable()
	{
		var userId = await CreatePersonalUserAsync();
		using var client = CreateClientAs(userId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		using var inbox = await client.GetAsync($"{Route}/inbox?maxResultCount=5");
		inbox.StatusCode.ShouldBe(HttpStatusCode.OK, await inbox.Content.ReadAsStringAsync());
		(inbox.Headers.CacheControl?.NoStore).ShouldBe(true);
		var page = await inbox.Content.ReadFromJsonAsync<JsonElement>();
		page.GetProperty("totalCount").GetInt64().ShouldBe(0);
		page.GetProperty("items").GetArrayLength().ShouldBe(0);
		using var unread = await client.GetAsync($"{Route}/unread-count");
		unread.StatusCode.ShouldBe(HttpStatusCode.OK);
		(unread.Headers.CacheControl?.NoStore).ShouldBe(true);
		(await unread.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unreadCount").GetInt64().ShouldBe(0);
	}

	[Fact]
	public async Task Preferences_are_owned_by_the_authenticated_user_and_stale_edits_are_rejected()
	{
		var userId = await CreatePersonalUserAsync();
		using var client = CreateClientAs(userId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		using var saved = await client.PutAsJsonAsync($"{Route}/preferences", PreferenceInput());
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var preference = await saved.Content.ReadFromJsonAsync<JsonElement>();
		preference.GetProperty("category").GetString().ShouldBe("Academic.Homework");
		var stamp = preference.GetProperty("concurrencyStamp").GetString();
		stamp.ShouldNotBeNullOrWhiteSpace();
		using var stale = await client.PutAsJsonAsync($"{Route}/preferences", PreferenceInput("stale"));
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		using var other = CreateClientAs(await CreatePersonalUserAsync());
		var ownRows = await client.GetFromJsonAsync<JsonElement>($"{Route}/preferences");
		ownRows.GetArrayLength().ShouldBe(1);
		(await other.GetFromJsonAsync<JsonElement>($"{Route}/preferences")).GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task Choosing_a_child_preference_cannot_grant_a_student_association()
	{
		using var client = CreateClientAs(await CreatePersonalUserAsync());
		using var response = await client.PutAsJsonAsync($"{Route}/preferences", PreferenceInput(studentId: Guid.NewGuid()));
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Missing_or_inactive_current_tenant_identity_cannot_use_the_personal_api(bool inactive)
	{
		var userId = inactive ? await CreatePersonalUserAsync(false) : Guid.NewGuid();
		using var client = CreateClientAs(userId);
		using var response = await client.GetAsync($"{Route}/inbox");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task A_user_claim_from_another_tenant_does_not_grant_personal_access()
	{
		using var client = CreateClientAs(await CreatePersonalUserAsync(tenantOverride: Guid.NewGuid()));
		using var response = await client.GetAsync($"{Route}/inbox");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	private static object PreferenceInput(string? stamp = null, Guid? studentId = null) => new {
		category = "Academic.Homework", studentId, enabledChannels = new[] { 2, 3 }, languageCode = "en",
		quietHoursEnabled = true, quietHoursStart = "20:00", quietHoursEnd = "07:00", concurrencyStamp = stamp
	};

	private async Task<Guid> CreatePersonalUserAsync(bool active = true, Guid? tenantOverride = null)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		var tenantId = tenantOverride ?? Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var id = Guid.NewGuid(); var user = new IdentityUser(id, $"notification-{id:N}", $"{id:N}@example.test", tenantId);
		user.SetIsActive(active); db.Add(user); await uow.CompleteAsync(); return id;
	}
}
