using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class InAppNotificationChannelSender_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _userId = Guid.NewGuid();
	private readonly Guid _lease = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly List<NotificationInboxItem> _rows = [];
	private readonly List<string> _writes = [];
	private readonly NotificationDispatch _dispatch;
	private readonly NotificationIntent _intent;
	private readonly IdentityUser _user;
	private readonly NotificationChannelSendRequest _request;
	private readonly InAppNotificationChannelSender _sender;

	public InAppNotificationChannelSender_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_user = new IdentityUser(_userId, "test-account", "test@example.invalid", _tenantId);
		_user.SetIsActive(true);
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns((IUnitOfWork?)null);
		var unit = Substitute.For<IUnitOfWork>();
		unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(_ => { _writes.Add("commit"); return Task.CompletedTask; });
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(unit);
		var clock = Substitute.For<IClock>(); clock.Now.Returns(Now.UtcDateTime);
		var recipient = new NotificationRecipientRefDto("Guardian", Guid.NewGuid(), "mother", _userId);
		var campaignId = Guid.NewGuid();
		_intent = new(Guid.NewGuid(), _tenantId, Guid.NewGuid(), 1, "Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(),
			"v1", "operation", Now.AddHours(-1), Now.AddMinutes(-1), "[]", "[]", Now.AddHours(1));
		_intent.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.Automatic);
		_intent.BindAutomaticOperation(campaignId);
		Set(_intent, nameof(NotificationIntent.Status), NotificationIntentState.Processing);
		Set(_intent, nameof(NotificationIntent.LeaseId), _lease);
		Set(_intent, nameof(NotificationIntent.LeaseExpiresAt), Now.AddMinutes(5));
		_intent.CompletePlanning(_lease, Now.AddMinutes(-1));
		_dispatch = new(Guid.NewGuid(), _tenantId, _intent.Id, Guid.NewGuid(),
			NotificationRuntimeJson.Serialize(recipient, NotificationErrorCodes.RuntimeSnapshotInvalid), "student-context",
			CommunicationChannelType.InApp, "fingerprint", "protected-payload", "hash", "dispatch-key", Now, Now.AddHours(1));
		_dispatch.BindPlanningEvidence(campaignId, Guid.NewGuid(), null);
		Set(_dispatch, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(_dispatch, nameof(NotificationDispatch.LeaseId), _lease);
		Set(_dispatch, nameof(NotificationDispatch.LeaseExpiresAt), Now.AddMinutes(5));
		Set(_dispatch, nameof(NotificationDispatch.AttemptNumber), 1);
		_dispatch.BeginIo(_lease, Now);
		_request = new(_dispatch.Id, _dispatch.DeduplicationKey, Guid.Empty, CommunicationChannelType.InApp, _userId.ToString("N"),
			"Homework published", "Please review the task.", "https://school.example/portal/homework?view=family#details", "en", 1,
			TemplateRevisionId: _dispatch.TemplateRevisionId, Recipient: recipient);

		var inbox = Repository(_rows);
		inbox.InsertAsync(Arg.Any<NotificationInboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var item = call.Arg<NotificationInboxItem>(); _rows.Add(item); _writes.Add("inbox"); return item;
		});
		var dispatches = Repository(new[] { _dispatch });
		dispatches.UpdateAsync(Arg.Any<NotificationDispatch>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			_writes.Add("dispatch"); return call.Arg<NotificationDispatch>();
		});
		var app = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example" }).Build();
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		_sender = new(_tenant, units, clock, new(Substitute.For<INotificationPersonalWriteLock>()), dispatches, Repository(new[] { _intent }),
			inbox, Repository(new[] { _user }), new AsyncQueryableExecuter([]), guids, new([], app));
	}

	[Fact]
	public async Task Persists_the_exact_child_context_and_delivery_in_one_transaction_and_replays_the_same_receipt()
	{
		var receipt = await _sender.SendAsync(_request, default);
		receipt.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		_writes.ShouldBe(new[] { "inbox", "dispatch", "commit" });
		_dispatch.Status.ShouldBe(NotificationDispatchState.Delivered);
		_dispatch.ProviderMessageId.ShouldBe(receipt.ProviderMessageId);
		var inbox = _rows.Single();
		inbox.TenantId.ShouldBe(_tenantId);
		inbox.UserId.ShouldBe(_userId);
		inbox.DispatchId.ShouldBe(_dispatch.Id);
		inbox.StudentId.ShouldBe(_request.Recipient!.StudentId);
		inbox.SourceId.ShouldBe(_intent.SourceId);
		inbox.ContextKey.ShouldBe(_dispatch.RecipientContextKey);
		inbox.RelativeLink.ShouldBe("/portal/homework?view=family#details");

		var repeat = await _sender.SendAsync(_request, default);
		repeat.ShouldBe(receipt);
		_rows.Count.ShouldBe(1);
		_writes.Count(x => x == "dispatch").ShouldBe(1);
	}

	[Theory]
	[InlineData("inactive")]
	[InlineData("tenant")]
	[InlineData("child")]
	[InlineData("attempt")]
	[InlineData("unsafe-link")]
	public async Task An_ineligible_account_or_mismatched_delivery_cannot_create_an_inbox_item(string barrier)
	{
		var request = _request;
		if (barrier == "inactive") _user.SetIsActive(false);
		if (barrier == "tenant") _tenant.Id.Returns(Guid.NewGuid());
		if (barrier == "child") request = request with { Recipient = request.Recipient! with { StudentId = Guid.NewGuid() } };
		if (barrier == "attempt") request = request with { AttemptNumber = 2 };
		if (barrier == "unsafe-link") request = request with { LinkUrl = "https://other-school.example/portal/homework" };

		(await _sender.SendAsync(request, default)).Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);

		_rows.ShouldBeEmpty();
		_writes.ShouldBeEmpty();
		_dispatch.Status.ShouldBe(NotificationDispatchState.Processing);
	}

	[Fact]
	public async Task A_missing_template_title_uses_the_readable_inbox_title()
	{
		var result = await _sender.SendAsync(_request with { Subject = string.Empty }, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		_rows.Single().Title.ShouldBe("Academic update");
	}

	private static void Set<T>(T row, string property, object value) => typeof(T).GetProperty(property)!.SetValue(row, value);

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}
}
