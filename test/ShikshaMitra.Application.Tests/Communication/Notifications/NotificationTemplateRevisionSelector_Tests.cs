using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateRevisionSelector_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly List<CommunicationTemplateRevision> _rows = [];
	private readonly NotificationTemplateRevisionSelector _selector;
	private const string EventKey = "Academic.Homework.Published";
	public NotificationTemplateRevisionSelector_Tests()
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(_tenantId);
		var revisions = Substitute.For<IRepository<CommunicationTemplateRevision, Guid>>(); revisions.GetQueryableAsync().Returns(_rows.AsQueryable());
		_selector = new(tenant, revisions, new AsyncQueryableExecuter([]));
	}
	[Fact]
	public async Task Requested_language_wins_and_missing_translation_uses_only_the_explicit_fallback()
	{
		var english = Add("en"); var hindi = Add("hi");
		SelectedId(await SelectAsync("hi", "en")).ShouldBe(hindi.Id);
		SelectedId(await SelectAsync("mr", "en")).ShouldBe(english.Id);
		(await SelectAsync("mr", "fr")).ShouldBeNull();
	}
	[Fact]
	public async Task Unpublished_retired_foreign_and_wrong_recipient_revisions_are_ineligible_for_new_intents()
	{
		Add("en", publish: false); Add("en", retired: true); Add("en", tenant: Guid.NewGuid()); Add("en", kind: "AssignedTeacher");
		(await SelectAsync("en", "en")).ShouldBeNull();
		var eligible = Add("en");
		SelectedId(await SelectAsync("en", "en")).ShouldBe(eligible.Id);
	}
	[Fact]
	public async Task Ambiguous_same_language_bindings_fail_instead_of_choosing_an_arbitrary_header()
	{
		Add("en"); Add("en");
		await Should.ThrowAsync<BusinessException>(() => SelectAsync("en", "en"));
	}
	[Fact]
	public async Task Only_the_rules_allowed_header_ids_can_supply_a_published_revision()
	{
		var allowed = Add("en");
		(await _selector.SelectAsync([Guid.NewGuid()], EventKey, "Guardian", CommunicationChannelType.Email, "en", "en")).ShouldBeNull();
		Add("en");
		SelectedId(await _selector.SelectAsync([allowed.TemplateId], EventKey, "Guardian", CommunicationChannelType.Email, "en", "en"))
			.ShouldBe(allowed.Id);
	}
	private static Guid SelectedId(CommunicationTemplateRevision? revision)
	{
		revision.ShouldNotBeNull();
		return revision!.Id;
	}
	private Task<CommunicationTemplateRevision?> SelectAsync(string language, string fallback) => _selector.SelectAsync(
		_rows.Select(x => x.TemplateId).ToArray(), EventKey, "Guardian", CommunicationChannelType.Email, language, fallback);
	private CommunicationTemplateRevision Add(string language, bool publish = true, bool retired = false, Guid? tenant = null, string kind = "Guardian")
	{
		var row = new CommunicationTemplateRevision(Guid.NewGuid(), tenant ?? _tenantId, Guid.NewGuid(), 1,
			EventKey, kind, CommunicationChannelType.Email, language, "Homework", "{{source_link}}", 1);
		if (publish) row.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		if (retired) row.Retire();
		_rows.Add(row); return row;
	}
}
