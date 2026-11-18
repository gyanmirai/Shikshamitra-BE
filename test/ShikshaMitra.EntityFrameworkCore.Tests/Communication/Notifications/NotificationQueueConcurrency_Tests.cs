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
