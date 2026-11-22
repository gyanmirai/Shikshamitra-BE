using System;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationStorageInvariants_Tests
{
	[Fact]
	public void Scope_rejects_a_child_without_its_parent_and_empty_identifiers()
	{
		Should.Throw<ArgumentException>(() => NotificationRuleScope.Create(boardId: Guid.NewGuid()));
		Should.Throw<ArgumentException>(() => NotificationRuleScope.Create(yearId: Guid.Empty));
	}

	[Fact]
	public void Scope_key_is_nonnullable_canonical_and_does_not_match_another_branch()
	{
		var year = Guid.NewGuid();
		var board = Guid.NewGuid();
		var medium = Guid.NewGuid();
		var school = NotificationRuleScope.Create();
		var parent = NotificationRuleScope.Create(year, board);
		var child = NotificationRuleScope.Create(year, board, medium);
		var other = NotificationRuleScope.Create(year, Guid.NewGuid(), medium);
		school.Key.ShouldBe("school");
		child.Depth.ShouldBe(3);
		child.Key.ShouldContain(medium.ToString("N"));
		school.Contains(child).ShouldBeTrue();
		parent.Contains(child).ShouldBeTrue();
		parent.Contains(other).ShouldBeFalse();
		child.Contains(parent).ShouldBeFalse();
	}

	[Fact]
	public void New_rule_is_disabled_and_cannot_be_owned_by_host_or_an_empty_tenant()
	{
		var rule = new NotificationRule(Guid.NewGuid(), Guid.NewGuid(), "Academic.Homework.Published", NotificationRuleScope.Create());
		rule.IsEnabled.ShouldBeFalse();
		rule.ScopeKey.ShouldBe("school");
		rule.Revision.ShouldBe(1);
		Should.Throw<ArgumentException>(() => new NotificationRule(Guid.NewGuid(), Guid.Empty,
			"Academic.Homework.Published", NotificationRuleScope.Create()));
	}

	[Fact]
	public void Published_revision_is_immutable_and_retains_its_original_hash()
	{
		var revision = Revision();
		var hash = revision.ContentHash;
		revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		Should.Throw<BusinessException>(() => revision.UpdateDraft("Changed", "Changed body"))
			.Code.ShouldBe(NotificationErrorCodes.TemplateImmutable);
		revision.Body.ShouldBe("Hello {{recipient_name}}");
		revision.ContentHash.ShouldBe(hash);
		revision.Retire();
		revision.Body.ShouldBe("Hello {{recipient_name}}");
	}

	[Fact]
	public void Template_hash_includes_language_channel_and_recipient_context()
	{
		var original = Revision();
		Revision("gu").ContentHash.ShouldNotBe(original.ContentHash);
		Revision("en", CommunicationChannelType.WebPush).ContentHash.ShouldNotBe(original.ContentHash);
	}

	[Fact]
	public void Review_consumption_is_idempotent_for_the_same_key_but_not_another_send()
	{
		var now = DateTimeOffset.UtcNow;
		var operationId = Guid.NewGuid();
		var review = new NotificationSendReview(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"source-audience-template-digest", now.AddMinutes(10));
		review.Consume(operationId, "request-1", now).ShouldBe(operationId);
		review.Consume(Guid.NewGuid(), "request-1", now).ShouldBe(operationId);
		Should.Throw<BusinessException>(() => review.Consume(Guid.NewGuid(), "request-2", now))
			.Code.ShouldBe(NotificationErrorCodes.ReviewConsumed);
	}

	[Fact]
	public void Expired_review_cannot_create_an_operation()
	{
		var now = DateTimeOffset.UtcNow;
		var review = new NotificationSendReview(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "digest", now);
		Should.Throw<BusinessException>(() => review.Consume(Guid.NewGuid(), "request", now))
			.Code.ShouldBe(NotificationErrorCodes.ReviewExpired);
	}

	private static CommunicationTemplateRevision Revision(string language = "en",
		CommunicationChannelType channel = CommunicationChannelType.Email) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "Academic.Homework.Published",
			"PrimaryGuardian", channel, language, "Homework", "Hello {{recipient_name}}", 1);
}
