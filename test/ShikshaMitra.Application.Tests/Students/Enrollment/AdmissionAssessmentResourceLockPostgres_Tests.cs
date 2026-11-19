using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Linq;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentResourceLockPostgres_Tests
{
	[AssessmentRosterPostgresFact]
	public async Task Interviewer_day_reservation_guard_runs_on_Postgres_and_preserves_live_status_tenant_and_window_rules()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES"));
		connection.Database += "_rooms";
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^admissions_assessment_tests_[a-z0-9_]{1,32}$")) throw new InvalidOperationException("Only a new disposable loopback database is allowed.");
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
		await admin.OpenAsync();
		await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin))
		{
			exists.Parameters.AddWithValue("name", connection.Database); Convert.ToInt64(await exists.ExecuteScalarAsync()).ShouldBe(0);
		}
		await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
		try
		{
			await using var db = new ReservationQueryDb(new DbContextOptionsBuilder<ReservationQueryDb>().UseNpgsql(connection.ConnectionString).Options);
			await db.Database.EnsureCreatedAsync();
			var teacher = Guid.NewGuid(); var date = new DateTime(2035, 1, 2, 0, 0, 0, DateTimeKind.Utc);
			RoomReservation Reservation(Guid tenant, Guid employee, DateTime day) => new(Guid.NewGuid(), tenant, Guid.NewGuid(), day, TimeSpan.FromHours(9), TimeSpan.FromHours(10), "Synthetic interview occupancy", RoomReservationType.Meeting, employee);
			var matching = Reservation(ReservationQueryDb.Tenant, teacher, date);
			db.AddRange(matching, Reservation(Guid.NewGuid(), teacher, date), Reservation(ReservationQueryDb.Tenant, Guid.NewGuid(), date), Reservation(ReservationQueryDb.Tenant, teacher, date.AddDays(1)));
			await db.SaveChangesAsync();
			// Prove the originally reported TimeSpan predicate itself translates on the production provider.
			(await db.Set<RoomReservation>().AnyAsync(row => row.TeacherId == teacher && row.Date == date && row.StartTime < TimeSpan.FromHours(10) && row.EndTime > TimeSpan.FromHours(9))).ShouldBeTrue();
			var repository = Substitute.For<IRepository<RoomReservation, Guid>>(); repository.GetQueryableAsync().Returns(_ => Task.FromResult(db.Set<RoomReservation>().AsNoTracking().AsQueryable()));
			var scheduling = new AdmissionAssessmentSchedulingService(null!, repository, null!, null!, null!, null!, null!, null!);
			using var provider = new ServiceCollection().AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter([new EfCoreAsyncQueryableProvider()])).BuildServiceProvider();
			scheduling.LazyServiceProvider = new AbpLazyServiceProvider(provider);
			await Should.ThrowAsync<BusinessException>(() => scheduling.EnsureInterviewerReservationAvailableAsync(teacher, date, TimeSpan.FromHours(9.5), TimeSpan.FromHours(9.75)));
			await scheduling.EnsureInterviewerReservationAvailableAsync(teacher, date, TimeSpan.FromHours(9.5), TimeSpan.FromHours(9.75), matching.Id);
			await scheduling.EnsureInterviewerReservationAvailableAsync(teacher, date, TimeSpan.FromHours(10), TimeSpan.FromHours(11));
			matching.Approve(Guid.NewGuid()); await db.SaveChangesAsync();
			await Should.ThrowAsync<BusinessException>(() => scheduling.EnsureInterviewerReservationAvailableAsync(teacher, date, TimeSpan.FromHours(9), TimeSpan.FromHours(10)));
			matching.Cancel(Guid.NewGuid(), "Synthetic test cancellation"); await db.SaveChangesAsync();
			await scheduling.EnsureInterviewerReservationAvailableAsync(teacher, date, TimeSpan.FromHours(9), TimeSpan.FromHours(10));
		}
		finally { NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
	}

	private sealed class ReservationQueryDb(DbContextOptions<ReservationQueryDb> options) : DbContext(options)
	{
		public static readonly Guid Tenant = Guid.Parse("c1111111-1111-1111-1111-111111111111");
		protected override void OnModelCreating(ModelBuilder builder)
		{
			var reservation = builder.Entity<RoomReservation>(); reservation.ToTable("academic_RoomReservations"); reservation.HasKey(row => row.Id);
			reservation.Ignore(row => row.ExtraProperties); reservation.Property(row => row.StartTime).HasColumnType("time without time zone"); reservation.Property(row => row.EndTime).HasColumnType("time without time zone");
			reservation.HasQueryFilter(row => !row.IsDeleted && row.TenantId == Tenant);
		}
	}

	[AssessmentRosterPostgresFact]
	public async Task Resource_locks_require_a_transaction_serialize_same_tenant_and_release_at_commit()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES"));
		connection.Database += "_locks";
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^admissions_assessment_tests_[a-z0-9_]{1,32}$"))
			throw new InvalidOperationException("Only a new disposable loopback Assessment test database is allowed.");
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
		await admin.OpenAsync();
		await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin))
		{
			exists.Parameters.AddWithValue("name", connection.Database);
			Convert.ToInt64(await exists.ExecuteScalarAsync()).ShouldBe(0, "Never reuse or remove an existing database.");
		}
		await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
		try
		{
			// Scheduling locks execute raw provider SQL and need no school/identity tables or seeding.
			var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString)
				.UseModel(new ModelBuilder().FinalizeModel()).Options;
			await using var first = new ShikshaMitraDbContext(options);
			await using var second = new ShikshaMitraDbContext(options);
			await using var otherTenant = new ShikshaMitraDbContext(options);
			static EfCoreAdmissionAssessmentResourceLock Lock(ShikshaMitraDbContext db)
			{
				var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
				provider.GetDbContextAsync().Returns(db);
				return new(provider);
			}
			await Should.ThrowAsync<InvalidOperationException>(() => Lock(first).AcquireAsync(Guid.NewGuid(), [Guid.NewGuid()]));
			var tenant = Guid.NewGuid(); var room = Guid.NewGuid();
			await using var firstTransaction = await first.Database.BeginTransactionAsync();
			await using var secondTransaction = await second.Database.BeginTransactionAsync();
			await using var isolatedTransaction = await otherTenant.Database.BeginTransactionAsync();
			await Lock(first).AcquireAsync(tenant, [room]);
			await Lock(otherTenant).AcquireAsync(Guid.NewGuid(), [room]).WaitAsync(TimeSpan.FromSeconds(5));
			var waiting = Lock(second).AcquireAsync(tenant, [room]);
			(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
			await firstTransaction.CommitAsync();
			await waiting.WaitAsync(TimeSpan.FromSeconds(5));
			await secondTransaction.CommitAsync(); await isolatedTransaction.CommitAsync();
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
			await drop.ExecuteNonQueryAsync();
		}
	}
}
