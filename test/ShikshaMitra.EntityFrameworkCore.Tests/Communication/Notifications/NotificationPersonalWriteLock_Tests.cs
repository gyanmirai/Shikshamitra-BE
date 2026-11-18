using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationPersonalWriteLock_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task A_personal_write_lock_requires_an_explicit_transaction()
	{
		await using var connection = fixture.CreateConnection(); await connection.OpenAsync();
		await Should.ThrowAsync<InvalidOperationException>(() => NotificationPostgresPersonalLocks.AcquireAsync(connection, null,
			Guid.NewGuid(), ["preference:test"], default));
	}

	[NotificationPostgresFact]
	public async Task Same_tenant_and_key_serialize_until_the_first_transaction_finishes()
	{
		var tenant = Guid.NewGuid();
		await using var first = fixture.CreateConnection(); await first.OpenAsync();
		await using var firstTransaction = await first.BeginTransactionAsync();
		await NotificationPostgresPersonalLocks.AcquireAsync(first, firstTransaction, tenant, ["browser:shared", "endpoint:shared"], default);
		await using var second = fixture.CreateConnection(); await second.OpenAsync();
		await using var secondTransaction = await second.BeginTransactionAsync();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var waiting = NotificationPostgresPersonalLocks.AcquireAsync(second, secondTransaction, tenant, ["endpoint:shared", "browser:shared"], timeout.Token);
		await Task.Delay(100); waiting.IsCompleted.ShouldBeFalse();
		await firstTransaction.CommitAsync(); await waiting; await secondTransaction.CommitAsync();
	}

	[NotificationPostgresFact]
	public async Task Identical_keys_in_different_tenants_do_not_block_each_other()
	{
		await using var first = fixture.CreateConnection(); await first.OpenAsync();
		await using var firstTransaction = await first.BeginTransactionAsync();
		await NotificationPostgresPersonalLocks.AcquireAsync(first, firstTransaction, Guid.NewGuid(), ["browser:shared"], default);
		await using var second = fixture.CreateConnection(); await second.OpenAsync();
		await using var secondTransaction = await second.BeginTransactionAsync();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await NotificationPostgresPersonalLocks.AcquireAsync(second, secondTransaction, Guid.NewGuid(), ["browser:shared"], timeout.Token);
		await secondTransaction.CommitAsync(); await firstTransaction.CommitAsync();
	}

	[NotificationPostgresFact]
	public async Task Concurrent_first_creations_cannot_bypass_the_unique_preference_context()
	{
		var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		async Task<Guid> CreateOrReadAsync()
		{
			await using var db = fixture.CreateContext(); await using var transaction = await db.Database.BeginTransactionAsync();
			await start.Task;
			await NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(), tenant,
				[$"preference:{user:N}:user:Academic.Homework"], default);
			var existing = await db.Set<NotificationPreference>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.UserId == user);
			if (existing != null) { await transaction.CommitAsync(); return existing.Id; }
			await Task.Delay(200);
			var row = new NotificationPreference(Guid.NewGuid(), tenant, user, "Academic.Homework"); db.Add(row);
			await db.SaveChangesAsync(); await transaction.CommitAsync(); return row.Id;
		}
		var first = CreateOrReadAsync(); var second = CreateOrReadAsync(); start.SetResult();
		var ids = await Task.WhenAll(first, second); ids.Distinct().Count().ShouldBe(1);
	}
}
