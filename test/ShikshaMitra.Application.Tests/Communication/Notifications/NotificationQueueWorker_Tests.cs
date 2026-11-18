using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.HostTenants;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueueWorker_Tests
{
	private sealed class TestTenant(Guid id, string name) : Tenant(id, name, name.ToUpperInvariant());

	[Fact]
	public async Task Active_school_pages_wrap_after_every_school_has_had_a_turn()
	{
		var repo = Substitute.For<IHostTenantQueryRepository>();
		var tenants = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		var rows = tenants.Select((id, i) => (Tenant)new TestTenant(id, "school-" + i)).ToArray();
		repo.GetPageAsync(null, true, "name", Arg.Any<int>(), Arg.Any<int>()).Returns(call =>
			Task.FromResult((Count: (long)rows.Length, Items: rows.Skip(call.ArgAt<int>(3)).Take(call.ArgAt<int>(4)).ToList())));
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns((IUnitOfWork?)null);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => Substitute.For<IUnitOfWork>());
		var host = Substitute.For<ICurrentTenant>();
		host.Id.Returns((Guid?)null);
		var directory = new NotificationWorkerTenants(host, units, repo);

		var first = await directory.ReadPageAsync(0, 2, default);
		var second = await directory.ReadPageAsync(first.NextOffset, 2, default);
		var third = await directory.ReadPageAsync(second.NextOffset, 2, default);

		first.TenantIds.Concat(second.TenantIds).Concat(third.TenantIds).ShouldBe(tenants);
		third.NextOffset.ShouldBe(0);
		(await directory.ReadPageAsync(10, 2, default)).NextOffset.ShouldBe(0);
		host.Id.Returns(tenants[0]);
		(await Should.ThrowAsync<BusinessException>(() => directory.ReadPageAsync(0, 2, default)))
			.Code.ShouldBe(NotificationErrorCodes.StateConflict);
	}

	[Fact]
	public async Task Worker_commits_each_claim_before_dispatch_and_caps_each_school_turn()
	{
		var tenantId = Guid.NewGuid();
		var now = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(now);
		var units = Substitute.For<IUnitOfWorkManager>();
		var queue = Substitute.For<INotificationQueueRepository>();
		var dispatcher = Substitute.For<INotificationDispatcher>();
		var trace = new List<string>();
		IUnitOfWork? active = null;
		units.Current.Returns(_ => active);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => {
			active.ShouldBeNull();
			var unit = Substitute.For<IUnitOfWork>();
			active = unit;
			unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(_ => { trace.Add("commit"); return Task.CompletedTask; });
			unit.When(x => x.Dispose()).Do(_ => { active = null; trace.Add("dispose"); });
			return unit;
		});
		queue.ClaimIntentsAsync(Arg.Any<DateTimeOffset>(), 1, TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<NotificationQueueClaim>>([]));
		queue.ClaimDispatchesAsync(Arg.Any<DateTimeOffset>(), 1, TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>(), 1)
			.Returns(_ => {
				active.ShouldNotBeNull();
				trace.Add("claim");
				return Task.FromResult<IReadOnlyList<NotificationQueueClaim>>([new(Guid.NewGuid(), tenantId, Guid.NewGuid())]);
			});
		dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => {
			active.ShouldBeNull();
			trace.TakeLast(3).ShouldBe(new[] { "claim", "commit", "dispose" });
			trace.Add("dispatch");
			return Task.CompletedTask;
		});
		var queries = new AsyncQueryableExecuter([]);
		var intents = Empty<NotificationIntent>();
		var dispatches = Empty<NotificationDispatch>();
		var maintenance = new NotificationLeaseRecoveryService(tenant, units, intents, dispatches, queries);
		var outcome = new NotificationOutcomeProjector(tenant, units, new(Substitute.For<INotificationPersonalWriteLock>()),
			intents, dispatches, Empty<CommunicationCampaign>(), queries);
		using var services = new ServiceCollection().BuildServiceProvider();
		var heartbeat = new NotificationDispatchHeartbeat(services.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<NotificationDispatchHeartbeat>.Instance);
		var runner = new NotificationTenantQueueRunner(tenant, units, clock, queue, null!, dispatcher, heartbeat, maintenance,
			outcome, NullLogger<NotificationTenantQueueRunner>.Instance);

		await runner.RunAsync(3, default);

		await dispatcher.Received(3).DispatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
		await queue.Received(3).ClaimDispatchesAsync(new DateTimeOffset(now), 1, TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>(), 1);
	}

	private static IRepository<T, Guid> Empty<T>() where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(Array.Empty<T>().AsQueryable());
		return repository;
	}
}
