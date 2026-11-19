using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using ShikshaMitra.HR.Calendar.Holidays;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

// Query fixtures use an in-memory SQLite connection, not a school database or a
// database service. Repository filters model the existing tenant boundary.
public class AttendanceRegisterLookupPerformance_Tests(ITestOutputHelper output)
{
	[Theory]
	[InlineData(1)]
	[InlineData(31)]
	[InlineData(366)]
	public async Task Day_locks_use_one_projected_query_and_match_per_date_reads(int days)
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		var recorder = new ReadRecorder();
		await using var db = new LookupDb(new DbContextOptionsBuilder<LookupDb>()
			.UseSqlite(connection).AddInterceptors(recorder).Options);
		await db.Database.EnsureCreatedAsync();
		var start = new DateOnly(2026, 1, 1);
		var dates = Enumerable.Range(0, days).Select(start.AddDays).ToArray();
		for (var i = 0; i < days; i++)
		{
			if (i % 3 != 2) db.Add(Lock(db.TenantId, dates[i]));
			db.Add(Lock(Guid.NewGuid(), dates[i]));
		}
		db.Add(Lock(db.TenantId, start.AddDays(-1)));
		await db.SaveChangesAsync();
		var repository = Substitute.For<IStaffAttendanceDayLockRepository>();
		repository.GetQueryableAsync().Returns(_ => Task.FromResult(db.Set<StaffAttendanceDayLock>().AsNoTracking().AsQueryable()));
		repository.FindAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(call =>
			db.Set<StaffAttendanceDayLock>().AsNoTracking().FirstOrDefaultAsync(x => x.LockDate == call.Arg<DateOnly>()));
		var reader = CreateReader(repository, Substitute.For<IRepository<Holiday, Guid>>());
		var withDuplicates = dates.Concat(dates.Take(2)).ToArray();

		// Reproduce the previous method's exact query pattern on the same fixture.
		recorder.Commands.Clear();
		var baseline = new HashSet<DateOnly>();
		foreach (var date in withDuplicates.Distinct())
			if (await repository.FindAsync(date) != null) baseline.Add(date);
		recorder.Commands.Count.ShouldBe(days);
		var pageDates = dates.Take(Math.Min(7, days)).ToArray();
		foreach (var date in pageDates)
			await repository.FindAsync(date); // Former ungrouped page lookup.
		recorder.Commands.Count.ShouldBe(days + pageDates.Length);

