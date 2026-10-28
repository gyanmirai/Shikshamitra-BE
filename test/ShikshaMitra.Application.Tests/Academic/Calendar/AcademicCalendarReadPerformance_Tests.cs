using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarReadPerformance_Tests
    : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Vacation_Duplicate_Checks_Should_Batch_Dates_And_Preserve_Conflicts()
    {
        var repository = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
        var days = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
        var service = GetRequiredService<IAcademicCalendarAppService>();
        var shortCalendar = Calendar("Short");
        var longCalendar = Calendar("Long");
        await WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(shortCalendar);
            await repository.InsertAsync(longCalendar);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            int shortReads;
            using (var capture = new CalendarCommands(db.Database.GetDbConnection()))
            {
                await service.CreateCalendarDayAsync(shortCalendar.Id, Vacation(2));
                shortReads = capture.Selects("CalendarDays").Length;
            }
            using (var capture = new CalendarCommands(db.Database.GetDbConnection()))
            {
                await service.CreateCalendarDayAsync(longCalendar.Id, Vacation(10));
                capture.Selects("CalendarDays").Length.ShouldBeLessThanOrEqualTo(shortReads + 1);
            }
            await db.SaveChangesAsync();
            (await days.CountAsync(day => day.AcademicCalendarId == longCalendar.Id)).ShouldBe(10);
            var duplicate = Vacation(1);
            duplicate.Date = duplicate.Date.AddDays(3);
            duplicate.EndDate = duplicate.Date;
            var error = await Should.ThrowAsync<UserFriendlyException>(() =>
                service.CreateCalendarDayAsync(longCalendar.Id, duplicate));
            error.Message.ShouldContain("already exists for 2026-02-04");
        });
    }

    [Fact]
    public async Task Audit_Page_Should_Use_Async_Database_Reads_And_Preserve_Total()
    {
        var repository = GetRequiredService<IRepository<AcademicCalendarAuditLog, Guid>>();
        var service = GetRequiredService<IAcademicCalendarAppService>();
        var calendarId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(new AcademicCalendarAuditLog(Guid.NewGuid(), null,
                calendarId, CalendarAuditAction.DayAdded, "{}", "{}", Guid.Empty,
                module: "CalendarManagement", actorNameSnapshot: "Calendar administrator"));
        });
        await WithUnitOfWorkAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            using var capture = new CalendarCommands(db.Database.GetDbConnection());
            var page = await service.GetAuditLogsAsync(calendarId, null, 0, 1);
            page.TotalCount.ShouldBe(1);
            page.Items.Count.ShouldBe(1);
            var reads = capture.Selects("CalendarAuditLogs");
            reads.Length.ShouldBeGreaterThanOrEqualTo(2);
            reads.ShouldAllBe(command => command.IsAsync);
        });
    }

    private static AcademicCalendar Calendar(string name) => new(Guid.NewGuid(), null,
        Guid.NewGuid(), null, name, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

    private static CreateCalendarDayDto Vacation(int count) => new()
    {
        DayType = DayType.Vacation, Title = "School vacation", AppliesToAll = true,
        Date = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        EndDate = new DateTimeOffset(2026, 2, count, 0, 0, 0, TimeSpan.Zero)
    };

    private sealed class CalendarCommands : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly DbConnection _connection;
        private readonly ConcurrentBag<IDisposable> _subscriptions = [];
        private readonly ConcurrentQueue<(string Sql, bool IsAsync)> _commands = [];
        private readonly IDisposable _listener;

        public CalendarCommands(DbConnection connection)
        {
            _connection = connection;
            _listener = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public (string Sql, bool IsAsync)[] Selects(string table) => _commands.Where(command =>
            command.Sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            command.Sql.Contains(table, StringComparison.Ordinal)).ToArray();

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
                _subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is CommandExecutedEventData data && data.Context != null &&
                ReferenceEquals(data.Context.Database.GetDbConnection(), _connection))
                _commands.Enqueue((data.Command.CommandText, data.IsAsync));
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose()
        {
            _listener.Dispose();
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
    }
}
