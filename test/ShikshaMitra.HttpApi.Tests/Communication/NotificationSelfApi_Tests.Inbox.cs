using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

public partial class NotificationSelfApi_Tests
{
	[Fact]
	public async Task Pending_guardian_review_suspends_existing_child_inbox_access_until_the_review_is_resolved()
	{
		var owner = await CreatePersonalUserAsync(); var studentId = await SeedLinkedStudentAsync(owner, "guardian", "mother");
		await SeedInboxAsync(owner, "Guardian", studentId);
		using var client = CreateClientAs(owner);
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/inbox")).GetProperty("items").GetArrayLength().ShouldBe(1);
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.Add(new StudentGuardianAccessReview(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), studentId,
				Guid.NewGuid(), "[]", "SuspendUntilReview")); await uow.CompleteAsync();
		}
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/inbox")).GetProperty("items").GetArrayLength().ShouldBe(0);
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/preferences/options"))
			.GetProperty("students").GetArrayLength().ShouldBe(0);
		using var denied = await client.PutAsJsonAsync($"{Route}/preferences", PreferenceInput(studentId: studentId));
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Another_user_cannot_list_read_or_acknowledge_an_owned_inbox_item()
	{
		var owner = await CreatePersonalUserAsync(); var itemId = await SeedInboxAsync(owner);
		using var client = CreateClientAs(owner);
		using var other = CreateClientAs(await CreatePersonalUserAsync());
		var own = await client.GetFromJsonAsync<JsonElement>($"{Route}/inbox");
		own.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(itemId);
		var foreign = await other.GetFromJsonAsync<JsonElement>($"{Route}/inbox");
		foreign.GetProperty("items").GetArrayLength().ShouldBe(0);
		foreach (var action in new[] { "read", "acknowledge" })
		{
			using var forbidden = await other.PostAsync($"{Route}/inbox/{itemId}/{action}", null);
			forbidden.StatusCode.ShouldBe(HttpStatusCode.NotFound, await forbidden.Content.ReadAsStringAsync());
		}
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/unread-count")).GetProperty("unreadCount").GetInt64().ShouldBe(1);
		using var valid = await client.PostAsync($"{Route}/inbox/{itemId}/read", null);
		valid.StatusCode.ShouldBe(HttpStatusCode.OK, await valid.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Reading_does_not_acknowledge_and_repeated_explicit_actions_preserve_the_first_timestamp(bool acknowledgeFirst)
	{
		var owner = await CreatePersonalUserAsync(); var itemId = await SeedInboxAsync(owner);
		using var client = CreateClientAs(owner);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "none");
		var action = acknowledgeFirst ? "acknowledge" : "read";
		using var response = await client.PostAsync($"{Route}/inbox/{itemId}/{action}", null);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var first = await response.Content.ReadFromJsonAsync<JsonElement>();
		first.GetProperty("readAt").ValueKind.ShouldBe(JsonValueKind.String);
		first.GetProperty("acknowledgedAt").ValueKind.ShouldBe(acknowledgeFirst ? JsonValueKind.String : JsonValueKind.Null);
		using var repeated = await client.PostAsync($"{Route}/inbox/{itemId}/{action}", null);
		repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
		var second = await repeated.Content.ReadFromJsonAsync<JsonElement>();
		second.GetProperty("readAt").GetString().ShouldBe(first.GetProperty("readAt").GetString());
		second.GetProperty("acknowledgedAt").ToString().ShouldBe(first.GetProperty("acknowledgedAt").ToString());
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/unread-count")).GetProperty("unreadCount").GetInt64().ShouldBe(0);
		if (!acknowledgeFirst)
		{
			using var acknowledgement = await client.PostAsync($"{Route}/inbox/{itemId}/acknowledge", null);
			acknowledgement.StatusCode.ShouldBe(HttpStatusCode.OK);
			var final = await acknowledgement.Content.ReadFromJsonAsync<JsonElement>();
			final.GetProperty("readAt").GetString().ShouldBe(first.GetProperty("readAt").GetString());
			final.GetProperty("acknowledgedAt").ValueKind.ShouldBe(JsonValueKind.String);
		}
	}

	[Theory]
	[InlineData("guardian", "mother", "Guardian")]
	[InlineData("student", "self", "StudentAccount")]
	public async Task Revoking_a_verified_association_hides_child_content_and_preferences_and_blocks_mutations(string type, string relationship, string kind)
	{
		var owner = await CreatePersonalUserAsync(); var studentId = await SeedLinkedStudentAsync(owner, type, relationship);
		var itemId = await SeedInboxAsync(owner, kind, studentId);
		using var client = CreateClientAs(owner);
		var before = await client.GetFromJsonAsync<JsonElement>($"{Route}/inbox");
		before.GetProperty("items").GetArrayLength().ShouldBe(1);
		using var preference = await client.PutAsJsonAsync($"{Route}/preferences", PreferenceInput(studentId: studentId));
		preference.StatusCode.ShouldBe(HttpStatusCode.OK, await preference.Content.ReadAsStringAsync());
		await RevokeLinkAsync(owner, studentId);
		var after = await client.GetFromJsonAsync<JsonElement>($"{Route}/inbox");
		after.GetProperty("items").GetArrayLength().ShouldBe(0);
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/preferences")).GetArrayLength().ShouldBe(0);
		(await client.GetFromJsonAsync<JsonElement>($"{Route}/unread-count")).GetProperty("unreadCount").GetInt64().ShouldBe(0);
		using var read = await client.PostAsync($"{Route}/inbox/{itemId}/read", null);
		read.StatusCode.ShouldBe(HttpStatusCode.NotFound, await read.Content.ReadAsStringAsync());
		using var denied = await client.PutAsJsonAsync($"{Route}/preferences", PreferenceInput(studentId: studentId));
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
	}

	private async Task<Guid> SeedInboxAsync(Guid owner, string kind = "Requester", Guid? studentId = null)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var eventKey = studentId.HasValue ? "Academic.Homework.Published" : "Academic.Report.ExportReady";
		var sourceType = studentId.HasValue ? "Academic.Homework" : "Academic.Report";
		var rule = await db.Set<NotificationRule>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EventKey == eventKey && x.ScopeKey == "school");
		if (rule == null) { rule = new(Guid.NewGuid(), tenantId, eventKey, NotificationRuleScope.Create()); db.Add(rule); }
		var header = new CommunicationTemplate(Guid.NewGuid(), tenantId) { TemplateName = "Personal inbox fixture" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenantId, header.Id, 1, eventKey, kind,
			CommunicationChannelType.InApp, "en", "Private update", "Owned content", 1);
		revision.Publish(owner, DateTimeOffset.UtcNow);
		var sourceId = Guid.NewGuid(); var logical = Guid.NewGuid().ToString("N");
		var intent = new NotificationIntent(Guid.NewGuid(), tenantId, rule.Id, 1, eventKey, sourceType, sourceId, "1", logical,
			DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "[]", "[]");
		var dispatch = new NotificationDispatch(Guid.NewGuid(), tenantId, intent.Id, revision.Id, "{}", "user", CommunicationChannelType.InApp,
			logical, "protected-fixture", logical, logical, DateTimeOffset.UtcNow);
		db.Entry(dispatch).Property(x => x.Status).CurrentValue = NotificationDispatchState.Delivered;
		var item = new NotificationInboxItem(Guid.NewGuid(), tenantId, owner, dispatch.Id, sourceType, sourceId,
			studentId.HasValue ? $"student:{studentId:N}" : "user", logical, "Private update", "Owned content", "/academic/notification", studentId);
		db.AddRange(header, revision, intent, dispatch, item); await uow.CompleteAsync(); return item.Id;
	}

	private async Task<Guid> SeedLinkedStudentAsync(Guid owner, string type, string relationship)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var student = new Student(Guid.NewGuid(), tenantId, "Notification", null, "Fixture", Factory.SeededAcademicYearId,
			Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId,
			status: StudentLifeCycleStatusEnum.Enrolled);
		db.Add(student); db.Add(new StudentPortalAccountLink(Guid.NewGuid(), tenantId, student.Id, owner, type, relationship,
			Guid.Parse(TestAuthHandler.TestUserId))); await uow.CompleteAsync(); return student.Id;
	}

	private async Task RevokeLinkAsync(Guid owner, Guid studentId)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var link = await db.Set<StudentPortalAccountLink>().SingleAsync(x => x.StudentId == studentId && x.UserId == owner);
		link.IsDeleted = true; await uow.CompleteAsync();
	}
}
