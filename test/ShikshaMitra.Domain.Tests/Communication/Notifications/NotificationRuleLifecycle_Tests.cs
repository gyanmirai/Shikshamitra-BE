using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationRuleLifecycle_Tests
{
	private const string Configuration = "{\"schemaVersion\":1,\"channels\":[3]}";
	[Fact]
	public void Configuration_changes_revision_and_fence_but_cannot_change_scope_or_event()
	{
		var row = Rule(); var stamp = row.ConcurrencyStamp; var scope = row.ScopeKey;
		row.Configure(true, Configuration, stamp);
		row.IsEnabled.ShouldBeTrue(); row.ConfigurationJson.ShouldBe(Configuration);
		row.Revision.ShouldBe(2); row.ConcurrencyStamp.ShouldNotBe(stamp);
		row.ScopeKey.ShouldBe(scope); row.EventKey.ShouldBe("Academic.Homework.Published");
	}
	[Fact]
	public void A_repeated_identical_save_is_not_a_new_rule_revision()
	{
		var row = Rule(); row.Configure(true, Configuration, row.ConcurrencyStamp); var stamp = row.ConcurrencyStamp;
		row.Configure(true, Configuration, stamp); row.Revision.ShouldBe(2); row.ConcurrencyStamp.ShouldBe(stamp);
		row.Configure(false, Configuration, stamp); row.Revision.ShouldBe(3); row.IsEnabled.ShouldBeFalse();
	}
	[Theory]
	[InlineData("{}")]
	[InlineData("[]")]
	[InlineData("not json")]
	[InlineData("{\"schemaVersion\":2}")]
	public void An_unvalidated_legacy_placeholder_cannot_be_enabled(string json)
	{
		var row = Rule(); Should.Throw<BusinessException>(() => row.Configure(true, json, row.ConcurrencyStamp));
		row.IsEnabled.ShouldBeFalse(); row.Revision.ShouldBe(1); row.ConfigurationJson.ShouldBe("{}");
	}
	[Fact]
	public void A_stale_or_missing_fence_cannot_overwrite_current_configuration()
	{
		var row = Rule(); Should.Throw<BusinessException>(() => row.Configure(true, Configuration, ""));
		Should.Throw<BusinessException>(() => row.Configure(true, Configuration, Guid.NewGuid().ToString("N")));
		row.Revision.ShouldBe(1); row.IsEnabled.ShouldBeFalse();
	}
	private static NotificationRule Rule() => new(Guid.NewGuid(), Guid.NewGuid(), "Academic.Homework.Published", NotificationRuleScope.Create());
}
