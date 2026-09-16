using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicLearningTaskExportParity_Tests : SubjectTeacherReassignmentTestBase
{
    [Fact]
    public async Task Every_Registered_Static_Report_Should_Have_A_Bounded_Export_Reader()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(Guid.NewGuid()))
            using (AsAdmin())
            {
                var generator = GetRequiredService<AcademicReportGenerator>();
                foreach (var field in typeof(AcademicReportTypes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    var kind = (string)field.GetValue(null)!;
                    var expected = await generator.GenerateAsync(new() { ReportType = kind }, ActorId, Tenant.Id, true);
                    var export = await generator.GenerateExportAsync(new() { ReportType = kind }, ActorId, Tenant.Id, batchSize: 2);
                    export.Metadata.Columns.Select(column => (column.Id, column.Label))
                        .ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)), kind);
                    var actual = new List<Dictionary<string, string?>>();
                    await foreach (var row in export.Rows) actual.Add(row);
                    actual.Count.ShouldBe(expected.Rows.Count, kind);
                    for (var index = 0; index < actual.Count; index++)
                        actual[index].OrderBy(pair => pair.Key).ShouldBe(expected.Rows[index].OrderBy(pair => pair.Key), $"{kind}, row {index}");
                }
            }
        });
    }

    [Fact]
    public async Task Export_Should_Preserve_Unknown_Report_Error_For_Different_Casing()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(Guid.NewGuid()))
            {
                var kind = AcademicReportTypes.ChapterProgressReport.ToLowerInvariant();
                var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<AcademicReportGenerator>()
                    .GenerateExportAsync(new() { ReportType = kind }, ActorId, Tenant.Id));
                error.Code.ShouldBe("Academic:Reports:UnknownReportType");
                error.Data["ReportType"].ShouldBe(kind);
            }
        });
    }

    public static IEnumerable<object[]> ReportCases()
    {
        string[] kinds = [AcademicReportTypes.DailyHomeworkReport, AcademicReportTypes.ClasswiseHomeworkReport,
            AcademicReportTypes.SubjectwiseHomeworkReport, AcademicReportTypes.TeacherwiseHomeworkReport,
            AcademicReportTypes.StudentwiseHomeworkSummaryReport, AcademicReportTypes.HomeworkCompletionStatusReport,
            AcademicReportTypes.AssignmentIssuanceReport, AcademicReportTypes.AssignmentEvaluationReport,
            AcademicReportTypes.AssignmentGradeDistributionReport, AcademicReportTypes.AssignmentPerformanceReport,
            AcademicReportTypes.HomeworkLoadAnalysisReport, AcademicReportTypes.AssignmentLoadAnalysisReport,
            AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport, AcademicReportTypes.StudentPerformanceTrendReport,
            AcademicReportTypes.ClasswiseOverallHomeworkAssignmentReport,
            AcademicReportTypes.PendingHomeworkReport, AcademicReportTypes.OverdueHomeworkReport,
            AcademicReportTypes.HomeworkSubmissionHistoryReport, AcademicReportTypes.HomeworkAcknowledgementReport,
            AcademicReportTypes.HomeworkWithAttachmentsReport, AcademicReportTypes.AssignmentSubmissionReport,
            AcademicReportTypes.AssignmentMarksReport, AcademicReportTypes.LateSubmissionReport,
            AcademicReportTypes.MissingAssignmentReport, AcademicReportTypes.AssignmentFeedbackReport];
        foreach (var kind in kinds)
        {
            yield return [kind, false];
            yield return [kind, true];
        }
    }

    [Theory]
    [MemberData(nameof(ReportCases))]
    public async Task Export_Should_Preserve_Learning_Task_Columns_Values_And_Order_Across_Chunks(string kind, bool filtered)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var studentIds = await SeedLearningTasksAsync();
                var request = new AcademicReportRequestDto
                {
                    ReportType = kind, Page = 3, PageSize = 1,
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
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, true);
                db.ChangeTracker.Clear();
                var export = await generator.GenerateExportAsync(request, ActorId, TenantId, batchSize: 2);
                var actual = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows) actual.Add(row);
                export.Metadata.Columns.Select(c => (c.Id, c.Label)).ShouldBe(expected.Columns.Select(c => (c.Id, c.Label)), kind);
                actual.Count.ShouldBe(expected.Rows.Count, kind);
                for (var index = 0; index < actual.Count; index++)
                    actual[index].OrderBy(p => p.Key).ShouldBe(expected.Rows[index].OrderBy(p => p.Key), $"{kind}, row {index}");
                db.ChangeTracker.Entries<HomeworkAssignmentEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignmentEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
                db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
            }
        });
    }

    [Fact]
    public async Task Assignment_Performance_Should_Preserve_Root_Encounter_Order_For_Equal_Display_Names()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                await SeedLearningTasksAsync();
                var firstSubject = new Subject(Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1"), TenantId, "Tie subject", "#112233", AcademicMediumId);
                var secondSubject = new Subject(Guid.Parse("00000000-0000-0000-0000-000000000010"), TenantId, "Tie subject", "#112233", AcademicMediumId);
                await GetRequiredService<IRepository<Subject, Guid>>().InsertManyAsync([firstSubject, secondSubject], autoSave: true);
                var assignmentType = (await GetRequiredService<IRepository<AssignmentType, Guid>>().GetListAsync()).Single();
                for (var index = 0; index < 3; index++)
                {
                    var assignment = new AssignmentEntity(Guid.Parse($"03000000-0000-0000-0000-{index + 1:000000000000}"), StandardId,
                        index == 0 ? firstSubject.Id : secondSubject.Id, $"Tied assignment {index}", "Description", assignmentType.Id,
                        SubmissionType.OnlineText, 100, TenantId, YearId, educationMediumId: AcademicMediumId);
                    assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, DivisionId, null, DateTime.UtcNow.Date, TenantId));
                    await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(assignment, autoSave: true);
                }

                var request = new AcademicReportRequestDto
                {
                    ReportType = AcademicReportTypes.AssignmentPerformanceReport,
                    Filters = new() { SubjectIds = [firstSubject.Id, secondSubject.Id] }
                };
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var generator = GetRequiredService<AcademicReportGenerator>();
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, true);
                expected.Rows.Select(row => row["TotalAssignments"]).ShouldBe(["1", "2"]);
                db.ChangeTracker.Clear();
                var export = await generator.GenerateExportAsync(request, ActorId, TenantId, batchSize: 1);
                var actual = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows) actual.Add(row);
                actual.Count.ShouldBe(expected.Rows.Count);
                for (var index = 0; index < actual.Count; index++)
                    actual[index].OrderBy(pair => pair.Key).ShouldBe(expected.Rows[index].OrderBy(pair => pair.Key), $"Tied performance row {index}");
            }
        });
    }

    private async Task<List<Guid>> SeedLearningTasksAsync()
    {
        await SeedAllocationAsync();
        await SeedLessonPlanOwnerAsync();
        var today = DateTime.UtcNow.Date;
        await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(YearId, TenantId, "Report year",
            today.AddMonths(-3), today.AddMonths(9)), autoSave: true);
        var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
        var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
        var students = new List<Guid>();
        string[] names = [" Zoe ", "Ada", "Émile", "Bela"];
        for (var index = 0; index < names.Length; index++)
        {
            var student = new Student(Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"), TenantId,
                names[index], index == 1 ? " \t " : "Middle", "Student", YearId, medium.EducationBoardId,
                AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId);
            await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
            students.Add(student.Id);
        }
        var homeworkType = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Report homework");
        var assignmentType = await GetRequiredService<AssignmentTypeManager>().CreateAsync(AcademicMediumId, "Report assignment",
            defaultSubmissionType: SubmissionType.OnlineText, defaultMarks: 100, defaultEvaluationMethod: EvaluationMethod.Marks);
        var otherDivision = Guid.NewGuid();
        SubmissionStatus[] statuses = [SubmissionStatus.NotSubmitted, SubmissionStatus.Draft, SubmissionStatus.Reviewed,
            SubmissionStatus.Late, SubmissionStatus.Excused, SubmissionStatus.NeedsReview];
        for (var taskIndex = 0; taskIndex < 5; taskIndex++)
        {
            var homework = new HomeworkAssignmentEntity(Guid.Parse($"10000000-0000-0000-0000-{taskIndex + 1:000000000000}"),
                StandardId, SubjectId, $"Homework {4 - taskIndex}", "Details", homeworkType.Id, SubmissionType.OnlineText,
                TenantId, YearId, attachmentsJson: taskIndex % 2 == 0 ? "[{\"name\":\"first\"},{\"name\":\"second\"}]" : "broken-json",
                educationBoardId: medium.EducationBoardId, educationMediumId: AcademicMediumId, educationMediumStageId: standard.EducationMediumStageId);
            homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, DivisionId, null, today.AddDays(taskIndex - 2), TenantId));
            homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, otherDivision, null, today.AddDays(taskIndex + 2), TenantId));
            typeof(HomeworkAssignmentEntity).GetProperty(nameof(homework.CreatorId))!.SetValue(homework, TeacherUserId);
            typeof(HomeworkAssignmentEntity).GetProperty(nameof(homework.CreationTime))!.SetValue(homework, today.AddDays(-10 + taskIndex));
            await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
            var assignment = new AssignmentEntity(Guid.Parse($"20000000-0000-0000-0000-{taskIndex + 1:000000000000}"),
                StandardId, SubjectId, $"Assignment {4 - taskIndex}", "Details", assignmentType.Id, SubmissionType.OnlineText,
                taskIndex == 4 ? 0 : 100, TenantId, YearId, educationBoardId: medium.EducationBoardId,
                educationMediumId: AcademicMediumId, educationMediumStageId: standard.EducationMediumStageId);
            assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, DivisionId, null, today.AddDays(taskIndex - 1), TenantId));
            typeof(AssignmentEntity).GetProperty(nameof(assignment.CreatorId))!.SetValue(assignment, TeacherUserId);
            typeof(AssignmentEntity).GetProperty(nameof(assignment.CreationTime))!.SetValue(assignment, today.AddDays(-12 + taskIndex));
            await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(assignment, autoSave: true);
            for (var studentIndex = 0; studentIndex < students.Count; studentIndex++)
            {
                var ordinal = taskIndex * students.Count + studentIndex + 1;
                var division = studentIndex == 3 ? otherDivision : DivisionId;
                var due = today.AddDays(taskIndex - studentIndex - 2).AddMinutes(ordinal);
                var hwSubmission = new StudentSubmissionEntity(Guid.Parse($"30000000-0000-0000-0000-{ordinal:000000000000}"),
                    homework.Id, students[studentIndex], division, due, TenantId);
                var aSubmission = new StudentSubmissionEntity(Guid.Parse($"40000000-0000-0000-0000-{ordinal:000000000000}"),
                    assignment.Id, students[studentIndex], division, due, TenantId, true);
                foreach (var submission in new[] { hwSubmission, aSubmission })
                {
                    submission.Status = statuses[(taskIndex + studentIndex) % statuses.Length];
                    submission.SubmissionDate = studentIndex == 0 ? null : new DateTimeOffset(due.AddDays(studentIndex - 1));
                    submission.Marks = studentIndex == 0 ? null : 50.5m + studentIndex;
                    submission.TotalMarks = studentIndex == 2 ? 80 : null;
                    submission.ReviewedBy = studentIndex == 0 ? null : TeacherUserId;
                    submission.ReviewedAt = studentIndex == 0 ? null : new DateTimeOffset(today.AddMinutes(ordinal));
                    submission.Feedback = studentIndex == 2 ? "Reviewed, well done" : " \t ";
                    submission.Remarks = studentIndex == 1 ? "Legacy feedback" : null;
                }
                await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertManyAsync([hwSubmission, aSubmission], autoSave: true);
            }
        }
        return students;
    }
}
