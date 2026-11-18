using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueueConcurrency_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task A_cached_transaction_snapshot_cannot_bypass_channel_capacity()
	{
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync();
		await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
		await Should.ThrowAsync<InvalidOperationException>(() => NotificationPostgresQueueClaims.ClaimAsync(connection, transaction,
			Guid.NewGuid(), NotificationQueueKind.Dispatch, DateTimeOffset.UtcNow, 1, TimeSpan.FromMinutes(5), default, maxActivePerChannel: 1));
	}

	[NotificationPostgresFact]
	public async Task Simultaneous_worker_claims_enforce_school_channel_capacity_without_blocking_other_channels()
	{
		var tenant = Guid.NewGuid();
		var otherTenant = Guid.NewGuid();
		Guid firstEmail;
		Guid secondEmail;
		Guid push;
		await using (var seed = fixture.CreateContext())
		{
			firstEmail = (await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant)).Id;
			secondEmail = (await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant)).Id;
			push = (await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant, CommunicationChannelType.WebPush)).Id;
			await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, otherTenant);
		}
		var now = DateTimeOffset.UtcNow.AddSeconds(1);
		await using var first = fixture.CreateConnection();
		await using var second = fixture.CreateConnection();
		await using var other = fixture.CreateConnection();
		await first.OpenAsync();
		await second.OpenAsync();
		await other.OpenAsync();
		await using var firstTx = await first.BeginTransactionAsync();
		await using var secondTx = await second.BeginTransactionAsync();
		await using var otherTx = await other.BeginTransactionAsync();
		var email = (await NotificationPostgresQueueClaims.ClaimAsync(first, firstTx, tenant, NotificationQueueKind.Dispatch,
			now, 1, TimeSpan.FromMinutes(5), default, maxActivePerChannel: 1)).ShouldHaveSingleItem();
		new[] { firstEmail, secondEmail }.ShouldContain(email.Id);
		var blocked = NotificationPostgresQueueClaims.ClaimAsync(second, secondTx, tenant, NotificationQueueKind.Dispatch,
			now, 1, TimeSpan.FromMinutes(5), default, maxActivePerChannel: 1);
		(await Task.WhenAny(blocked, Task.Delay(125))).ShouldNotBe(blocked);
		(await NotificationPostgresQueueClaims.ClaimAsync(other, otherTx, otherTenant, NotificationQueueKind.Dispatch,
			now, 1, TimeSpan.FromMinutes(5), default, maxActivePerChannel: 1)).ShouldHaveSingleItem().TenantId.ShouldBe(otherTenant);
		await otherTx.CommitAsync();
		await firstTx.CommitAsync();
		(await blocked.WaitAsync(TimeSpan.FromSeconds(10))).ShouldHaveSingleItem().Id.ShouldBe(push);
		(await NotificationPostgresQueueClaims.ClaimAsync(second, secondTx, tenant, NotificationQueueKind.Dispatch,
			now, 1, TimeSpan.FromMinutes(5), default, maxActivePerChannel: 1)).ShouldBeEmpty();
		await secondTx.CommitAsync();
	}

	[NotificationPostgresFact]
	public async Task Renewal_extends_only_the_current_unexpired_lease_and_fences_stale_row_updates()
	{
		var tenant = Guid.NewGuid();
		await using (var seed = fixture.CreateContext()) await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant);
		await using var connection = fixture.CreateConnection();
		var time = DateTimeOffset.UtcNow.AddSeconds(1);
		var claims = await NotificationPostgresQueueClaims.ClaimAsync(connection, null, tenant, NotificationQueueKind.Dispatch,
			time, 1, TimeSpan.FromMinutes(5), default);
		var claim = claims.ShouldHaveSingleItem();
		await using var staleContext = fixture.CreateContext();
		var stale = await staleContext.Set<NotificationDispatch>().SingleAsync(x => x.Id == claim.Id);
		var renewalTime = time.AddMinutes(2);
		(await NotificationPostgresQueueClaims.RenewDispatchAsync(connection, null, tenant, claim.Id, Guid.NewGuid(),
			renewalTime, TimeSpan.FromMinutes(5), default)).ShouldBeFalse();
		(await NotificationPostgresQueueClaims.RenewDispatchAsync(connection, null, Guid.NewGuid(), claim.Id, claim.LeaseId,
			renewalTime, TimeSpan.FromMinutes(5), default)).ShouldBeFalse();
		(await NotificationPostgresQueueClaims.RenewDispatchAsync(connection, null, tenant, claim.Id, claim.LeaseId,
			renewalTime, TimeSpan.FromMinutes(5), default)).ShouldBeTrue();
		stale.RecoverExpiredLease(time.AddMinutes(6)).ShouldBeTrue();
		await Should.ThrowAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
		await using var check = fixture.CreateContext();
		var current = await check.Set<NotificationDispatch>().SingleAsync(x => x.Id == claim.Id);
		current.LeaseExpiresAt!.Value.UtcTicks.ShouldBeInRange(renewalTime.AddMinutes(5).UtcTicks - 9, renewalTime.AddMinutes(5).UtcTicks);
		current.LeaseId.ShouldBe(claim.LeaseId);
		(await NotificationPostgresQueueClaims.RenewDispatchAsync(connection, null, tenant, claim.Id, claim.LeaseId,
			time.AddMinutes(8), TimeSpan.FromMinutes(5), default)).ShouldBeFalse();
	}

	[NotificationPostgresFact]
	public async Task Workers_skip_locked_rows_and_claim_disjoint_due_jobs_inside_their_tenant()
	{
		var tenant = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		await SeedAsync(tenant, 12);
		await SeedAsync(Guid.NewGuid(), 5);
		await using var first = fixture.CreateConnection();
		await using var second = fixture.CreateConnection();
		await first.OpenAsync();
		await second.OpenAsync();
		await using var transaction = await first.BeginTransactionAsync();
		var firstClaims = await NotificationPostgresQueueClaims.ClaimAsync(first, transaction, tenant,
			NotificationQueueKind.Intent, now.AddMinutes(1), 6, TimeSpan.FromMinutes(5), CancellationToken.None);
		var secondClaims = await NotificationPostgresQueueClaims.ClaimAsync(second, null, tenant,
			NotificationQueueKind.Intent, now.AddMinutes(1), 6, TimeSpan.FromMinutes(5), CancellationToken.None);
		firstClaims.Count.ShouldBe(6);
		secondClaims.Count.ShouldBe(6);
		firstClaims.Concat(secondClaims).Select(x => x.Id).Distinct().Count().ShouldBe(12);
		firstClaims.Concat(secondClaims).ShouldAllBe(x => x.TenantId == tenant && x.LeaseId != Guid.Empty);
		await transaction.CommitAsync();
	}

	[NotificationPostgresFact]
	public async Task Expired_inflight_intents_are_not_blindly_reclaimed_as_new_work()
	{
		var tenant = Guid.NewGuid();
		await SeedAsync(tenant, 1);
		var now = DateTimeOffset.UtcNow;
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync();
		var first = await NotificationPostgresQueueClaims.ClaimAsync(connection, null, tenant,
			NotificationQueueKind.Intent, now, 10, TimeSpan.FromMinutes(1), CancellationToken.None);
		first.Count.ShouldBe(1);
		var next = await NotificationPostgresQueueClaims.ClaimAsync(connection, null, tenant,
			NotificationQueueKind.Intent, now.AddMinutes(2), 10, TimeSpan.FromMinutes(1), CancellationToken.None);
		next.ShouldBeEmpty();
		await using var context = fixture.CreateContext();
		var stored = await context.Set<NotificationIntent>().SingleAsync(x => x.Id == first[0].Id);
		stored.Status.ShouldBe(NotificationIntentState.Processing);
		stored.LeaseId.ShouldBe(first[0].LeaseId);
	}

	private async Task SeedAsync(Guid tenant, int count)
	{
		await using var context = fixture.CreateContext();
		var rule = new NotificationRule(Guid.NewGuid(), tenant, "Academic.Homework.Published", NotificationRuleScope.Create());
		context.Add(rule);
		for (var index = 0; index < count; index++)
			context.Add(NotificationPersistence_Tests.Intent(tenant, rule.Id, $"claim-{index}"));
		await context.SaveChangesAsync();
	}

	[NotificationPostgresFact]
	public async Task Dispatch_claim_records_one_attempt_and_closes_a_connection_it_opened()
	{
		var tenant = Guid.NewGuid();
		Guid dispatchId;
		await using (var seed = fixture.CreateContext())
			dispatchId = (await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant)).Id;
		await using var connection = fixture.CreateConnection();
		var claims = await NotificationPostgresQueueClaims.ClaimAsync(connection, null, tenant,
			NotificationQueueKind.Dispatch, DateTimeOffset.UtcNow.AddSeconds(1), 10, TimeSpan.FromMinutes(5), CancellationToken.None);
		claims.Count.ShouldBe(1);
		claims[0].Id.ShouldBe(dispatchId);
		connection.State.ShouldBe(System.Data.ConnectionState.Closed);
		await using var context = fixture.CreateContext();
		var stored = await context.Set<NotificationDispatch>().SingleAsync(x => x.Id == dispatchId);
		stored.Status.ShouldBe(NotificationDispatchState.Processing);
		stored.AttemptNumber.ShouldBe(1);
		stored.LeaseId.ShouldBe(claims[0].LeaseId);
	}
}
