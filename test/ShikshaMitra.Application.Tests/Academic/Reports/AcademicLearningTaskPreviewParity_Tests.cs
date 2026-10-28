using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicLearningTaskExportParity_Tests
{
    [Theory]
    [MemberData(nameof(ReportCases))]
    public async Task Preview_Should_Preserve_Learning_Task_Values_And_Pages_Without_Hydrating_History(string kind, bool filtered)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var studentIds = await SeedLearningTasksAsync();
                var request = new AcademicReportRequestDto
                {
                    ReportType = kind, PageSize = 2,
                    Filters = new()
                    {
                        ClassIds = filtered ? [StandardId] : [], DivisionIds = filtered ? [DivisionId] : [],
                        SubjectIds = filtered ? [SubjectId] : [], StudentIds = filtered ? studentIds.Take(2).ToList() : [],
                        TeacherIds = filtered ? [TeacherEmployeeId] : [],
                        FromDate = filtered ? DateTime.UtcNow.Date.AddDays(-20) : null,
                        ToDate = filtered ? DateTime.UtcNow.Date : null
                    }
                };
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var generator = GetRequiredService<AcademicReportGenerator>();
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: true);
                foreach (var page in new[] { 1, 2, 999 })
                {
                    request.Page = page;
                    db.ChangeTracker.Clear();
                    var actual = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: false);
                    actual.TotalCount.ShouldBe(expected.TotalCount, kind);
                    actual.Page.ShouldBe(expected.Page == 0 ? 0 : page, kind);
                    actual.PageSize.ShouldBe(expected.PageSize, kind);
                    actual.Columns.Select(column => (column.Id, column.Label))
                        .ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)), kind);
                    var expectedRows = expected.Rows.Skip((page - 1) * request.PageSize).Take(request.PageSize).ToList();
                    actual.Rows.Count.ShouldBe(expectedRows.Count, kind);
                    for (var index = 0; index < expectedRows.Count; index++)
                        actual.Rows[index].OrderBy(cell => cell.Key).ShouldBe(expectedRows[index].OrderBy(cell => cell.Key), $"{kind}, page {page}, row {index}");
                    db.ChangeTracker.Entries<HomeworkAssignmentEntity>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<AssignmentEntity>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
                }
            }
        });
    }

    [Fact]
    public async Task Homework_Schedule_Oracle_Should_Use_Fresh_Request_Encounter_Order()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                await SeedLearningTasksAsync();
                var repository = GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
                var sample = await repository.GetAsync(Guid.Parse("10000000-0000-0000-0000-000000000001"));
                var firstDate = DateTime.UtcNow.Date.AddDays(20);
                var secondDate = firstDate.AddDays(1);
                var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), sample.StandardId, sample.SubjectId,
                    "Schedule encounter proof", "Proof", sample.HomeworkTypeId!.Value, SubmissionType.OnlineText, TenantId);
                homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                    homework.Id, DivisionId, null, firstDate, TenantId));
                homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    homework.Id, Guid.NewGuid(), null, secondDate, TenantId));
                await repository.InsertAsync(homework, autoSave: true);
                var generator = GetRequiredService<AcademicReportGenerator>();
                var request = new AcademicReportRequestDto { ReportType = AcademicReportTypes.DailyHomeworkReport, PageSize = 100 };
                var warm = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: true);
                warm.Rows.Where(row => row["HomeworkTitle"] == homework.Title).Select(row => row["DueDate"])
                    .ShouldBe(new[] { firstDate.ToString("yyyy-MM-dd"), secondDate.ToString("yyyy-MM-dd") });
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var cold = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: true);
                var coldRows = cold.Rows.Where(row => row["HomeworkTitle"] == homework.Title).ToList();
                coldRows.Select(row => row["DueDate"])
                    .ShouldBe(new[] { secondDate.ToString("yyyy-MM-dd"), firstDate.ToString("yyyy-MM-dd") });
                db.ChangeTracker.Clear();
                var preview = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: false);
                var previewRows = preview.Rows.Where(row => row["HomeworkTitle"] == homework.Title).ToList();
                for (var index = 0; index < coldRows.Count; index++)
                    previewRows[index].OrderBy(cell => cell.Key).ShouldBe(coldRows[index].OrderBy(cell => cell.Key));
            }
        });
    }
}
