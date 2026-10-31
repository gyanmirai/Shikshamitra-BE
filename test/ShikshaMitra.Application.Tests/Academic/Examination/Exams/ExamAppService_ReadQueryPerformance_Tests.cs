using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
    [Fact]
    public async Task Marks_Summary_Should_Batch_Required_Subject_Eligibility_And_Count_Roster_Without_Graphs()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var (exam, _) = await CreateBulkMarksFixtureAsync();
                var standard = await _standardRepository.GetAsync(exam.StandardId);
                var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
                for (var index = 0; index < 8; index++)
                {
                    var subject = await CreateSubjectAsync(stage.EducationMediumId, $"Summary subject {index} {Guid.NewGuid():N}"[..45]);
                    exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.Id, DateTime.Today, null, null, 100, 33));
                }
                await _examRepository.UpdateAsync(exam, autoSave: true);
                var subjectId = exam.ExamSubjects.First().SubjectId;
                var divisionId = exam.ExamDivisions.Single().DivisionId;
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                var assignmentTable = db.Model.FindEntityType(typeof(AssignedSubject))!.GetTableName();
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var summary = await _examAppService.GetMarksSummaryAsync(new GetExamMarksSummaryInput
                { ExamId = exam.Id, SubjectId = subjectId, DivisionId = divisionId });
                summary.TotalStudents.ShouldBe(2);
                summary.MarksEntered.ShouldBe(0);
                summary.Pending.ShouldBe(2);
                summary.PendingPublishedCount.ShouldBe(10);
                db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
                capture.Commands.Count(sql => sql.Contains($"\"{assignmentTable}\"", StringComparison.Ordinal)).ShouldBeLessThanOrEqualTo(2);
                capture.Commands.ShouldAllBe(sql => !sql.Contains("\"PrimaryContactNo\"", StringComparison.Ordinal));
            }
        });
    }

    [Fact]
    public async Task Grade_Audit_Should_Project_Only_Latest_Mark_And_Total_Count()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var (exam, students) = await CreateBulkMarksFixtureAsync(21);
                var subjectId = exam.ExamSubjects.First().SubjectId;
                var divisionId = exam.ExamDivisions.Single().DivisionId;
                var marks = students.Select(student => new ExamMark(Guid.NewGuid(), exam.Id, student.Id, subjectId,
                    divisionId, 50, false, new string('f', 900), TestTenantId)).ToList();
                await GetRequiredService<IRepository<ExamMark, Guid>>().InsertManyAsync(marks, autoSave: true);
                var actor = await _identityUserRepository.GetAsync(FakeAdminUserId);
                var expectedName = actor.Name ?? actor.UserName;
                var latest = DateTime.UtcNow.AddDays(1);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                var markTable = db.Model.FindEntityType(typeof(ExamMark))!.GetTableName();
                var quotedTable = db.GetService<ISqlGenerationHelper>().DelimitIdentifier(markTable.ShouldNotBeNull());
                var updateSql = "UPDATE " + quotedTable + " SET \"LastModificationTime\" = {0}, \"LastModifierId\" = {1} WHERE \"Id\" = {2}";
                await db.Database.ExecuteSqlRawAsync(updateSql,
                    latest, FakeAdminUserId, marks[^1].Id);
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var result = await _examAppService.GetAuditLogsAsync(exam.Id, subjectId, divisionId);
                var audit = result.Single();
                audit.Action.ShouldBe("Marks Modified");
                audit.Timestamp.ShouldBe(latest);
                audit.ChangedBy.ShouldBe(expectedName);
                audit.Details.ShouldBe("Latest changes applied to student records. Active count of records: 21.");
                db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
                db.ChangeTracker.Entries<IdentityUser>().ShouldBeEmpty();
                capture.Reads.ShouldNotBeEmpty();
                capture.Commands.ShouldAllBe(sql => !sql.Contains("\"Feedback\"", StringComparison.Ordinal));
            }
        });
    }
}
