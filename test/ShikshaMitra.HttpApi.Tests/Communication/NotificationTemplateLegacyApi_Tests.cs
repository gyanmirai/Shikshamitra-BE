using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication;

[Collection(ApiTestCollection.Name)]
public class NotificationTemplateLegacyApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Templates = "/api/communication/templates";
	private const string Campaigns = "/api/communication/campaigns";
	private const string WorkflowError = "Communication:Notification:UseNotificationWorkflow";

	[Fact]
	public async Task Legacy_edit_of_a_linked_template_appends_a_draft_and_preserves_published_content()
	{
		var header = await CreateHeaderAsync();
		var version = await LinkAsync(header.Id);
		using var response = await Client.PutAsJsonAsync($"{Templates}/{header.Id}", Input(header.TemplateName, "Updated {{recipient_name}}"));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var updated = await response.Content.ReadFromJsonAsync<CommunicationTemplateDto>(JsonOptions);
		updated!.Version.ShouldBe(2); updated.ApprovalStatus.ShouldBe(CommunicationTemplateApprovalStatus.Draft);
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var rows = await db.Set<CommunicationTemplateRevision>().Where(x => x.TemplateId == header.Id).OrderBy(x => x.Revision).ToListAsync();
		rows.Count.ShouldBe(2);
		rows[0].Id.ShouldBe(version); rows[0].Body.ShouldBe("Original body"); rows[0].IsPublished.ShouldBeTrue();
		rows[1].Body.ShouldBe("Updated {{recipient_name}}"); rows[1].IsPublished.ShouldBeFalse();
	}

	[Fact]
	public async Task An_unlinked_legacy_template_keeps_its_existing_update_and_delete_behavior()
	{
		var header = await CreateHeaderAsync();
		using var update = await Client.PutAsJsonAsync($"{Templates}/{header.Id}", Input(header.TemplateName, "Legacy edit"));
		update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
		using var deletion = await Client.DeleteAsync($"{Templates}/{header.Id}");
		deletion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Legacy_browse_excludes_linked_templates_and_detail_requires_academic_access(bool tombstone)
	{
		var linked = await CreateHeaderAsync();
		var unlinked = await CreateHeaderAsync();
		await LinkAsync(linked.Id, tombstone);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.View);

		using var hidden = await Client.GetAsync($"{Templates}?search={Uri.EscapeDataString(linked.TemplateName)}");
		hidden.StatusCode.ShouldBe(HttpStatusCode.OK);
		var hiddenPage = await hidden.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		hiddenPage.GetProperty("totalCount").GetInt32().ShouldBe(0);
		hiddenPage.GetProperty("items").GetArrayLength().ShouldBe(0);
		using var denied = await Client.GetAsync($"{Templates}/{linked.Id}");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		using var visible = await Client.GetAsync($"{Templates}?search={Uri.EscapeDataString(unlinked.TemplateName)}");
		visible.StatusCode.ShouldBe(HttpStatusCode.OK);
		var visiblePage = await visible.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		visiblePage.GetProperty("totalCount").GetInt32().ShouldBe(1);
		using var ordinary = await Client.GetAsync($"{Templates}/{unlinked.Id}");
		ordinary.StatusCode.ShouldBe(HttpStatusCode.OK);

		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		using var authorized = await Client.GetAsync($"{Templates}/{linked.Id}");
		authorized.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Legacy_delete_cannot_remove_published_or_deleted_draft_notification_binding(bool tombstone)
	{
		var header = await CreateHeaderAsync(); await LinkAsync(header.Id, tombstone);
		using var deletion = await Client.DeleteAsync($"{Templates}/{header.Id}");
		deletion.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await deletion.Content.ReadAsStringAsync());
		(await deletion.Content.ReadAsStringAsync()).ShouldContain(NotificationErrorCodes.TemplateImmutable);
	}

	[Theory]
	[InlineData("delete")]
	[InlineData("submit-for-approval")]
	[InlineData("approve")]
	[InlineData("reject")]
	[InlineData("cancel")]
	[InlineData("update")]
	public async Task Generic_campaign_mutations_cannot_change_pipeline_owned_history_or_review_state(string operation)
	{
		var id = await CreateCampaignRowAsync("AcademicNotification", null);
		using var response = operation switch {
			"delete" => await Client.DeleteAsync($"{Campaigns}/{id}"),
			"update" => await Client.PutAsJsonAsync($"{Campaigns}/{id}", new CreateUpdateCommunicationCampaignDto { Title = "Renamed", Body = "Body" }),
			"approve" or "reject" => await Client.PutAsJsonAsync($"{Campaigns}/{id}/{operation}", new CommunicationApprovalDto { Reason = "Generic review" }),
			_ => await Client.PutAsync($"{Campaigns}/{id}/{operation}", null)
		};
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain(WorkflowError);
	}

	[Theory]
	[InlineData("publish", true)]
	[InlineData("clone", true)]
	[InlineData("publish", false)]
	[InlineData("clone", false)]
	public async Task Generic_send_and_clone_cannot_bypass_the_notification_workflow(string operation, bool pipelineType)
	{
		Guid? templateId = null;
		if (!pipelineType) { var header = await CreateHeaderAsync(); await LinkAsync(header.Id); templateId = header.Id; }
		var id = await CreateCampaignRowAsync(pipelineType ? "AcademicNotification" : "Notification", templateId);
		using var response = operation == "publish" ? await Client.PutAsync($"{Campaigns}/{id}/publish", null)
			: await Client.PostAsync($"{Campaigns}/{id}/clone", null);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain(WorkflowError);
	}

	private async Task<CommunicationTemplateDto> CreateHeaderAsync()
	{
		using var response = await Client.PostAsJsonAsync(Templates, Input("Legacy " + Guid.NewGuid().ToString("N"), "Original body"));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<CommunicationTemplateDto>(JsonOptions))!;
	}
	private static CreateUpdateCommunicationTemplateDto Input(string name, string body) => new() {
		TemplateName = name, Subject = "Original subject", Body = body, Channels = { CommunicationChannelType.Email }, LanguageCode = "en"
	};
	private async Task<Guid> LinkAsync(Guid templateId, bool tombstone = false)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var header = await db.Set<CommunicationTemplate>().SingleAsync(x => x.Id == templateId);
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), header.TenantId!.Value, header.Id, 1,
			"Academic.Homework.Published", "Guardian", CommunicationChannelType.Email, "en", header.Subject, header.Body, 1);
		if (!tombstone) revision.Publish(Guid.Parse(TestAuthHandler.TestUserId), DateTimeOffset.UtcNow);
		revision.IsDeleted = tombstone; db.Add(revision); await db.SaveChangesAsync();
		if (!tombstone) { header.SetNotificationRevision(revision.Id); await db.SaveChangesAsync(); }
		await uow.CompleteAsync(); return revision.Id;
	}
	private async Task<Guid> CreateCampaignRowAsync(string type, Guid? templateId)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var campaign = new CommunicationCampaign(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId)) {
			CampaignType = type, TemplateId = templateId, Title = "Notification guard test", Subject = "Guard", Body = "Guard body"
		};
		db.Add(campaign); await uow.CompleteAsync(); return campaign.Id;
	}
}
