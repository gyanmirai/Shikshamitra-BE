using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDeliveryProjection_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 19, 19, 0, 0, TimeSpan.Zero);
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly Guid _lease = Guid.NewGuid();
	private readonly ChannelProviderSetting _provider;
	private readonly List<DeliveryAttempt> _attempts = [];
	private readonly NotificationDeliveryProjection _projection;

	public NotificationDeliveryProjection_Tests()
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(_tenantId);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns("Asia/Kolkata");
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		var clock = Substitute.For<IClock>(); clock.Now.Returns(Now.UtcDateTime);
		_provider = new(Guid.NewGuid(), _tenantId) {
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true, Status = CommunicationProviderStatus.Enabled,
			DailyQuota = 1
		};
		var attempts = Repository(_attempts);
		attempts.InsertAsync(Arg.Any<DeliveryAttempt>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var item = call.Arg<DeliveryAttempt>(); _attempts.Add(item); return item;
		});
		_projection = new(tenant, Substitute.For<INotificationPersonalWriteLock>(), new(tenant, settings),
			Repository(new[] { _provider }), attempts, new AsyncQueryableExecuter([]), guids, clock);
	}

	[Fact]
	public async Task Reservations_are_held_until_the_new_local_day_when_acceptance_is_unknown()
	{
		var first = Dispatch();
		var entry = await _projection.ReserveAsync(first, default);
		entry.AttemptId.ShouldNotBeNull();
		first.BeginIo(_lease, Now, entry.AttemptId);
		first.RecordOutcome(_lease, Now, NotificationDispatchState.UnknownAcceptance, errorCode: NotificationErrorCodes.UnknownAcceptance);
		await _projection.RecordAsync(first, Now, default);

		_attempts.Single().Status.ShouldBe(CommunicationDeliveryStatus.UnknownAcceptance);
		var next = await _projection.ReserveAsync(Dispatch(), default);
		next.AttemptId.ShouldBeNull();
		next.RetryAt.ShouldBe(new DateTimeOffset(2026, 11, 20, 18, 30, 0, TimeSpan.Zero));
		_attempts.Count.ShouldBe(1);
	}

	[Fact]
	public async Task A_definite_rejection_frees_capacity_and_email_acceptance_leaves_delivery_unconfirmed()
	{
		var first = Dispatch();
		var rejected = await _projection.ReserveAsync(first, default);
		first.BeginIo(_lease, Now, rejected.AttemptId);
		first.RecordOutcome(_lease, Now, NotificationDispatchState.Failed, errorCode: "SmtpPermanentRejection");
		await _projection.RecordAsync(first, Now, default);

		var second = Dispatch();
		var accepted = await _projection.ReserveAsync(second, default);
		accepted.AttemptId.ShouldNotBeNull();
		second.BeginIo(_lease, Now, accepted.AttemptId);
		second.RecordOutcome(_lease, Now, NotificationDispatchState.ProviderAccepted, "smtp:test");
		await _projection.RecordAsync(second, Now, default);

		_attempts.Count.ShouldBe(2);
		var row = _attempts.Single(x => x.Id == accepted.AttemptId);
		row.Status.ShouldBe(CommunicationDeliveryStatus.Sent);
		row.DeliveredAt.ShouldBeNull();
		row.ReadAt.ShouldBeNull();
		row.ProviderMessageId.ShouldBe("smtp:test");
		row.NotificationDispatchId.ShouldBe(second.Id);
		row.NotificationProviderSettingId.ShouldBe(_provider.Id);
	}

	[Fact]
	public async Task A_disabled_provider_cannot_reserve_or_record_a_send_attempt()
	{
		_provider.IsEnabled = false;
		var rejected = await _projection.ReserveAsync(Dispatch(), default);
		rejected.ExclusionCode.ShouldBe(NotificationErrorCodes.ProviderUnavailable);
		rejected.AttemptId.ShouldBeNull();
		_attempts.ShouldBeEmpty();
	}

	[Fact]
	public async Task The_monthly_limit_includes_reservations_from_earlier_school_days()
	{
		_provider.MonthlyQuota = 1;
		_attempts.Add(new DeliveryAttempt(Guid.NewGuid(), _tenantId) {
			NotificationDispatchId = Guid.NewGuid(), NotificationProviderSettingId = _provider.Id,
			QueuedAt = Now.AddDays(-10), Status = CommunicationDeliveryStatus.Sent
		});

		var result = await _projection.ReserveAsync(Dispatch(), default);

		result.AttemptId.ShouldBeNull();
		result.RetryAt.ShouldBe(new DateTimeOffset(2026, 11, 30, 18, 30, 0, TimeSpan.Zero));
	}

	private NotificationDispatch Dispatch()
	{
		var dispatch = new NotificationDispatch(Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(), "{}", "context",
			CommunicationChannelType.Email, "fingerprint", "protected", "hash", Guid.NewGuid().ToString("N"), Now, Now.AddDays(2));
		dispatch.BindPlanningEvidence(Guid.NewGuid(), Guid.NewGuid(), _provider.Id);
		Set(dispatch, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(dispatch, nameof(NotificationDispatch.LeaseId), _lease);
		Set(dispatch, nameof(NotificationDispatch.LeaseExpiresAt), Now.AddMinutes(5));
		Set(dispatch, nameof(NotificationDispatch.AttemptNumber), 1);
		return dispatch;
	}

	private static void Set<T>(T row, string property, object value) => typeof(T).GetProperty(property)!.SetValue(row, value);

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}
}
