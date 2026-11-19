using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
    [Theory]
    [InlineData(AcademicReportTypes.ClasswiseTimetable)]
    [InlineData(AcademicReportTypes.TeacherwiseTimetable)]
    [InlineData(AcademicReportTypes.RoomUtilizationReport)]
    [InlineData(AcademicReportTypes.SubjectwiseTimetableDistribution)]
    [InlineData(AcademicReportTypes.FreePeriodsReport)]
    [InlineData(AcademicReportTypes.TimetableChangeLog)]
    public async Task Timetable_Preview_Should_Bound_Detail_Reads_And_Preserve_Pages(string reportType)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var teacher = await SeedTeacherAsync(tenantId, "Ada", "Lovelace");
                var room = await SeedTimetableExportRoomAsync(tenantId, "Preview room");
                var division = await GetRequiredService<DivisionManager>().CreateAsync(assignment.StandardId, "Preview", 30);
                await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);
                var timetable = new TimetableEntity(Guid.NewGuid(), tenantId, assignment.StandardId, division.Id);
                foreach (var number in Enumerable.Range(1, 321))
                {
                    var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, (TimetableDayOfWeek)(number % 6), number,
                        new TimeOnly(8, 0), new TimeOnly(8, 45), SlotType.Period);
                    if (number % 3 != 0 || number == 6)
                        slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, assignment.SubjectId, teacher.Id, room.Id));
                    timetable.AddSlot(slot);
                }
                await GetRequiredService<IRepository<TimetableEntity, Guid>>().InsertAsync(timetable, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                await db.Database.ExecuteSqlRawAsync("UPDATE \"academic_Timetables\" SET \"LastModificationTime\" = {0}, \"LastModifierId\" = {1} WHERE \"TenantId\" = {2}",
                    new DateTime(2026, 9, 15, 10, 30, 0), teacher.Id, tenantId);
                var generator = GetRequiredService<AcademicReportGenerator>();
                var request = new AcademicReportRequestDto
                {
                    ReportType = reportType, PageSize = 2,
                    Filters = new() { ClassIds = [assignment.StandardId], DivisionIds = [division.Id] }
                };
                db.ChangeTracker.Clear();
                var expected = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: true);
                if (reportType == AcademicReportTypes.ClasswiseTimetable)
                    expected.Rows.Take(2).Select(row => row["Status"]).ShouldBe(new[] { "Scheduled", "Free" });
                foreach (var page in new[] { 1, 2, 999 })
                {
                    db.ChangeTracker.Clear();
                    request.Page = page;
                    using var capture = new ReportQueryCapture(db.Database.GetDbConnection());
                    var result = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: false);
                    result.TotalCount.ShouldBe(expected.TotalCount);
                    result.Columns.Select(column => (column.Id, column.Label)).ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)));
                    var expectedRows = expected.Rows.Skip((page - 1) * request.PageSize).Take(request.PageSize).ToList();
                    result.Rows.Count.ShouldBe(expectedRows.Count);
                    for (var index = 0; index < expectedRows.Count; index++)
                        foreach (var cell in expectedRows[index]) result.Rows[index][cell.Key].ShouldBe(cell.Value);
                    db.ChangeTracker.Entries<TimetableEntity>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<TimetableSlot>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<TimetableSlotSubject>().ShouldBeEmpty();
                    if (result.Rows.Count > 0)
                    {
                        capture.Reads.ShouldNotBeEmpty();
                        if (reportType is AcademicReportTypes.ClasswiseTimetable or AcademicReportTypes.TeacherwiseTimetable or AcademicReportTypes.FreePeriodsReport)
                            capture.Reads.ShouldContain(read => read.Sql.Contains("\"RoomId\"", StringComparison.Ordinal) && read.Sql.Contains("\"StartTime\"", StringComparison.Ordinal));
                    }
                    capture.Reads.Where(read => read.Sql.Contains("\"RoomId\"", StringComparison.Ordinal) &&
                        read.Sql.Contains("\"StartTime\"", StringComparison.Ordinal)).ShouldAllBe(read => read.Rows <= request.PageSize + 1);
                }
            }
        });
    }
}
