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
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[Collection(ApiTestCollection.Name)]
public class NotificationLegacyReadApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Campaigns = "/api/communication/campaigns";
	[Theory]
	[InlineData("AcademicNotification")]
	[InlineData("academicnotification")]
	[InlineData(" AcademicNotification ")]
	public async Task Generic_lists_exclude_pipeline_content_and_its_total_count(string type)
	{
		var marker = "Notification privacy " + Guid.NewGuid().ToString("N");
		var hidden = await SeedAsync(type, marker); var ordinary = await SeedAsync("Notification", marker);
		using var response = await Client.GetAsync($"{Campaigns}?search={Uri.EscapeDataString(marker)}&maxResultCount=100");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("totalCount").GetInt64().ShouldBe(1);
		var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
		ids.ShouldContain(ordinary.Campaign); ids.ShouldNotContain(hidden.Campaign);
	}
	[Theory]
	[InlineData("/api/communication/campaigns/{0}")]
	[InlineData("/api/notifications/{0}")]
	[InlineData("/api/notifications/{0}/step-one")]
	[InlineData("/api/notifications/{0}/step-three")]
	[InlineData("/api/announcements/{0}")]
	public async Task Generic_and_legacy_detail_routes_require_the_owning_workflow(string route)
	{
		var row = await SeedAsync("AcademicNotification", "Protected detail");
		using var response = await Client.GetAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, route, row.Campaign));
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync(); body.ShouldContain(NotificationErrorCodes.UseWorkflow);
		body.ShouldNotContain(row.PrivateText);
	}
	[Theory]
	[InlineData("delivery-attempts", false)]
	[InlineData("delivery-attempts", true)]
	[InlineData("replies", false)]
	[InlineData("replies", true)]
	public async Task Child_history_queries_cannot_expose_pipeline_destinations_or_replies_even_after_parent_archive(string endpoint, bool archived)
	{
		var row = await SeedAsync("AcademicNotification", "Protected history", archived);
		using var response = await Client.GetAsync($"/api/communication/{endpoint}?campaignId={row.Campaign}&maxResultCount=100");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(body);
		json.RootElement.GetProperty("totalCount").GetInt64().ShouldBe(0);
		json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0); body.ShouldNotContain(row.PrivateText);
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Generic_history_still_contains_ordinary_campaign_rows(bool archived)
	{
		var row = await SeedAsync("Notification", "Ordinary history", archived);
		foreach (var endpoint in new[] { "delivery-attempts", "replies" })
		{
			using var response = await Client.GetAsync($"/api/communication/{endpoint}?campaignId={row.Campaign}&maxResultCount=100");
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
		}
	}
	[Fact]
	public async Task Reply_pages_order_by_received_instant_before_applying_skip_and_take()
	{
		var row = await SeedAsync("Notification", "Ordinary reply ordering"); var newerId = Guid.NewGuid();
		await WithDbAsync(async db => {
			(await db.Set<CommunicationReply>().SingleAsync(x => x.Id == row.Reply)).ReceivedAt = new(2026, 11, 18, 15, 0, 0, TimeSpan.FromHours(5.5));
			db.Add(new CommunicationReply(newerId, Guid.Parse(TestAuthHandler.TestTenantId)) {
				CampaignId = row.Campaign, ReceivedAt = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero), Body = "Newer instant"
			});
		});
		for (var skip = 0; skip < 2; skip++)
		{
			using var response = await Client.GetAsync($"/api/communication/replies?campaignId={row.Campaign}&skipCount={skip}&maxResultCount=1");
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			json.RootElement.GetProperty("totalCount").GetInt64().ShouldBe(2);
			json.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(skip == 0 ? newerId : row.Reply);
		}
	}
	[Fact]
	public async Task Operational_dashboard_counts_do_not_include_pipeline_payload_work()
	{
		var before = await ReadDashboardAsync(); await SeedAsync("AcademicNotification", "Protected dashboard");
		var after = await ReadDashboardAsync();
		after.TotalCampaigns.ShouldBe(before.TotalCampaigns); after.DraftCampaigns.ShouldBe(before.DraftCampaigns);
		after.DeliveryAttemptCount.ShouldBe(before.DeliveryAttemptCount); after.OpenReplies.ShouldBe(before.OpenReplies);
		after.EstimatedCost.ShouldBe(before.EstimatedCost);
	}
	[Theory]
	[InlineData("campaign")]
	[InlineData("attempt")]
	[InlineData("recipient")]
	public async Task Generic_reply_capture_cannot_attach_to_pipeline_records_through_any_reference(string reference)
	{
		var row = await SeedAsync("AcademicNotification", "Protected reply reference");
		using var response = await Client.PostAsJsonAsync("/api/communication/replies", new CaptureCommunicationReplyDto {
			CampaignId = reference == "campaign" ? row.Campaign : null,
			DeliveryAttemptId = reference == "attempt" ? row.Attempt : null,
			RecipientSnapshotId = reference == "recipient" ? row.Recipient : null,
			Channel = CommunicationChannelType.Email, FromAddress = "test@example.test", Body = "Untrusted capture"
		});
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain(NotificationErrorCodes.UseWorkflow);
	}
	[Fact]
	public async Task A_reply_with_only_an_indirect_pipeline_reference_is_not_visible_or_assignable()
	{
		var row = await SeedAsync("AcademicNotification", "Protected indirect reply");
		await WithDbAsync(async db => {
			var reply = await db.Set<CommunicationReply>().SingleAsync(x => x.Id == row.Reply);
			reply.CampaignId = null;
		});
		using var list = await Client.GetAsync("/api/communication/replies?maxResultCount=1000");
		list.StatusCode.ShouldBe(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());
		(await list.Content.ReadAsStringAsync()).ShouldNotContain(row.PrivateText);
		using var update = await Client.PutAsJsonAsync($"/api/communication/replies/{row.Reply}/assign", new AssignCommunicationReplyDto {
			Status = CommunicationReplyStatus.Assigned, AssignedToUserId = Guid.Parse(TestAuthHandler.TestUserId)
		});
		update.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await update.Content.ReadAsStringAsync());
		(await update.Content.ReadAsStringAsync()).ShouldContain(NotificationErrorCodes.UseWorkflow);
	}
	private async Task<CommunicationDashboardDto> ReadDashboardAsync()
	{
		using var response = await Client.GetAsync("/api/communication/dashboard");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<CommunicationDashboardDto>(JsonOptions))!;
	}
	[Fact]
	public async Task Usage_billing_remains_complete_while_academic_payloads_use_their_own_history()
	{
		var row = await SeedAsync("AcademicNotification", "Protected billing projection");
		var before = await ReadDashboardAsync();
		await WithDbAsync(db => {
			db.Add(new UsageLedgerEntry(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId)) {
				CampaignId = row.Campaign, Channel = CommunicationChannelType.Email, ProviderName = "Synthetic provider", EstimatedCost = 1.25m, AttemptCount = 1
			});
			return Task.CompletedTask;
		});
		(await ReadDashboardAsync()).EstimatedCost.ShouldBe(before.EstimatedCost + 1.25m);
		using var response = await Client.GetAsync($"/api/communication/usage-ledger?campaignId={row.Campaign}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(body);
		json.RootElement.GetProperty("totalCount").GetInt64().ShouldBe(1);
		json.RootElement.GetProperty("items")[0].GetProperty("estimatedCost").GetDecimal().ShouldBe(1.25m);
		body.ShouldNotContain(row.PrivateText);
	}
	[Fact]
	public async Task The_existing_view_action_runs_before_the_workflow_guard()
	{
		var row = await SeedAsync("AcademicNotification", "Protected denied detail");
		using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"{Campaigns}/{row.Campaign}");
		request.Headers.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		request.Headers.Add(TestAuthHandler.TestRolesHeader, "unassigned");
		request.Headers.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Academics.Notification.View");
		using var response = await Client.SendAsync(request);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync(); body.ShouldNotContain(row.PrivateText); body.ShouldNotContain(NotificationErrorCodes.UseWorkflow);
	}
	private async Task<(Guid Campaign, Guid Recipient, Guid Attempt, Guid Reply, string PrivateText)> SeedAsync(string type, string title, bool archived = false)
	{
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId); var privateText = "Private notification " + Guid.NewGuid().ToString("N");
		var campaign = new CommunicationCampaign(Guid.NewGuid(), tenant) { CampaignType = type, Title = title, Body = privateText, IsDeleted = archived };
		var recipient = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant) { CampaignId = campaign.Id, RecipientType = "ParentGuardian", DisplayName = privateText };
		var attempt = new DeliveryAttempt(Guid.NewGuid(), tenant) { CampaignId = campaign.Id, RecipientSnapshotId = recipient.Id, Destination = privateText };
		var reply = new CommunicationReply(Guid.NewGuid(), tenant) { CampaignId = campaign.Id, DeliveryAttemptId = attempt.Id, RecipientSnapshotId = recipient.Id, Body = privateText };
		await WithDbAsync(db => { db.AddRange(campaign, recipient, attempt, reply); return Task.CompletedTask; });
		return (campaign.Id, recipient.Id, attempt.Id, reply.Id, privateText);
	}
	private async Task WithDbAsync(Func<ShikshaMitraDbContext, Task> action)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		await action(db); await uow.CompleteAsync();
	}
}
