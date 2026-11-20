using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Library;

public class LibraryInventoryLockPostgres_Tests
{
	[LibraryPostgresFact]
	public async Task Same_School_Transactions_Should_Wait_While_Different_Schools_Remain_Independent()
	{
		await using var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
		await using var first = fixture.CreateContext();
		await using var second = fixture.CreateContext();
		await using var otherSchool = fixture.CreateContext();
		await using var observer = fixture.CreateContext();
		await observer.Database.OpenConnectionAsync();
		await using var firstTransaction = await first.Database.BeginTransactionAsync();
		await using var secondTransaction = await second.Database.BeginTransactionAsync();
		await using var otherTransaction = await otherSchool.Database.BeginTransactionAsync();
		var school = Guid.NewGuid();
		await CreateLock(first).AcquireAsync(school);

		var waiting = CreateLock(second).AcquireAsync(school);
		var secondConnection = (NpgsqlConnection)second.Database.GetDbConnection();
		try
		{
			var blocked = false;
			for (var attempt = 0; attempt < 50 && !blocked && !waiting.IsCompleted; attempt++)
			{
				await using var command = new NpgsqlCommand("""
					SELECT EXISTS (SELECT 1 FROM pg_stat_activity
					WHERE pid = @pid AND wait_event_type = 'Lock' AND wait_event = 'advisory')
					""", (NpgsqlConnection)observer.Database.GetDbConnection());
				command.Parameters.AddWithValue("pid", secondConnection.ProcessID);
				blocked = (bool)(await command.ExecuteScalarAsync())!;
				if (!blocked) await Task.Delay(50);
			}
			blocked.ShouldBeTrue("The database must show an advisory lock wait, rather than merely an unscheduled task.");
			waiting.IsCompleted.ShouldBeFalse();
			await CreateLock(otherSchool).AcquireAsync(Guid.NewGuid()).WaitAsync(TimeSpan.FromSeconds(5));
		}
		finally
		{
			await firstTransaction.RollbackAsync();
			await waiting.WaitAsync(TimeSpan.FromSeconds(5));
		}
		await secondTransaction.CommitAsync();
		await otherTransaction.CommitAsync();
	}

	[LibraryPostgresFact]
	public async Task Inventory_Mutation_Without_A_Transaction_Should_Be_Rejected_On_Postgres()
	{
		await using var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
		await using var db = fixture.CreateContext();

		var error = await Should.ThrowAsync<InvalidOperationException>(() => CreateLock(db).AcquireAsync(Guid.NewGuid()));

		error.Message.ShouldContain("transactional unit of work");
	}

	private static EfCoreLibraryInventoryLock CreateLock(ShikshaMitraDbContext db)
	{
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		provider.GetDbContextAsync().Returns(Task.FromResult(db));
		return new EfCoreLibraryInventoryLock(provider);
	}
}
