using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Seating_Readiness_And_Workspace_Should_Share_One_Roster_Snapshot(bool workspace)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                await EnsureAdminUserExistsAsync();
                var (exam, plans, _, students) = await CreateAllocatedSeparateSessionPlansAsync();
                var expected = await _seatAllocationAppService.ValidateExaminationSeatingAsync(exam.Id);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var actual = workspace
                    ? (await _seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id, plans[0])).Readiness
                    : await _seatAllocationAppService.ValidateExaminationSeatingAsync(exam.Id);
                JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected));
                actual.TotalStudentSeats.ShouldBe(students.Count * 2);
                var rosterReads = capture.Commands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
                rosterReads.ShouldNotBeEmpty();
                rosterReads.Count.ShouldBe(1);
                capture.Commands.ShouldAllBe(sql => !sql.Contains("\"PrimaryContactNo\"", StringComparison.Ordinal));
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Seating_Publish_And_Reset_Should_Batch_Audit_Snapshots_And_Flush_Once(bool publish)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                await EnsureAdminUserExistsAsync();
                var (exam, plans, _, _) = await CreateAllocatedSeparateSessionPlansAsync();
                var allocations = await _seatAllocationRepository.GetListAsync(allocation => allocation.ExamId == exam.Id);
                var expected = plans.ToDictionary(plan => plan, plan => JsonSerializer.Serialize(allocations.Where(allocation => allocation.SeatingPlanId == plan)
                    .Select(allocation => new { allocation.StudentId, allocation.RoomId, allocation.SeatNumber, allocation.BenchId })));
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var dirtySaves = 0;
                var saveTraces = new List<string>();
                EventHandler<SavingChangesEventArgs> onSave = (_, _) =>
                {
                    var pendingEntries = db.ChangeTracker.Entries().Count(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
                    if (pendingEntries > 0) dirtySaves++;
                    saveTraces.Add($"Pending entries: {pendingEntries}\n{Environment.StackTrace}");
                };
                db.SavingChanges += onSave;
                try
                {
                    if (publish) await _seatAllocationAppService.PublishExaminationSeatingAsync(exam.Id, new PublishSeatingPlanInputDto());
                    else await _seatAllocationAppService.ResetExaminationSeatingDraftAsync(exam.Id, new ResetExaminationSeatingDraftInputDto { Reason = "Batch reset test" });
                }
                finally { db.SavingChanges -= onSave; }
                var action = publish ? SeatingPlanAuditAction.ExaminationPublished : SeatingPlanAuditAction.DraftReset;
                var audits = await _auditLogRepository.GetListAsync(log => log.ExamId == exam.Id && log.Action == action);
                audits.Count.ShouldBe(2);
                foreach (var audit in audits) audit.AssignmentsSnapshotJson.ShouldBe(expected[audit.SeatingPlanId]);
                // ABP auditing performs an empty save after the method returns.
                dirtySaves.ShouldBe(1, string.Join("\n\n", saveTraces));
            }
        });
    }
}
