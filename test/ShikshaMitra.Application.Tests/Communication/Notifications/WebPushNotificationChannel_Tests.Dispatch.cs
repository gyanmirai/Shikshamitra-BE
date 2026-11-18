using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public partial class WebPushNotificationChannel_Tests
{
	[Fact]
	public async Task Sends_only_to_the_fenced_device_and_caps_time_to_live_at_source_expiry()
	{
		var (sender, _, request) = Dispatch();

		var result = await sender.SendAsync(request with { ExpiresAt = Now.AddSeconds(75) }, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		_handler.Requests.ShouldBe(1);
		_handler.Ttl.ShouldBe("75");
		_handler.Topic.ShouldBe(request.DispatchId.ToString("N"));
	}

	[Fact]
	public async Task Reassociated_subscriptions_and_forged_dispatch_destinations_never_reach_the_provider()
	{
		var (sender, row, request) = Dispatch();
		(await sender.SendAsync(request with { Destination = Guid.NewGuid().ToString("N") }, default)).ErrorCode
			.ShouldBe(NotificationErrorCodes.RecipientIneligible);
		row.Rebind(Guid.NewGuid(), row.BrowserInstallationId, row.ProtectedSubscription, row.VapidKeyVersion, Now.AddSeconds(1));

		(await sender.SendAsync(request, default)).ErrorCode.ShouldBe("WebPushSubscriptionUnavailable");
		_handler.Requests.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task An_expired_receipt_disables_only_the_registration_used_for_the_send(bool refreshed)
	{
		var (sender, row, request) = Dispatch();
		_handler.Status = HttpStatusCode.Gone;
		_handler.BeforeSend += () => {
			if (refreshed) row.ConcurrencyStamp = "refreshed-registration";
		};

		(await sender.SendAsync(request, default)).ErrorCode.ShouldBe("WebPushSubscriptionExpired");
		row.IsEnabled.ShouldBe(refreshed);
	}

	private (WebPushNotificationChannelSender Sender, WebPushSubscription Subscription, NotificationChannelSendRequest Request) Dispatch()
	{
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(_subscription.TenantId);
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(Now.UtcDateTime);
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test",
			["Notifications:WebPush:KeyVersion"] = _subscription.VapidKeyVersion,
			["Notifications:WebPush:PublicKey"] = PublicKey(_senderKeys),
			["Notifications:WebPush:PrivateKey"] = Encode(_senderKeys.D!)
		}).Build();
		var options = new WebPushKeyConfiguration(config);
		var protection = new EphemeralDataProtectionProvider();
		var payloads = new NotificationDispatchPayloadProtector(protection, tenant);
		var subscriptions = new WebPushSubscriptionProtector(protection);
		var user = new IdentityUser(_subscription.UserId, "guardian", "guardian@example.test", _subscription.TenantId);
		user.SetIsActive(true);
		var endpoint = new RegisterWebPushSubscriptionDto(_subscription.BrowserInstallationId, _subscription.Endpoint, _subscription.Keys,
			_subscription.VapidKeyVersion);
		var registration = new WebPushSubscription(Guid.NewGuid(), _subscription.TenantId, user.Id, _subscription.BrowserInstallationId,
			WebPushSubscriptionProtector.HashEndpoint(endpoint.Endpoint), subscriptions.Protect(new(_subscription.TenantId, user.Id), endpoint),
			endpoint.VapidKeyVersion, Now);
		var recipient = new NotificationRecipientRefDto("Guardian", Guid.NewGuid(), "mother", user.Id);
		var source = new NotificationRecipientRefDto("Student", recipient.StudentId);
		var key = new NotificationDispatchPayloadKey(_subscription.TenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			CommunicationChannelType.WebPush, "hash");
		var destination = registration.Id.ToString("N");
		var message = payloads.Protect(key, new(source, recipient, "association", destination, "Homework", "Update for Child",
			"https://school.example.test/homework", "en", null));
		var row = new NotificationDispatch(key.DispatchId, key.TenantId, key.IntentId, key.TemplateRevisionId, "{}", "context", key.Channel,
			"fingerprint", message, key.ContentHash, "dedup-key", Now, Now.AddMinutes(5));
		row.BindPlanningEvidence(Guid.NewGuid(), Guid.NewGuid(), null);
		var lease = Guid.NewGuid();
		Set(row, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(row, nameof(NotificationDispatch.LeaseId), lease);
		Set(row, nameof(NotificationDispatch.LeaseExpiresAt), Now.AddMinutes(5));
		Set(row, nameof(NotificationDispatch.AttemptNumber), 1);
		row.BeginIo(lease, Now, Guid.NewGuid());
		var units = Substitute.For<IUnitOfWorkManager>();
		IUnitOfWork? active = null;
		units.Current.Returns(_ => active);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => {
			var unit = Substitute.For<IUnitOfWork>();
			active = unit;
			unit.When(x => x.Dispose()).Do(_ => active = null);
			return unit;
		});
		_handler.BeforeSend = () => active.ShouldBeNull();
		var queries = new AsyncQueryableExecuter([]);
		var sender = new WebPushNotificationChannelSender(tenant, units, clock, new(tenant, Repository(registration), queries, options),
			options, _client, payloads, subscriptions, Repository(row), Repository(user), queries, Substitute.For<INotificationPersonalWriteLock>());
		return (sender, registration, new(row.Id, row.DeduplicationKey, Guid.Empty, row.Channel, destination, "Homework",
			"Update for Child", "https://school.example.test/homework", "en", 1, TemplateRevisionId: row.TemplateRevisionId, Recipient: recipient));
	}

	private static IRepository<T, Guid> Repository<T>(T row) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(new[] { row }.AsQueryable());
		return repository;
	}

	private static void Set<T>(T row, string property, object value) => typeof(T).GetProperty(property)!.SetValue(row, value);
}
