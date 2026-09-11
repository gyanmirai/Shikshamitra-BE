using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class SqliteEligibilityCalendarDate_Tests
{
	[Fact]
	public async Task Eligibility_test_provider_filters_calendar_dates_in_SQL_without_changing_offsets_or_nulls()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		var options = new DbContextOptionsBuilder<CalendarContext>().UseSqlite(connection)
			.ReplaceService<IMemberTranslatorProvider, SqliteApplicationTestMemberTranslatorProvider>().Options;
		await using var db = new CalendarContext(options);
		await db.Database.EnsureCreatedAsync();
		var cutoff = new DateTime(2026, 4, 1);
		db.Rows.AddRange(
			new CalendarRow { Id = 1, Value = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), Cutoff = cutoff },
			new CalendarRow { Id = 2, Value = new(2026, 4, 1, 23, 59, 59, TimeSpan.Zero), Cutoff = cutoff },
			new CalendarRow { Id = 3, Value = new(2026, 4, 2, 0, 0, 0, TimeSpan.Zero), Cutoff = cutoff },
			new CalendarRow { Id = 4, Value = new(2026, 4, 1, 0, 15, 0, TimeSpan.FromMinutes(330)), Cutoff = cutoff },
			new CalendarRow { Id = 5, Value = null, Cutoff = cutoff });
		await db.SaveChangesAsync();

		var query = db.Rows.Where(row => row.Value.HasValue && row.Value.Value.Date == row.Cutoff.Date);
		query.ToQueryString().ShouldContain("WHERE");
		(await query.OrderBy(row => row.Id).Select(row => row.Id).ToListAsync()).ShouldBe([1, 2, 4]);
		(await db.Rows.Where(row => row.Id == 5).Select(row => (DateTime?)row.Value!.Value.Date).SingleAsync()).ShouldBeNull();
	}

	private sealed class CalendarContext(DbContextOptions<CalendarContext> options) : DbContext(options)
	{
		public DbSet<CalendarRow> Rows => Set<CalendarRow>();
	}

	private sealed class CalendarRow
	{
		public int Id { get; set; }
		public DateTimeOffset? Value { get; set; }
		public DateTime Cutoff { get; set; }
	}
}
