using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(AcademicNotificationPreviewApiCollection.Name)]
public class AcademicNotificationManualApi_Tests(AcademicNotificationPreviewApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BasePath = "/api/academic/notifications";
	private const string Route = BasePath + "/manual/preview";
	private const string SendRoute = BasePath + "/manual/send";
	private const string Event = AcademicNotificationPreviewApiFactory.Event;
	private const string SourceType = "Academic.Homework";
	private const string View = ShikshaMitraSubmodulePermissions.Academics.Notification.View;
	private const string Add = ShikshaMitraSubmodulePermissions.Academics.Notification.Add;
	private const string Send = ShikshaMitraSubmodulePermissions.Academics.Notification.Send;

	[Fact]
	public async Task Source_sender_without_add_can_review_a_published_template_and_receives_a_bound_expiring_token()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var published = await PublishGuardianTemplateAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Send}");
		var first = await ReviewAsync(Request(published.TemplateId));
		var repeated = await ReviewAsync(Request(published.TemplateId));
		first.Id.ShouldNotBe(repeated.Id);
		first.Digest.ShouldBe(repeated.Digest);
		await ReviewAsync(Request());

		try
		{
			factory.PrimaryEmail = "updated@example.invalid";
			var changedContact = await ReviewAsync(Request(published.TemplateId));
			changedContact.Digest.ShouldNotBe(first.Digest);
			factory.SourceRevision = "homework-v2";
			var changedSource = await ReviewAsync(Request(published.TemplateId));
			changedSource.Digest.ShouldNotBe(changedContact.Digest);
		}
		finally
		{
			factory.PrimaryEmail = "private@example.invalid";
			factory.SourceRevision = "homework-v1";
		}
	}

	[Theory]
	[InlineData(View)]
	[InlineData(Send)]
	[InlineData(View + "," + Add)]
	public async Task Manual_review_requires_both_view_and_send(string actions)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, actions);
		using var response = await Client.PostAsJsonAsync(Route, Request());
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Manual_review_rejects_unknown_saved_recipients_and_cannot_accept_arbitrary_destinations()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Send}");
		using var foreign = await Client.PostAsJsonAsync(Route, Request(studentId: Guid.NewGuid()));
		foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await foreign.Content.ReadAsStringAsync());
		using var arbitrary = await Client.PostAsJsonAsync(Route, new
		{
			eventKey = Event, sourceType = SourceType, sourceId = AcademicNotificationPreviewApiFactory.SourceId,
			channels = new[] { CommunicationChannelType.InApp }, phone = "+15555550101"
		});
		arbitrary.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await arbitrary.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task A_review_with_more_than_one_hundred_eligible_recipients_flags_approval()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var published = await PublishGuardianTemplateAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Send}");
		try
		{
			factory.AdditionalStudents = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
			await ReviewAsync(new
			{
				eventKey = Event, sourceType = SourceType, sourceId = AcademicNotificationPreviewApiFactory.SourceId,
				channels = new[] { CommunicationChannelType.InApp }, templateId = published.TemplateId,
				selectedRecipients = factory.AdditionalStudents.Prepend(AcademicNotificationPreviewApiFactory.StudentId)
					.Select(id => new NotificationRecipientRefDto("Student", id)).ToArray()
			}, expectedRecipients: 101, requiresApproval: true);
		}
		finally
		{
			factory.AdditionalStudents = [];
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Confirmation_queues_once_and_the_worker_checks_the_reviewed_delivery(bool contactChangesAfterConfirmation)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			Permissions(Client, $"{View},{Send}");
			var review = await ReviewAsync(Request(sourceId: factory.ActiveSourceId));
			Permissions(Client, Send);
			var request = new { reviewToken = review.Id, idempotencyKey = $"single-{review.Id:N}" };
			var queued = await ConfirmAsync(Client, request, CommunicationCampaignStatus.Queued);
			(await ConfirmAsync(Client, request, CommunicationCampaignStatus.Queued)).OperationId.ShouldBe(queued.OperationId);
			using var consumed = await Client.PostAsJsonAsync(SendRoute, new { reviewToken = review.Id, idempotencyKey = $"different-{review.Id:N}" });
			await AssertConflictAsync(consumed, NotificationErrorCodes.ReviewConsumed);
			if (contactChangesAfterConfirmation) factory.PrimaryEmail = "changed-after-confirming@example.invalid";
			await WithDatabaseAsync(async (db, services) =>
			{
				var intent = await db.NotificationIntents.SingleAsync(x => x.CampaignId == queued.OperationId);
				intent.RuleId.ShouldBeNull();
				intent.Origin.ShouldBe(NotificationIntentOrigin.ReviewedManual);
				(await db.CommunicationCampaigns.CountAsync(x => x.SourceEntityId == factory.ActiveSourceId)).ShouldBe(1);
				await PlanAsync(db, services, intent);
				(await db.NotificationDispatches.CountAsync(x => x.IntentId == intent.Id)).ShouldBe(contactChangesAfterConfirmation ? 0 : 1);
				if (contactChangesAfterConfirmation)
				{
					intent.Status.ShouldBe(NotificationIntentState.NeedsReview);
					intent.ErrorCode.ShouldBe(NotificationErrorCodes.ReviewChanged);
				}
			});
		}
		finally
		{
			factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId;
			factory.PrimaryEmail = "private@example.invalid";
		}
	}

	[Fact]
	public async Task Changed_and_expired_reviews_return_conflict_before_an_operation_is_created()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			Permissions(Client, $"{View},{Send}");
			var review = await ReviewAsync(Request(sourceId: factory.ActiveSourceId));
			factory.PrimaryEmail = "changed@example.invalid";
			using var changed = await Client.PostAsJsonAsync(SendRoute, new { reviewToken = review.Id, idempotencyKey = "changed-contact" });
			await AssertConflictAsync(changed, NotificationErrorCodes.ReviewChanged);
			await WithDatabaseAsync(async (db, _) =>
			{
				var stored = await db.NotificationSendReviews.SingleAsync(x => x.Id == review.Id);
				stored.OperationId.ShouldBeNull();
				db.Entry(stored).Property(x => x.ExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
				await db.SaveChangesAsync();
			});
			using var expired = await Client.PostAsJsonAsync(SendRoute, new { reviewToken = review.Id, idempotencyKey = "changed-contact" });
			await AssertConflictAsync(expired, NotificationErrorCodes.ReviewExpired);
		}
		finally
		{
			factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId;
			factory.PrimaryEmail = "private@example.invalid";
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Pending_bulk_send_requires_an_independent_assigned_approver(bool approve)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PublishGuardianTemplateAsync();
		await SeedApproverAsync();
		factory.ActiveSourceId = Guid.NewGuid();
		try
		{
			factory.AdditionalStudents = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
			Permissions(Client, $"{View},{Send},{Add}");
			var review = await ReviewAsync(new
			{
				eventKey = Event, sourceType = SourceType, sourceId = factory.ActiveSourceId,
				channels = new[] { CommunicationChannelType.InApp },
				selectedRecipients = factory.AdditionalStudents.Prepend(AcademicNotificationPreviewApiFactory.StudentId)
					.Select(id => new NotificationRecipientRefDto("Student", id)).ToArray()
			}, expectedRecipients: 101, requiresApproval: true);
			var pending = await ConfirmAsync(Client, new { reviewToken = review.Id, idempotencyKey = $"bulk-{review.Id:N}" },
				CommunicationCampaignStatus.PendingApproval);
			var decisionRoute = $"{BasePath}/operations/{pending.OperationId}/{(approve ? "approve" : "reject")}";
			await WithDatabaseAsync(async (db, _) =>
			{
				(await db.NotificationIntents.AnyAsync(x => x.CampaignId == pending.OperationId)).ShouldBeFalse();
			});
			using var self = await Client.PostAsync(decisionRoute, null);
			self.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var approver = CreateClientAs(AcademicNotificationPreviewApiFactory.ApproverId);
			approver.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Staff");
			Permissions(approver, Add);
			using var decision = await approver.PostAsync(decisionRoute, null);
			decision.StatusCode.ShouldBe(HttpStatusCode.OK, await decision.Content.ReadAsStringAsync());
			using var repeated = await approver.PostAsync(decisionRoute, null);
			repeated.StatusCode.ShouldBe(HttpStatusCode.OK, await repeated.Content.ReadAsStringAsync());
			await WithDatabaseAsync(async (db, _) =>
			{
				var operation = await db.CommunicationCampaigns.SingleAsync(x => x.Id == pending.OperationId);
				operation.ApprovedByUserId.ShouldBe(approve ? AcademicNotificationPreviewApiFactory.ApproverId : null);
				operation.Status.ShouldBe(approve ? CommunicationCampaignStatus.Queued : CommunicationCampaignStatus.Cancelled);
				(await db.NotificationIntents.CountAsync(x => x.CampaignId == operation.Id)).ShouldBe(approve ? 2 : 0);
			});
		}
		finally
		{
			factory.ActiveSourceId = AcademicNotificationPreviewApiFactory.SourceId;
			factory.AdditionalStudents = [];
		}
	}

	private static object Request(Guid? templateId = null, Guid? studentId = null, Guid? sourceId = null) => new
	{
		eventKey = Event, sourceType = SourceType, sourceId = sourceId ?? AcademicNotificationPreviewApiFactory.SourceId,
		selectedRecipients = new[] { new NotificationRecipientRefDto("Student", studentId ?? AcademicNotificationPreviewApiFactory.StudentId) },
		channels = new[] { CommunicationChannelType.InApp }, templateId
	};

	private async Task<NotificationTemplateDto> PublishGuardianTemplateAsync()
	{
		using var setup = Factory.CreateClient();
		setup.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Add}");
		using var initialize = await setup.PostAsJsonAsync($"{BasePath}/templates/initialize-defaults", new { eventKey = Event });
		initialize.StatusCode.ShouldBe(HttpStatusCode.OK, await initialize.Content.ReadAsStringAsync());
		var page = await setup.GetFromJsonAsync<PagedResultDto<NotificationTemplateDto>>($"{BasePath}/templates?eventKey={Event}&maxResultCount=100", JsonOptions);
		var template = page!.Items.Single(x => x.RecipientKind == "Guardian" && x.Channel == CommunicationChannelType.InApp);
		if (template.IsPublished) return template;
		using var published = await setup.PostAsJsonAsync($"{BasePath}/templates/{template.TemplateId}/publish",
			new NotificationTemplateRevisionCommandDto(template.RevisionId, template.ConcurrencyStamp));
		published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
		return (await published.Content.ReadFromJsonAsync<NotificationTemplateDto>(JsonOptions))!;
	}

	private async Task<NotificationSendReview> ReviewAsync(object request, int expectedRecipients = 1, bool requiresApproval = false)
	{
		var before = DateTimeOffset.UtcNow;
		using var response = await Client.PostAsJsonAsync(Route, request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		response.Headers.CacheControl?.NoStore.ShouldBe(true);
		var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		var token = result.GetProperty("reviewToken").GetGuid();
		var expiresAt = result.GetProperty("expiresAt").GetDateTimeOffset();
		expiresAt.ShouldBeInRange(before.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(10));
		result.GetProperty("eligibleRecipientCount").GetInt32().ShouldBe(expectedRecipients);
		result.GetProperty("requiresApproval").GetBoolean().ShouldBe(requiresApproval);
		result.GetProperty("channels")[0].GetProperty("readyDeliveryCount").GetInt32().ShouldBe(expectedRecipients);
		var raw = await response.Content.ReadAsStringAsync();
		raw.ShouldNotContain("Private source text");
		raw.ShouldNotContain("private@example.invalid");
		raw.ShouldNotContain("digest", Case.Insensitive);

		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(AcademicNotificationPreviewApiFactory.TenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin();
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var stored = await db.Set<NotificationSendReview>().SingleAsync(x => x.Id == token);
		stored.ActorUserId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
		stored.TenantId.ShouldBe(AcademicNotificationPreviewApiFactory.TenantId);
		stored.ExpiresAt.ShouldBe(expiresAt);
		stored.Digest.Length.ShouldBe(64);
		stored.ProtectedRequest.ShouldNotBeNullOrWhiteSpace();
		stored.ProtectedRequest.ShouldNotContain(factory.ActiveSourceId.ToString());
		stored.ConsumedAt.ShouldBeNull();
		(await db.NotificationIntents.AnyAsync(x => x.SourceId == factory.ActiveSourceId)).ShouldBeFalse();
		(await db.CommunicationCampaigns.AnyAsync(x => x.SourceEntityId == factory.ActiveSourceId)).ShouldBeFalse();
		await unit.CompleteAsync();
		return stored;
	}

	private static async Task<AcademicNotificationOperationDto> ConfirmAsync(HttpClient client, object request, CommunicationCampaignStatus expected)
	{
		using var result = await client.PostAsJsonAsync(SendRoute, request);
		result.StatusCode.ShouldBe(HttpStatusCode.Accepted, await result.Content.ReadAsStringAsync());
		var operation = (await result.Content.ReadFromJsonAsync<AcademicNotificationOperationDto>(JsonOptions))!;
		operation.Status.ShouldBe(expected);
		return operation;
	}

	private static void Permissions(HttpClient client, string actions)
	{
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, actions);
	}

	private static async Task AssertConflictAsync(HttpResponseMessage response, string code)
	{
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		problem.GetProperty("errorCode").GetString().ShouldBe(code);
	}

	private async Task WithDatabaseAsync(Func<ShikshaMitraDbContext, IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(AcademicNotificationPreviewApiFactory.TenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		await action(db, services);
		await unit.CompleteAsync();
	}

	private Task SeedApproverAsync() => WithDatabaseAsync(async (db, _) =>
	{
		var tenant = AcademicNotificationPreviewApiFactory.TenantId;
		var id = AcademicNotificationPreviewApiFactory.ApproverId;
		if (await db.Set<IdentityUser>().AnyAsync(x => x.Id == id)) return;
		db.Add(new IdentityUser(id, "academic-checker", "checker@example.invalid", tenant));
		db.Add(new UserScopeAssignment(Guid.NewGuid(), tenant, userId: id));
		await db.SaveChangesAsync();
	});

	private static async Task PlanAsync(ShikshaMitraDbContext db, IServiceProvider services, NotificationIntent intent)
	{
		var lease = Guid.NewGuid();
		var entry = db.Entry(intent);
		entry.Property(x => x.Status).CurrentValue = NotificationIntentState.Processing;
		entry.Property(x => x.LeaseId).CurrentValue = lease;
		entry.Property(x => x.LeaseExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(5);
		await db.SaveChangesAsync();
		await services.GetRequiredService<NotificationDispatchPlanner>().PlanAsync(
			new NotificationQueueClaim(intent.Id, AcademicNotificationPreviewApiFactory.TenantId, lease), default);
	}
}
