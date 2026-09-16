using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicLearningTaskExportParity_Tests
{
    [Fact]
    public async Task Student_Trend_Should_Preserve_Duplicate_Input_Order_Subject_Encounter_Order_And_Offset_Periods()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var students = await SeedLearningTasksAsync();
                var today = DateTime.UtcNow.Date;
                var subject = new Subject(Guid.NewGuid(), TenantId, "Another subject", "#112233", AcademicMediumId);
                await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
                var homeworkType = (await GetRequiredService<IRepository<HomeworkType, Guid>>().GetListAsync()).Single();
                var assignmentType = (await GetRequiredService<IRepository<AssignmentType, Guid>>().GetListAsync()).Single();
                var homework = new HomeworkAssignmentEntity(Guid.Parse("01000000-0000-0000-0000-000000000001"), StandardId,
                    subject.Id, "Other subject homework", "Description", homeworkType.Id, SubmissionType.OnlineText, TenantId, YearId,
                    educationMediumId: AcademicMediumId) { CreationTime = today.AddDays(-15) };
                homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, DivisionId, null, today, TenantId));
                await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
                var assignment = new AssignmentEntity(Guid.Parse("02000000-0000-0000-0000-000000000001"), StandardId,
                    subject.Id, "Other subject assignment", "Description", assignmentType.Id, SubmissionType.OnlineText,
                    50, TenantId, YearId, educationMediumId: AcademicMediumId) { CreationTime = today.AddDays(-15) };
                assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, DivisionId, null, today, TenantId));
                await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(assignment, autoSave: true);
                foreach (var student in students.Take(3))
                {
                    await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(new StudentSubmissionEntity(
                        Guid.NewGuid(), homework.Id, student, DivisionId, today, TenantId) { Status = SubmissionStatus.Submitted }, autoSave: true);
                    await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(new StudentSubmissionEntity(
                        Guid.NewGuid(), assignment.Id, student, DivisionId, today, TenantId, true)
                    {
                        Status = SubmissionStatus.Reviewed, Marks = 40, TotalMarks = null,
                        SubmissionDate = new DateTimeOffset(DateTime.SpecifyKind(today.AddDays(-9).AddHours(1), DateTimeKind.Unspecified), TimeSpan.FromHours(5.5))
                    }, autoSave: true);
                }
                var request = new AcademicReportRequestDto
                {
                    ReportType = AcademicReportTypes.StudentPerformanceTrendReport,
                    Filters = new() { StudentIds = [students[2], students[0], students[2]], FromDate = today.AddDays(-20), ToDate = today }
                };
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var generator = GetRequiredService<AcademicReportGenerator>();
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, true);
                db.ChangeTracker.Clear();
                var export = await generator.GenerateExportAsync(request, ActorId, TenantId, batchSize: 1);
                var actual = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows) actual.Add(row);
                actual.Count.ShouldBe(6);
                actual.Count.ShouldBe(expected.Rows.Count);
                for (var index = 0; index < actual.Count; index++)
                    actual[index].OrderBy(pair => pair.Key).ShouldBe(expected.Rows[index].OrderBy(pair => pair.Key), $"Trend row {index}");
                actual.Where(row => row["Subject"] == "Another subject").ShouldAllBe(row => row["AssignmentAverage"] == "80%" && row["CurrentPeriodScore"] == "80%");
                db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
            }
        });
    }
}
