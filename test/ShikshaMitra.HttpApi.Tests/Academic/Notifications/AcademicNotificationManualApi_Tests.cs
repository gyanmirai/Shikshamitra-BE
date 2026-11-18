using System;
using System.Linq;
using System.Net;
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
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(AcademicNotificationPreviewApiCollection.Name)]
public class AcademicNotificationManualApi_Tests(AcademicNotificationPreviewApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BasePath = "/api/academic/notifications";
	private const string Route = BasePath + "/manual/preview";
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

	private static object Request(Guid? templateId = null, Guid? studentId = null) => new
	{
		eventKey = Event, sourceType = SourceType, sourceId = AcademicNotificationPreviewApiFactory.SourceId,
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
		stored.ConsumedAt.ShouldBeNull();
		(await db.NotificationIntents.AnyAsync(x => x.SourceId == AcademicNotificationPreviewApiFactory.SourceId)).ShouldBeFalse();
		(await db.CommunicationCampaigns.AnyAsync(x => x.SourceEntityId == AcademicNotificationPreviewApiFactory.SourceId)).ShouldBeFalse();
		await unit.CompleteAsync();
		return stored;
	}
}
