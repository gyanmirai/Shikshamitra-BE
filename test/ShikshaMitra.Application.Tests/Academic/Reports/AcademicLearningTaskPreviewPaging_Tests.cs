using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
    [Theory]
    [InlineData(AcademicReportTypes.PendingHomeworkReport)]
    [InlineData(AcademicReportTypes.HomeworkSubmissionHistoryReport)]
    [InlineData(AcademicReportTypes.HomeworkWithAttachmentsReport)]
    [InlineData(AcademicReportTypes.AssignmentFeedbackReport)]
    public async Task Learning_Preview_Should_Read_Only_Page_Content_And_Never_Submission_Bodies(string reportType)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var allocation = await SeedAssignmentAsync(tenantId);
                var subject = await GetRequiredService<IRepository<Subject, Guid>>().GetAsync(allocation.SubjectId);
                var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(allocation.StandardId);
                var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(subject.EducationMediumId);
                var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
                var homeworkType = await GetRequiredService<HomeworkTypeManager>().CreateAsync(subject.EducationMediumId, "Preview practice");
                var assignmentType = await GetRequiredService<AssignmentTypeManager>().CreateAsync(subject.EducationMediumId, "Preview assignment",
                    defaultMarks: 100, defaultEvaluationMethod: EvaluationMethod.Marks);
                var divisionId = Guid.NewGuid();
                await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new Division(divisionId, tenantId,
                    "Preview division", standard.Id, 30, string.Empty), autoSave: true);
                var student = await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(new Student(Guid.NewGuid(), tenantId, "Preview", null, "Student",
                    board.AcademicYearId, medium.EducationBoardId, medium.Id, standard.EducationMediumStageId, standard.Id, divisionId), autoSave: true);
                for (var index = 0; index < 40; index++)
                {
                    var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), standard.Id, subject.Id, $"Homework {index}", new string('d', 12000),
                        homeworkType.Id, SubmissionType.OnlineText, tenantId, attachmentsJson: "[{\"name\":\"preview.pdf\"}]");
                    homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, divisionId, null, DateTime.UtcNow.Date.AddDays(index - 40), tenantId));
                    await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
                    var assignment = new AssignmentEntity(Guid.NewGuid(), standard.Id, subject.Id, $"Assignment {index}", new string('d', 12000),
                        assignmentType.Id, SubmissionType.OnlineText, 100, tenantId);
                    assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, divisionId, null, DateTime.UtcNow.Date, tenantId));
                    await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(assignment, autoSave: true);
                    foreach (var isAssignment in new[] { false, true })
                    {
                        var due = DateTime.UtcNow.Date.AddDays(index - 40);
                        var submission = isAssignment
                            ? new StudentSubmissionEntity(Guid.NewGuid(), assignment.Id, student.Id, divisionId, due, tenantId, true)
                            : new StudentSubmissionEntity(Guid.NewGuid(), homework.Id, student.Id, divisionId, due, tenantId);
                        submission.SubmittedContent = new string('b', 12000);
                        submission.Feedback = new string('f', 900);
                        submission.Status = SubmissionStatus.NotSubmitted;
                        await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(submission, autoSave: true);
                    }
                }
                var request = new AcademicReportRequestDto { ReportType = reportType, Page = 2, PageSize = 3 };
                var generator = GetRequiredService<AcademicReportGenerator>();
                var expected = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new ReportQueryCapture(db.Database.GetDbConnection());
                var result = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: false);
                result.TotalCount.ShouldBe(expected.TotalCount);
                result.Rows.Count.ShouldBe(3);
                for (var index = 0; index < result.Rows.Count; index++)
                    result.Rows[index].OrderBy(cell => cell.Key).ShouldBe(expected.Rows[index + 3].OrderBy(cell => cell.Key));
                capture.Reads.ShouldNotBeEmpty();
                capture.Commands.ShouldAllBe(sql => !sql.Contains("\"SubmittedContent\"", StringComparison.Ordinal),
                    "Standard previews do not display submitted bodies and must never read them.");
                var contentReads = capture.Reads.Where(read => LearningPreviewSelectList(read.Sql).Contains("\"Feedback\"", StringComparison.Ordinal) ||
                        LearningPreviewSelectList(read.Sql).Contains("\"AttachmentsJson\"", StringComparison.Ordinal)).ToList();
                if (reportType is AcademicReportTypes.AssignmentFeedbackReport or AcademicReportTypes.HomeworkWithAttachmentsReport)
                    contentReads.ShouldNotBeEmpty("The page must actually load its displayed feedback or attachment JSON.");
                contentReads.ShouldAllBe(read => read.Rows <= request.PageSize + 1, "Only the requested page may read feedback or attachment JSON.");
                db.ChangeTracker.Entries<HomeworkAssignmentEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignmentEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
            }
        });
    }

    private static string LearningPreviewSelectList(string sql)
    {
        var from = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        return from < 0 ? sql : sql[..from];
    }
}
