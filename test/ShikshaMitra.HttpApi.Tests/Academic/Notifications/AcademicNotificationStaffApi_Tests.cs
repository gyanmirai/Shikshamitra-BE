using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AuditLogging;
using Volo.Abp.Emailing;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public class AcademicNotificationStaffApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/academic/notifications";
	private const string Templates = Route + "/templates";
	private const string View = ShikshaMitraSubmodulePermissions.Academics.Notification.View;
	private const string Add = ShikshaMitraSubmodulePermissions.Academics.Notification.Add;
	private const string Event = AcademicNotificationEventKeys.HomeworkPublished;
	private const string RuleEvent = AcademicNotificationEventKeys.HomeworkDue;

	[Fact]
	public async Task Catalogue_reports_the_current_servers_queue_processing_configuration()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, View);
		const string workerKey = "Notifications:QueueWorker:Enabled";
		var configuration = Factory.Services.GetRequiredService<IConfiguration>();
		var previousValue = configuration[workerKey];
		try
		{
			foreach (var (value, enabled) in new (string?, bool)[] { (null, false), ("false", false), ("true", true), ("invalid", false) })
			{
				configuration[workerKey] = value;
				var catalogue = await GetAndReadAsync<JsonElement>($"{Route}/catalogue");
				catalogue.GetProperty("isQueueWorkerEnabled").GetBoolean().ShouldBe(enabled);
			}
		}
		finally
		{
			configuration[workerKey] = previousValue;
		}
	}

	[Fact]
	public async Task Catalogue_reports_event_availability_and_channel_setup_independently()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, View);
		using var result = await Client.GetAsync($"{Route}/catalogue");
		result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
		result.Headers.CacheControl?.NoStore.ShouldBe(true);
		var data = await result.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		var events = data.GetProperty("events").EnumerateArray().ToArray();
		var homework = events
			.Single(x => x.GetProperty("definition").GetProperty("eventKey").GetString() == Event);
		var availability = homework.GetProperty("availability");
		availability.GetProperty("isAvailable").GetBoolean().ShouldBeTrue();
		availability.GetProperty("reasonCode").ValueKind.ShouldBe(JsonValueKind.Null);
		homework.TryGetProperty("availabilityDetail", out var homeworkDetail).ShouldBeTrue();
		homeworkDetail.ValueKind.ShouldBe(JsonValueKind.Null);
		var unavailable = events.Where(x => !x.GetProperty("availability").GetProperty("isAvailable").GetBoolean()).ToArray();
		unavailable.Length.ShouldBe(22);
		foreach (var pending in unavailable)
		{
			pending.GetProperty("availability").GetProperty("reasonCode").GetString().ShouldBe(NotificationErrorCodes.SourceUnavailable);
			pending.GetProperty("availabilityDetail").GetString().ShouldNotBeNullOrWhiteSpace();
			pending.GetProperty("manualAvailability").GetProperty("isAvailable").GetBoolean().ShouldBeFalse();
		}
		var reschedule = unavailable.Single(x =>
			x.GetProperty("definition").GetProperty("eventKey").GetString() == AcademicNotificationEventKeys.ExamRescheduled);
		reschedule.GetProperty("availabilityDetail").GetString().ShouldBe(
			"This notice requires a saved exam reschedule and a list of the people affected.");
		var seatingChange = unavailable.Single(x =>
			x.GetProperty("definition").GetProperty("eventKey").GetString() == AcademicNotificationEventKeys.SeatingChanged);
		seatingChange.GetProperty("availabilityDetail").GetString().ShouldContain("Published seating is locked.");
		var resultCorrection = unavailable.Single(x =>
			x.GetProperty("definition").GetProperty("eventKey").GetString() == AcademicNotificationEventKeys.ResultCorrected);
		resultCorrection.GetProperty("availabilityDetail").GetString().ShouldContain("correction and republication");
		data.GetProperty("channels").GetArrayLength().ShouldBe(4);
		var raw = await result.Content.ReadAsStringAsync();
		raw.ShouldNotContain("privateKey", Case.Insensitive);
		raw.ShouldNotContain("secretReference", Case.Insensitive);
	}

	[Fact]
	public async Task Catalogue_requires_usable_smtp_configuration_for_an_enabled_email_provider()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		const string providers = "/api/communication/provider-settings";
		var permissions = $"{View},{ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add}," +
			ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Delete;
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions);
		var settings = Factory.Services.GetRequiredService<IConfiguration>().GetSection("Settings");
		var fixture = new Dictionary<string, string?> {
			[EmailSettingNames.Smtp.Host] = "127.0.0.1",
			[EmailSettingNames.Smtp.Port] = "2525",
			[EmailSettingNames.Smtp.UseDefaultCredentials] = "false",
			[EmailSettingNames.Smtp.UserName] = "",
			[EmailSettingNames.DefaultFromAddress] = "fixture@example.invalid",
			[EmailSettingNames.DefaultFromDisplayName] = "Fixture school"
		};
		var previous = fixture.Keys.ToDictionary(key => key, key => settings[key]);
		var provider = await PostAndReadAsync<ChannelProviderSettingDto>(providers, new CreateUpdateChannelProviderSettingDto {
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true, Status = CommunicationProviderStatus.Enabled
		});
		try
		{
			foreach (var (key, value) in fixture) settings[key] = value;
			(await EmailConfiguredAsync()).ShouldBeFalse();
			settings[EmailSettingNames.Smtp.UseDefaultCredentials] = "true";
			(await EmailConfiguredAsync()).ShouldBeTrue();
			settings[EmailSettingNames.Smtp.Port] = "invalid";
			(await EmailConfiguredAsync()).ShouldBeFalse();
		}
		finally
		{
			foreach (var (key, value) in previous) settings[key] = value;
			using var removed = await Client.DeleteAsync($"{providers}/{provider.Id}");
			removed.IsSuccessStatusCode.ShouldBeTrue();
		}

		async Task<bool> EmailConfiguredAsync()
		{
			var catalogue = await GetAndReadAsync<AcademicNotificationCatalogueDto>($"{Route}/catalogue");
			return catalogue.Channels.Single(row => row.Channel == CommunicationChannelType.Email).IsConfigured;
		}
	}

	[Fact]
	public async Task Authorized_staff_can_initialize_review_and_publish_an_immutable_template()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Add}");
		var initialize = new { eventKey = Event };
		var created = await PostAndReadAsync<int>($"{Templates}/initialize-defaults", initialize);
		created.ShouldBeGreaterThan(0);
		(await PostAndReadAsync<int>($"{Templates}/initialize-defaults", initialize)).ShouldBe(0);

		var templates = await GetAndReadAsync<PagedResultDto<NotificationTemplateDto>>($"{Templates}?eventKey={Event}&maxResultCount=100");
		var draft = templates.Items.Single(x => x.RecipientKind == "Guardian" && x.Channel == CommunicationChannelType.InApp);
		draft.IsPublished.ShouldBeFalse();
		var preview = await GetAndReadAsync<NotificationTemplatePreviewDto>($"{Templates}/{draft.TemplateId}/preview/{draft.RevisionId}");
		preview.IsSynthetic.ShouldBeTrue();
		using var stale = await Client.PutAsJsonAsync($"{Templates}/{draft.TemplateId}/draft",
			new UpdateNotificationTemplateDraftDto(draft.RevisionId, draft.Subject, draft.Body, "stale-revision"));
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		var published = await PostAndReadAsync<NotificationTemplateDto>($"{Templates}/{draft.TemplateId}/publish",
			new NotificationTemplateRevisionCommandDto(draft.RevisionId, draft.ConcurrencyStamp));
		published.IsPublished.ShouldBeTrue();
		var current = await GetAndReadAsync<NotificationTemplateDto>($"{Templates}/{published.TemplateId}");
		var nextDraft = await PostAndReadAsync<NotificationTemplateDto>(Templates, new CreateNotificationTemplateDto(
			Event, published.RecipientKind, published.Channel, published.LanguageCode, published.TemplateName,
			published.Subject + " v2", published.Body, ExistingTemplateId: published.TemplateId,
			HeaderConcurrencyStamp: current.HeaderConcurrencyStamp));
		nextDraft.IsPublished.ShouldBeFalse();
		var options = await GetAndReadAsync<PagedResultDto<JsonElement>>($"{Templates}/options?eventKey={Event}&maxResultCount=100");
		var option = options.Items.Where(item => item.GetProperty("templateId").GetGuid() == published.TemplateId)
			.ShouldHaveSingleItem();
		option.TryGetProperty("body", out _).ShouldBeFalse();
		option.GetProperty("revisionId").GetGuid().ShouldBe(published.RevisionId);
		using var change = await Client.PutAsJsonAsync($"{Templates}/{draft.TemplateId}/draft",
			new UpdateNotificationTemplateDraftDto(published.RevisionId, published.Subject, published.Body, published.ConcurrencyStamp));
		change.IsSuccessStatusCode.ShouldBeFalse();
		var audit = (await ReadStaffAuditAsync("PublishTemplate", published.RevisionId)).ShouldHaveSingleItem();
		var metadata = JsonSerializer.Deserialize<JsonElement>(audit);
		metadata.GetProperty("templateId").GetGuid().ShouldBe(published.TemplateId);
		metadata.GetProperty("revisionId").GetGuid().ShouldBe(published.RevisionId);
		metadata.GetProperty("eventKey").GetString().ShouldBe(Event);
		metadata.GetProperty("revision").GetInt32().ShouldBe(published.Revision);
		metadata.EnumerateObject().Count().ShouldBe(6);
	}

	[Fact]
	public async Task View_and_send_permissions_do_not_allow_template_creation()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var send = ShikshaMitraSubmodulePermissions.Academics.Notification.Send;
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{send}");
		using var result = await Client.PostAsJsonAsync($"{Templates}/initialize-defaults", new { eventKey = Event });
		result.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await result.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task A_disabled_division_rule_overrides_an_enabled_school_rule_and_rejects_stale_changes()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedEnabledSchoolRuleAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Add}");
		var scoped = new AcademicNotificationScopeDto(Factory.SeededAcademicYearId, Factory.SeededBoardId,
			Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId);
		var list = $"{Route}/rules?eventKey={RuleEvent}&academicYearId={scoped.AcademicYearId}&boardId={scoped.BoardId}" +
			$"&mediumId={scoped.MediumId}&classId={scoped.ClassId}&divisionId={scoped.DivisionId}";
		var inherited = (await GetAndReadAsync<PagedResultDto<AcademicNotificationRuleListItemDto>>(list)).Items.ShouldHaveSingleItem();
		inherited.IsInherited.ShouldBeTrue();
		inherited.EffectiveRule!.Enabled.ShouldBeTrue();

		var saveUrl = $"{Route}/rules/{RuleEvent}";
		using var create = await Client.PutAsJsonAsync(saveUrl, new SaveAcademicNotificationRuleDto { EventKey = RuleEvent, Scope = scoped });
		create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
		var disabled = (await create.Content.ReadFromJsonAsync<AcademicNotificationRuleDto>(JsonOptions))!;
		var result = (await GetAndReadAsync<PagedResultDto<AcademicNotificationRuleListItemDto>>(list)).Items.ShouldHaveSingleItem();
		result.IsInherited.ShouldBeFalse();
		result.EffectiveRule!.Id.ShouldBe(disabled.Id);
		result.EffectiveRule.Enabled.ShouldBeFalse();
		result.EffectiveRule.Configuration!.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
		result.EffectiveRule.Configuration.AdditionalGuardianRelationships.ShouldBeEmpty();
		result.EffectiveRule.Configuration.IncludeStudentAccount.ShouldBeFalse();
		result.EffectiveRule.Configuration.Channels.ShouldBe([CommunicationChannelType.InApp]);

		var update = new SaveAcademicNotificationRuleDto { EventKey = RuleEvent, Scope = scoped,
			ConcurrencyStamp = disabled.ConcurrencyStamp, GuardianMode = NotificationGuardianMode.PrimaryAndSelected,
			AdditionalGuardianRelationships = ["mother"], IncludeStudentAccount = true };
		using var saved = await Client.PutAsJsonAsync(saveUrl, update);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var current = (await saved.Content.ReadFromJsonAsync<AcademicNotificationRuleDto>(JsonOptions))!;
		current.Revision.ShouldBe(disabled.Revision + 1);
		current.Configuration!.AdditionalGuardianRelationships.ShouldBe(["mother"]);
		current.Configuration.IncludeStudentAccount.ShouldBeTrue();
		using var stale = await Client.PutAsJsonAsync(saveUrl, update);
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		using var unchanged = await Client.PutAsJsonAsync(saveUrl, new SaveAcademicNotificationRuleDto { EventKey = RuleEvent, Scope = scoped,
			ConcurrencyStamp = current.ConcurrencyStamp, GuardianMode = NotificationGuardianMode.PrimaryAndSelected,
			AdditionalGuardianRelationships = ["mother"], IncludeStudentAccount = true });
		unchanged.StatusCode.ShouldBe(HttpStatusCode.OK, await unchanged.Content.ReadAsStringAsync());
		var audits = await ReadStaffAuditAsync("SaveRule", disabled.Id);
		audits.Length.ShouldBe(2);
		var metadata = audits.Select(x => JsonSerializer.Deserialize<JsonElement>(x)).OrderBy(x => x.GetProperty("revision").GetInt32()).ToArray();
		metadata.Select(x => x.GetProperty("revision").GetInt32()).ShouldBe([disabled.Revision, current.Revision]);
		foreach (var change in metadata)
		{
			change.GetProperty("eventKey").GetString().ShouldBe(RuleEvent);
			change.GetProperty("enabled").GetBoolean().ShouldBeFalse();
			change.GetProperty("ruleId").GetGuid().ShouldBe(disabled.Id);
			change.EnumerateObject().Count().ShouldBe(4);
		}
	}

	[Fact]
	public async Task Rule_write_rejects_unknown_event_unsupported_channel_and_unresolvable_template()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Add}");
		const string unknown = "Academic.Homework.Unknown";
		using var eventResponse = await Client.PutAsJsonAsync($"{Route}/rules/{unknown}",
			new SaveAcademicNotificationRuleDto { EventKey = unknown, Scope = new() });
		(await ErrorCodeAsync(eventResponse)).ShouldBe(NotificationErrorCodes.EventUnknown);

		using var channelResponse = await Client.PutAsJsonAsync($"{Route}/rules/{Event}",
			new SaveAcademicNotificationRuleDto { EventKey = Event, Scope = new(), Channels = [CommunicationChannelType.Sms] });
		(await ErrorCodeAsync(channelResponse)).ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid);

		var configured = (await GetAndReadAsync<PagedResultDto<AcademicNotificationRuleListItemDto>>(
			$"{Route}/rules?eventKey={Event}")).Items.ShouldHaveSingleItem().EffectiveRule;
		using var templateResponse = await Client.PutAsJsonAsync($"{Route}/rules/{Event}",
			new SaveAcademicNotificationRuleDto { EventKey = Event, Scope = new(), TemplateIds = [Guid.NewGuid()],
				ConcurrencyStamp = configured?.ConcurrencyStamp });
		(await ErrorCodeAsync(templateResponse)).ShouldBe(NotificationErrorCodes.TemplateInvalid);
	}

	[Fact]
	public async Task Rule_endpoints_reject_a_mismatched_hierarchy_and_arbitrary_destinations()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Add}");
		using var foreign = await Client.GetAsync($"{Route}/rules?academicYearId={Factory.SeededAcademicYearId}&boardId={Guid.NewGuid()}");
		foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await foreign.Content.ReadAsStringAsync());
		using var invalid = await Client.PutAsJsonAsync($"{Route}/rules/{Event}",
			new { eventKey = Event, scope = new { }, email = "not-a-recipient@example.test" });
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await invalid.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task A_division_assignment_can_configure_its_division_and_cannot_write_school_settings()
	{
		var staffId = await SeedDivisionStaffAsync();
		using var staff = CreateClientAs(staffId);
		staff.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		staff.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{View},{Add}");
		var url = $"{Route}/rules/{AcademicNotificationEventKeys.AssignmentDue}";
		var selected = new AcademicNotificationScopeDto(Factory.SeededAcademicYearId, Factory.SeededBoardId,
			Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId);
		using var denied = await staff.PutAsJsonAsync(url, new SaveAcademicNotificationRuleDto
		{
			EventKey = AcademicNotificationEventKeys.AssignmentDue,
			Scope = new()
		});
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		using var allowed = await staff.PutAsJsonAsync(url, new SaveAcademicNotificationRuleDto
		{
			EventKey = AcademicNotificationEventKeys.AssignmentDue,
			Scope = selected
		});
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
	}

	private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
	{
		var body = await response.Content.ReadAsStringAsync();
		response.IsSuccessStatusCode.ShouldBeFalse(body);
		using var error = JsonDocument.Parse(body);
		error.RootElement.TryGetProperty("errorCode", out var code).ShouldBeTrue($"{response.StatusCode} {body}");
		return code.GetString();
	}

	private async Task<string[]> ReadStaffAuditAsync(string action, Guid entityId)
	{
		using var serviceScope = Factory.Services.CreateScope();
		var services = serviceScope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var actorId = Guid.Parse(TestAuthHandler.TestUserId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin();
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var logs = await db.Set<AuditLog>().AsNoTracking().Include(x => x.Actions)
			.Where(x => x.TenantId == tenantId && x.UserId == actorId).ToListAsync();
		await unit.CompleteAsync();
		return logs.SelectMany(x => x.Actions).Where(x => x.ServiceName == "AcademicNotifications" &&
			x.MethodName == action && x.Parameters.Contains(entityId.ToString(), StringComparison.Ordinal)).Select(x => x.Parameters).ToArray();
	}

	private async Task SeedEnabledSchoolRuleAsync()
	{
		using var serviceScope = Factory.Services.CreateScope();
		var services = serviceScope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(RuleEvent);
		var codec = services.GetRequiredService<NotificationRuleConfigurationCodec>();
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
			[], [CommunicationChannelType.InApp], [Guid.NewGuid()], NotificationRuleTimingConfiguration.Default, "en");
		var rule = new NotificationRule(Guid.NewGuid(), tenantId, RuleEvent, NotificationRuleScope.Create());
		rule.Configure(true, codec.Serialize(config, definition), rule.ConcurrencyStamp);
		db.Add(rule);
		await unit.CompleteAsync();
	}

	private async Task<Guid> SeedDivisionStaffAsync()
	{
		using var serviceScope = Factory.Services.CreateScope();
		var services = serviceScope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var staffId = Guid.NewGuid();
		db.Add(new IdentityUser(staffId, $"staff_{staffId:N}", $"staff_{staffId:N}@example.test", tenantId));
		db.Add(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: staffId, academicYearId: Factory.SeededAcademicYearId,
			educationBoardId: Factory.SeededBoardId, educationMediumId: Factory.SeededMediumId,
			educationMediumStageId: Factory.SeededMediumStageId, standardId: Factory.SeededStandardId, divisionId: Factory.SeededDivisionId));
		await unit.CompleteAsync();
		return staffId;
	}
}
