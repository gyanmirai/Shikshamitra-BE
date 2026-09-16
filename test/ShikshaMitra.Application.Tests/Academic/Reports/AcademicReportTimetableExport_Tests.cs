using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timetable_Exports_Should_Preserve_All_Six_Reports_Across_Keyset_Batches(bool filtered)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            var currentTenant = GetRequiredService<ICurrentTenant>();
            using (currentTenant.Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var subject = await GetRequiredService<IRepository<Subject, Guid>>().GetAsync(assignment.SubjectId);
                // Legacy subject filters compare display names, including duplicate-name subjects.
                var duplicate = new Subject(Guid.NewGuid(), tenantId, subject.Name, "#00A76F", subject.EducationMediumId);
                await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(duplicate, autoSave: true);
                var teacher = await SeedTeacherAsync(tenantId, " Ada ", " Lovelace ");
                var secondTeacher = await SeedTeacherAsync(tenantId, "Grace", "Hopper", "BBCDE1234F", "223456789012");
                var room = await SeedTimetableExportRoomAsync(tenantId, "Z room");
                await SeedTimetableExportRoomAsync(tenantId, "A unused room");
                var divisionIds = new List<Guid>();
                for (var tableIndex = 0; tableIndex < 2; tableIndex++)
                {
                    var division = await GetRequiredService<DivisionManager>().CreateAsync(assignment.StandardId, $"Division {tableIndex}", 30);
                    await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);
                    divisionIds.Add(division.Id);
                    var timetable = new TimetableEntity(Guid.Parse($"81000000-0000-0000-0000-{tableIndex + 1:000000000000}"),
                        tenantId, assignment.StandardId, division.Id);
                    for (var index = 0; index < 13; index++)
                    {
                        var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, (TimetableDayOfWeek)(5 - index % 6),
                            index + 1, new TimeOnly(8, 0), new TimeOnly(8, 45), index == 12 ? SlotType.Break : SlotType.Period);
                        if (index % 3 != 0)
                        {
                            slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, subject.Id, teacher.Id, room.Id));
                            slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, duplicate.Id,
                                index % 2 == 0 ? secondTeacher.Id : null, room.Id));
                        }
                        timetable.AddSlot(slot);
                    }
                    await GetRequiredService<IRepository<TimetableEntity, Guid>>().InsertAsync(timetable, autoSave: true);
                }
                using (currentTenant.Change(Guid.NewGuid()))
                {
                    var foreign = new TimetableEntity(Guid.NewGuid(), currentTenant.Id, assignment.StandardId, Guid.NewGuid());
                    var foreignSlot = new TimetableSlot(Guid.NewGuid(), foreign.Id, TimetableDayOfWeek.Monday, 99,
                        new TimeOnly(9, 0), new TimeOnly(10, 0), SlotType.Period);
                    foreignSlot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), foreignSlot.Id, subject.Id, teacher.Id, room.Id));
                    foreign.AddSlot(foreignSlot);
                    await GetRequiredService<IRepository<TimetableEntity, Guid>>().InsertAsync(foreign, autoSave: true);
                }
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                // Set deterministic audit values directly so ABP modification stamping does not replace them.
                await db.Database.ExecuteSqlRawAsync("UPDATE \"academic_Timetables\" SET \"LastModificationTime\" = {0}, \"LastModifierId\" = {1} WHERE \"TenantId\" = {2}",
                    new DateTime(2026, 9, 15, 10, 30, 0), teacher.Id, tenantId);
                var generator = GetRequiredService<AcademicReportGenerator>();
                foreach (var reportType in new[]
                {
                    AcademicReportTypes.ClasswiseTimetable, AcademicReportTypes.TeacherwiseTimetable,
                    AcademicReportTypes.RoomUtilizationReport, AcademicReportTypes.SubjectwiseTimetableDistribution,
                    AcademicReportTypes.FreePeriodsReport, AcademicReportTypes.TimetableChangeLog
                })
                {
                    var request = new AcademicReportRequestDto
                    {
                        ReportType = reportType,
                        Filters = filtered ? new AcademicReportFilterDto
                        {
                            ClassIds = [assignment.StandardId], DivisionIds = [divisionIds[0]],
                            TeacherIds = [teacher.Id], SubjectIds = [subject.Id], RoomIds = [room.Id]
                        } : new AcademicReportFilterDto()
                    };
                    db.ChangeTracker.Clear();
                    var expected = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: true);
                    expected.TotalCount.ShouldBeGreaterThan(0, reportType);
                    db.ChangeTracker.Clear();
                    var export = await generator.GenerateExportAsync(request, Guid.NewGuid(), tenantId, batchSize: 2);
                    var actual = new List<Dictionary<string, string?>>();
                    await foreach (var row in export.Rows) actual.Add(row);
                    export.Metadata.Columns.Select(column => (column.Id, column.Label))
                        .ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)), reportType);
                    actual.Count.ShouldBe(expected.TotalCount, reportType);
                    for (var index = 0; index < expected.Rows.Count; index++)
                    {
                        actual[index].Count.ShouldBe(expected.Rows[index].Count, reportType);
                        foreach (var cell in expected.Rows[index]) actual[index][cell.Key].ShouldBe(cell.Value, $"{reportType}: {index}, {cell.Key}");
                    }
                    db.ChangeTracker.Entries<TimetableEntity>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<TimetableSlot>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<TimetableSlotSubject>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<Room>().ShouldBeEmpty();
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    var cancelledExport = await generator.GenerateExportAsync(request, Guid.NewGuid(), tenantId, cancellation.Token, batchSize: 2);
                    await Should.ThrowAsync<OperationCanceledException>(async () =>
                    {
                        await foreach (var _ in cancelledExport.Rows) { }
                    });
                }
            }
        });
    }

    private async Task<Room> SeedTimetableExportRoomAsync(Guid tenantId, string name)
    {
        var building = new Building { TenantId = tenantId, BuildingName = name, TotalFloors = 1 };
        SetTimetableExportEntityId(building);
        await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building, autoSave: true);
        var floor = new Floor { BuildingId = building.Id, FloorName = "First", FloorNo = 1 };
        SetTimetableExportEntityId(floor);
        await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(floor, autoSave: true);
        var type = new RoomType { TenantId = tenantId, RoomTypeName = name, ShortName = "EXP" };
        SetTimetableExportEntityId(type);
        await GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(type, autoSave: true);
        var room = new Room { TenantId = tenantId, RoomName = name, BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = type.Id, Capacity = 30 };
        SetTimetableExportEntityId(room);
        return await GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room, autoSave: true);
    }

    private static void SetTimetableExportEntityId<TEntity>(TEntity entity) where TEntity : Entity<Guid> =>
        typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, Guid.NewGuid());
}