		recorder.Commands.Clear();
		var actual = await reader.BuildDayLockLookupAsync(withDuplicates);
		actual.SetEquals(baseline).ShouldBeTrue();
		actual.Count.ShouldBe(Enumerable.Range(0, days).Count(i => i % 3 != 2));
		recorder.Commands.Count.ShouldBe(1);
		recorder.Commands.Single().ShouldContain("LockDate");
		recorder.Commands.Single().ShouldNotContain("LockedByUserId");
		output.WriteLine($"SQLite dates={days}, grouped lock reads={days}->1, ungrouped={days + pageDates.Length}->1; locked/unlocked values and tenant parity verified.");
		recorder.Commands.Clear();
		(await reader.BuildDayLockLookupAsync([])).ShouldBeEmpty();
		recorder.Commands.ShouldBeEmpty();
	}

	[Fact]
	public async Task Holiday_overlap_is_filtered_in_SQL_with_inclusive_boundaries_and_tenant_parity()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		var recorder = new ReadRecorder();
		await using var db = new LookupDb(new DbContextOptionsBuilder<LookupDb>()
			.UseSqlite(connection).AddInterceptors(recorder).Options);
		await db.Database.EnsureCreatedAsync();
		var start = new DateOnly(2026, 6, 10);
		var end = start.AddDays(2);
		Holiday HolidayRow(Guid tenant, string name, DateOnly from, DateOnly to) =>
			Holiday.Create(tenant, Guid.NewGuid(), name, Guid.NewGuid(), from, to);
		db.AddRange(
			HolidayRow(db.TenantId, "Ends at start", start.AddDays(-2), start),
			HolidayRow(db.TenantId, "Starts at end", end, end.AddDays(2)),
			HolidayRow(db.TenantId, "Outside", end.AddDays(1), end.AddDays(5)),
			HolidayRow(Guid.NewGuid(), "Other tenant", start, end));
		await db.SaveChangesAsync();
		var repository = Substitute.For<IRepository<Holiday, Guid>>();
		repository.GetQueryableAsync().Returns(_ => Task.FromResult(db.Set<Holiday>().AsNoTracking().AsQueryable()));
		var reader = CreateReader(Substitute.For<IStaffAttendanceDayLockRepository>(), repository);
		recorder.Commands.Clear();
		var actual = await reader.BuildHolidayLookupAsync([end, start, start]);
		actual.Keys.OrderBy(x => x).ShouldBe([start, end]);
		actual[start].Name.ShouldBe("Ends at start");
		actual[end].Name.ShouldBe("Starts at end");
		recorder.Commands.Count.ShouldBe(1);
		recorder.Commands.Single().ShouldContain("\"StartDate\" <=");
		recorder.Commands.Single().ShouldContain("\"EndDate\" >=");
		recorder.Commands.Clear();
		(await reader.BuildHolidayLookupAsync([])).ShouldBeEmpty();
		recorder.Commands.ShouldBeEmpty();
	}

	[Fact]
	public async Task Production_queries_translate_to_Npgsql_without_opening_a_connection()
	{
		await using var db = new LookupDb(new DbContextOptionsBuilder<LookupDb>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only;Timeout=1").Options);
		var sql = new List<string>();
		var executer = Substitute.For<IAsyncQueryableExecuter>();
		executer.ToListAsync(Arg.Any<IQueryable<DateOnly>>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			sql.Add(call.Arg<IQueryable<DateOnly>>().ToQueryString());
			return Task.FromResult(new List<DateOnly>());
		});
		executer.ToListAsync(Arg.Any<IQueryable<Holiday>>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			sql.Add(call.Arg<IQueryable<Holiday>>().ToQueryString());
			return Task.FromResult(new List<Holiday>());
		});
		var locks = Substitute.For<IStaffAttendanceDayLockRepository>();
		locks.GetQueryableAsync().Returns(db.Set<StaffAttendanceDayLock>().AsQueryable());
		var holidays = Substitute.For<IRepository<Holiday, Guid>>();
		holidays.GetQueryableAsync().Returns(db.Set<Holiday>().AsQueryable());
		var reader = CreateReader(locks, holidays, executer);
		await reader.BuildDayLockLookupAsync([new(2026, 6, 1), new(2026, 6, 30)]);
		await reader.BuildHolidayLookupAsync([new(2026, 6, 1), new(2026, 6, 30)]);
		sql.Count.ShouldBe(2);
		sql[0].ShouldContain("ANY");
		sql[0].ShouldNotContain("LockedByUserId");
		sql.ShouldAllBe(query => query.Contains("TenantId"));
		sql[1].ShouldContain("IsDeleted");
		sql[1].ShouldContain("\"StartDate\" <=");
		sql[1].ShouldContain("\"EndDate\" >=");
		db.Database.GetDbConnection().State.ShouldBe(System.Data.ConnectionState.Closed);
	}

	private static AttendanceRegisterLookupReader CreateReader(
		IStaffAttendanceDayLockRepository locks, IRepository<Holiday, Guid> holidays,
		IAsyncQueryableExecuter? executer = null) =>
		new(null!, locks, null!, null!, holidays, null!, null!, null!,
			executer ?? new AsyncQueryableExecuter([new EfCoreAsyncQueryableProvider()]));

	private static StaffAttendanceDayLock Lock(Guid tenant, DateOnly date) =>
		new(Guid.NewGuid(), tenant, date, Guid.NewGuid(), DateTime.UtcNow);

	private sealed class LookupDb(DbContextOptions<LookupDb> options) : DbContext(options)
	{
		public Guid TenantId { get; } = Guid.NewGuid();
		protected override void OnModelCreating(ModelBuilder model)
		{
			model.Entity<StaffAttendanceDayLock>(row =>
			{
				row.HasKey(x => x.Id);
				row.Ignore(x => x.ExtraProperties);
				row.HasQueryFilter(x => x.TenantId == TenantId);
			});
			model.Entity<Holiday>(row =>
			{
				row.HasKey(x => x.Id);
				row.Ignore(x => x.ExtraProperties);
				row.Ignore(x => x.AudienceIds);
				row.HasQueryFilter(x => x.TenantId == TenantId && !x.IsDeleted);
			});
		}
	}

	private sealed class ReadRecorder : DbCommandInterceptor
	{
		public List<string> Commands { get; } = [];
		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
			CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{
			Commands.Add(command.CommandText);
			return ValueTask.FromResult(result);
		}
	}
}
