using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateSnapshotSelector_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly List<CommunicationTemplateRevision> _rows = [];
	private readonly NotificationTemplateSnapshotSelector _selector;
	private readonly NotificationEventDefinition _definition = new AcademicNotificationCatalogue([]).Get("Academic.Homework.Published");
	public NotificationTemplateSnapshotSelector_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		var repository = Substitute.For<IRepository<CommunicationTemplateRevision, Guid>>(); repository.GetQueryableAsync().Returns(_rows.AsQueryable());
		_selector = new(_tenant, repository, new AsyncQueryableExecuter([]), new());
	}
	[Fact]
	public async Task Pinning_captures_exact_published_variants_in_all_available_languages()
	{
		var english = Add(); var translated = Add(language: "hi"); var draft = Add(publish: false);
		var ids = await _selector.PinAsync(Config(english, translated, draft), _definition);
		ids.OrderBy(x => x).ShouldBe(new[] { english.Id, translated.Id }.OrderBy(x => x));
		english.Retire(); ids.ShouldContain(english.Id);
		var next = Add(); (await _selector.PinAsync(Config(english, translated, next), _definition)).ShouldNotContain(english.Id);
	}
	[Theory]
	[InlineData("foreign")]
	[InlineData("retired")]
	[InlineData("deleted")]
	[InlineData("draft")]
	[InlineData("schema")]
	[InlineData("event")]
	public async Task An_ineligible_revision_cannot_satisfy_fallback_coverage(string variant)
	{
		var row = Add(tenantId: variant == "foreign" ? Guid.NewGuid() : null,
			publish: variant != "draft", schema: variant == "schema" ? 2 : 1,
			eventKey: variant == "event" ? "Academic.Assignment.Published" : null);
		if (variant == "retired") row.Retire(); if (variant == "deleted") row.IsDeleted = true;
		await InvalidAsync(Config(row));
	}
	[Fact]
	public async Task Required_guardian_student_and_staff_variants_need_each_selected_channel()
	{
		var guardian = Add(); var student = Add(kind: "StudentAccount"); var staff = Add(kind: "AssignedTeacher");
		var config = Config(guardian, student, staff) with { IncludeStudentAccount = true, StaffAudienceSelectors = ["AssignedTeacher"] };
		(await _selector.PinAsync(config, _definition)).Count.ShouldBe(3);
		await InvalidAsync(config with { Channels = [CommunicationChannelType.InApp, CommunicationChannelType.Email] });
	}
	[Fact]
	public async Task A_nonfallback_translation_is_not_a_substitute_for_the_required_fallback()
	{
		await InvalidAsync(Config(Add(language: "hi")));
	}
	[Fact]
	public async Task Ambiguous_published_variants_fail_instead_of_selecting_arbitrarily()
	{
		await InvalidAsync(Config(Add(), Add()));
	}
	[Fact]
	public async Task A_host_context_cannot_pin_school_templates()
	{
		_tenant.Id.Returns((Guid?)null);
		var error = await Should.ThrowAsync<BusinessException>(() => _selector.PinAsync(Config(Add()), _definition));
		error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
	}
	private async Task InvalidAsync(NotificationRuleRuntimeConfiguration config)
	{
		var error = await Should.ThrowAsync<BusinessException>(() => _selector.PinAsync(config, _definition));
		error.Code.ShouldBe(NotificationErrorCodes.TemplateInvalid);
	}
	private static NotificationRuleRuntimeConfiguration Config(params CommunicationTemplateRevision[] rows) => new(1,
		NotificationGuardianMode.PrimaryOnly, [], false, [], [CommunicationChannelType.InApp], rows.Select(x => x.TemplateId).ToArray(),
		NotificationRuleTimingConfiguration.Default, "en");
	private CommunicationTemplateRevision Add(string kind = "Guardian", string language = "en", bool publish = true,
		Guid? tenantId = null, int schema = 1, string? eventKey = null)
	{
		var row = new CommunicationTemplateRevision(Guid.NewGuid(), tenantId ?? _tenantId, Guid.NewGuid(), 1,
			eventKey ?? _definition.EventKey, kind, CommunicationChannelType.InApp, language, "Title", "Body", schema);
		if (publish) row.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow); _rows.Add(row); return row;
	}
}
