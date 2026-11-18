using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateRevision_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _actorId = Guid.NewGuid();
	private readonly List<CommunicationTemplate> _headerRows = [];
	private readonly List<CommunicationTemplateRevision> _revisionRows = [];
	private readonly INotificationTemplateAccessPolicy _policy = Substitute.For<INotificationTemplateAccessPolicy>();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly ICurrentUser _current = Substitute.For<ICurrentUser>();
	private readonly NotificationTemplateRevisionService _service;

	public NotificationTemplateRevision_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_current.Id.Returns(_actorId); _current.TenantId.Returns(_tenantId); _current.IsAuthenticated.Returns(true);
		var headers = Substitute.For<IRepository<CommunicationTemplate, Guid>>();
		headers.GetQueryableAsync().Returns(_headerRows.AsQueryable());
		headers.InsertAsync(Arg.Any<CommunicationTemplate>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<CommunicationTemplate>(); _headerRows.Add(row); return row;
		});
		headers.UpdateAsync(Arg.Any<CommunicationTemplate>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<CommunicationTemplate>());
		var revisions = Substitute.For<IRepository<CommunicationTemplateRevision, Guid>>();
		revisions.GetQueryableAsync().Returns(_revisionRows.AsQueryable());
		revisions.InsertAsync(Arg.Any<CommunicationTemplateRevision>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<CommunicationTemplateRevision>(); _revisionRows.Add(row); return row;
		});
		revisions.UpdateAsync(Arg.Any<CommunicationTemplateRevision>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<CommunicationTemplateRevision>());
		revisions.DeleteAsync(Arg.Any<CommunicationTemplateRevision>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			call.Arg<CommunicationTemplateRevision>().IsDeleted = true; return Task.CompletedTask;
		});
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		_policy.SupportsEvent(Arg.Any<string>()).Returns(true);
		var filters = Substitute.For<IDataFilter>(); filters.Disable<ISoftDelete>().Returns(Substitute.For<IDisposable>());
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example" }).Build();
		_service = new(_tenant, _current, headers, revisions, new AsyncQueryableExecuter([]), guids, filters, [_policy],
			new NotificationTemplateVariableValidator([new AcademicNotificationCatalogue([])], configuration), new());
	}

	[Fact]
	public async Task Defaults_cover_every_catalogue_variant_as_unpublished_english_drafts_and_reinitialization_preserves_school_edits()
	{
		var definitions = new AcademicNotificationCatalogue([]).GetAll();
		var expected = definitions.Sum(x => x.RecipientSelectors.Select(RecipientKind).Distinct().Count() * x.SupportedChannels.Count);
		foreach (var definition in definitions) await _service.InitializeDefaultsAsync(definition.EventKey);
		_headerRows.Count.ShouldBe(expected); _revisionRows.Count.ShouldBe(expected);
		_revisionRows.ShouldAllBe(x => !x.IsPublished && !x.IsRetired && x.LanguageCode == "en" && x.Revision == 1);
		_headerRows.ShouldAllBe(x => x.TenantId == _tenantId && x.CurrentNotificationRevisionId.HasValue);
		var first = _revisionRows[0];
		var edited = await _service.UpdateDraftAsync(first.TemplateId, new(first.Id, "School wording", "Our own message", first.ConcurrencyStamp));
		foreach (var definition in definitions) (await _service.InitializeDefaultsAsync(definition.EventKey)).ShouldBe(0);
		_headerRows.Count.ShouldBe(expected); _revisionRows.Count.ShouldBe(expected);
		first.Body.ShouldBe(edited.Body); first.Subject.ShouldBe("School wording");
		new AcademicNotificationCatalogue([]).GetAvailability(definitions[0].EventKey).IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task Default_initialization_does_not_resurrect_deleted_headers_or_deleted_drafts()
	{
		(await _service.InitializeDefaultsAsync(Input().EventKey)).ShouldBeGreaterThan(0);
		var header = _headerRows[0]; header.IsDeleted = true;
		_revisionRows[1].IsDeleted = true; var count = _headerRows.Count;
		(await _service.InitializeDefaultsAsync(Input().EventKey)).ShouldBe(0);
		_headerRows.Count.ShouldBe(count); header.IsDeleted.ShouldBeTrue(); _revisionRows[1].IsDeleted.ShouldBeTrue();
	}

	[Fact]
	public async Task Defaults_require_school_wide_write_authorization_before_any_rows_are_created()
	{
		_policy.EnsureAsync(Input().EventKey, null, NotificationTemplateAction.Write, Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new AbpAuthorizationException()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.InitializeDefaultsAsync(Input().EventKey));
		_headerRows.ShouldBeEmpty(); _revisionRows.ShouldBeEmpty();
	}

	[Fact]
	public async Task Default_initialization_isolates_tenants_even_when_the_repository_exposes_foreign_rows()
	{
		(await _service.InitializeDefaultsAsync(Input().EventKey)).ShouldBeGreaterThan(0);
		var own = _headerRows.ToArray();
		var otherTenant = Guid.NewGuid(); _tenant.Id.Returns(otherTenant); _current.TenantId.Returns(otherTenant);
		(await _service.InitializeDefaultsAsync(Input().EventKey)).ShouldBe(own.Length);
		_headerRows.Count(x => x.TenantId == _tenantId).ShouldBe(own.Length);
		_headerRows.Count(x => x.TenantId == otherTenant).ShouldBe(own.Length);
		_headerRows.Select(x => x.Id).Distinct().Count().ShouldBe(own.Length * 2);
	}

	private static string RecipientKind(string selector) => selector is "PrimaryGuardian" or "AdditionalGuardians" ? "Guardian" : selector;

	[Fact]
	public async Task List_is_event_and_medium_scoped_includes_school_defaults_and_pages_before_returning()
	{
		var medium = Guid.NewGuid();
		await _service.CreateAsync(Input());
		await _service.CreateAsync(Input() with { AcademicMediumId = medium, TemplateName = "Own medium" });
		await _service.CreateAsync(Input() with { AcademicMediumId = Guid.NewGuid(), TemplateName = "Other medium" });
		await _service.CreateAsync(Input() with { EventKey = "Academic.Assignment.Published", TemplateName = "Other event" });
		var input = new GetNotificationTemplateListInput { EventKey = Input().EventKey, AcademicMediumId = medium, MaxResultCount = 1 };
		var first = await _service.GetListAsync(input);
		first.TotalCount.ShouldBe(2); first.Items.Count.ShouldBe(1);
		input.SkipCount = 1;
		var second = await _service.GetListAsync(input);
		second.Items.Single().TemplateId.ShouldNotBe(first.Items.Single().TemplateId);
		input.AcademicMediumId = null; input.SkipCount = 0;
		(await _service.GetListAsync(input)).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Publish_records_actor_and_editing_creates_a_new_draft_without_changing_published_content()
	{
		var draft = await _service.CreateAsync(Input());
		_headerRows.Count.ShouldBe(1); _revisionRows.Count.ShouldBe(1);
		var published = await _service.PublishAsync(draft.TemplateId, Command(draft));
		published.IsPublished.ShouldBeTrue(); published.PublishedByUserId.ShouldBe(_actorId); published.PublishedAt.ShouldNotBeNull();
		var original = _revisionRows.Single(); var hash = original.ContentHash;
		var next = await _service.CreateAsync(Input() with { ExistingTemplateId = published.TemplateId,
			HeaderConcurrencyStamp = published.HeaderConcurrencyStamp, Body = "Updated {{task_title}}" });
		next.Revision.ShouldBe(2); next.IsPublished.ShouldBeFalse(); next.IsCurrent.ShouldBeTrue();
		original.ContentHash.ShouldBe(hash); original.Body.ShouldBe(Input().Body);
		(await _service.GetHistoryAsync(draft.TemplateId)).Count.ShouldBe(2);
	}

	[Fact]
	public async Task Existing_header_write_authorization_precedes_stale_stamp_and_binding_errors()
	{
		var draft = await _service.CreateAsync(Input());
		_policy.EnsureAsync(Arg.Any<string>(), Arg.Any<Guid?>(), NotificationTemplateAction.Write, Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new AbpAuthorizationException()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(Input() with {
			ExistingTemplateId = draft.TemplateId, HeaderConcurrencyStamp = "stale"
		}));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(Input() with {
			ExistingTemplateId = draft.TemplateId, HeaderConcurrencyStamp = draft.HeaderConcurrencyStamp,
			EventKey = "Academic.Assignment.Published"
		}));
		_revisionRows.Count.ShouldBe(1);
	}

	[Fact]
	public async Task A_stale_draft_update_or_publish_is_a_concurrency_error()
	{
		var draft = await _service.CreateAsync(Input());
		await Should.ThrowAsync<AbpDbConcurrencyException>(() => _service.UpdateDraftAsync(draft.TemplateId,
			new(draft.RevisionId, "Changed", "Changed {{task_title}}", "old-stamp")));
		await Should.ThrowAsync<AbpDbConcurrencyException>(() => _service.PublishAsync(draft.TemplateId, new(draft.RevisionId, "old-stamp")));
	}

	[Fact]
	public async Task Replacing_the_current_draft_prevents_publication_from_an_older_editor()
	{
		var first = await _service.CreateAsync(Input());
		await _service.CreateAsync(Input() with { ExistingTemplateId = first.TemplateId, HeaderConcurrencyStamp = first.HeaderConcurrencyStamp });
		await Should.ThrowAsync<AbpDbConcurrencyException>(() => _service.PublishAsync(first.TemplateId, Command(first)));
	}

	[Fact]
	public async Task Draft_update_preserves_revision_identity_and_validates_schema()
	{
		var draft = await _service.CreateAsync(Input());
		var changed = await _service.UpdateDraftAsync(draft.TemplateId,
			new(draft.RevisionId, "Changed", "Updated {{task_title}}", draft.ConcurrencyStamp));
		changed.RevisionId.ShouldBe(draft.RevisionId); changed.Body.ShouldBe("Updated {{task_title}}");
		await Should.ThrowAsync<BusinessException>(() => _service.UpdateDraftAsync(draft.TemplateId,
			new(draft.RevisionId, "Changed", "{{unknown_variable}}", changed.ConcurrencyStamp)));
	}

	[Fact]
	public async Task Restore_creates_an_english_default_draft_and_keeps_published_history()
	{
		var draft = await _service.CreateAsync(Input());
		var published = await _service.PublishAsync(draft.TemplateId, Command(draft));
		var restored = await _service.RestoreDefaultAsync(published.TemplateId, published.HeaderConcurrencyStamp);
		restored.Revision.ShouldBe(2); restored.IsPublished.ShouldBeFalse(); restored.Body.ShouldContain("{{source_link}}");
		_revisionRows.Single(x => x.Id == published.RevisionId).Body.ShouldBe(Input().Body);
	}

	[Fact]
	public async Task Retired_published_versions_remain_in_history_and_cannot_be_edited_or_deleted_as_drafts()
	{
		var draft = await _service.CreateAsync(Input());
		var published = await _service.PublishAsync(draft.TemplateId, Command(draft));
		await _service.RetireAsync(published.TemplateId, Command(published));
		(await _service.GetHistoryAsync(published.TemplateId)).Single().IsRetired.ShouldBeTrue();
		await Should.ThrowAsync<BusinessException>(() => _service.DeleteDraftAsync(published.TemplateId, Command(published)));
		await Should.ThrowAsync<BusinessException>(() => _service.UpdateDraftAsync(published.TemplateId,
			new(published.RevisionId, "Changed", "Changed", published.ConcurrencyStamp)));
	}

	[Fact]
	public async Task Deleted_draft_tombstones_do_not_allow_revision_number_reuse()
	{
		var first = await _service.CreateAsync(Input());
		await _service.DeleteDraftAsync(first.TemplateId, Command(first));
		var restored = await _service.RestoreDefaultAsync(first.TemplateId, first.HeaderConcurrencyStamp);
		restored.Revision.ShouldBe(2);
		_revisionRows.Single(x => x.Id == first.RevisionId).IsDeleted.ShouldBeTrue();
	}

	[Fact]
	public async Task Exact_module_policy_is_checked_and_foreign_headers_are_not_readable()
	{
		_policy.EnsureAsync(Arg.Any<string>(), Arg.Any<Guid?>(), NotificationTemplateAction.Write, Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new AbpAuthorizationException()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(Input()));
		_policy.EnsureAsync(Arg.Any<string>(), Arg.Any<Guid?>(), NotificationTemplateAction.Write, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var draft = await _service.CreateAsync(Input());
		_tenant.Id.Returns(Guid.NewGuid());
		await Should.ThrowAsync<AbpAuthorizationException>(() => _service.GetAsync(draft.TemplateId));
	}

	[Theory]
	[InlineData("{{unknown}}", "Guardian", CommunicationChannelType.Email)]
	[InlineData("Valid text", "Everyone", CommunicationChannelType.Email)]
	[InlineData("Valid text", "Guardian", CommunicationChannelType.Sms)]
	public async Task Invalid_drafts_are_rejected_before_persistence(string body, string recipientKind, CommunicationChannelType channel)
	{
		await Should.ThrowAsync<BusinessException>(() => _service.CreateAsync(Input() with { Body = body, RecipientKind = recipientKind, Channel = channel }));
		_revisionRows.ShouldBeEmpty();
	}

	private static CreateNotificationTemplateDto Input() => new("Academic.Homework.Published", "Guardian",
		CommunicationChannelType.Email, "en", "Homework update", "Homework", "Hello {{recipient_name}}, {{task_title}}");
	private static NotificationTemplateRevisionCommandDto Command(NotificationTemplateDto draft) => new(draft.RevisionId, draft.ConcurrencyStamp);
}
