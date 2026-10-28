using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Audit;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.StudentAcademic360;
using ShikshaMitra.Academic.Dashboard;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Performance;

[Collection(AcademicPerformanceCollection.Name)]
public sealed class AcademicReportPostgresSmoke_Tests(ITestOutputHelper output)
{
    [AcademicPerformancePostgresFact]
    public async Task All_Static_Exports_And_Dynamic_Datasets_Should_Translate_On_Postgres()
    {
        // The existing factory refuses non-loopback, incorrectly named, or already populated databases.
        await using var factory = new AcademicPerformancePostgresTestFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        var userId = Guid.Parse(TestAuthHandler.TestUserId);
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
         new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], TestAuthHandler.SchemeName)));
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
        var fixture = await SeedAsync(factory, services, db, tenantId);
        db.ChangeTracker.Clear();
        var studentIds = await db.Set<Student>().Where(student => student.FirstName.StartsWith("Provider"))
            .Select(student => student.Id).ToListAsync();
        studentIds.Count.ShouldBe(2);
        var fixtureStudents = (await services.GetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync())
            .Where(student => studentIds.Contains(student.Id) && student.AcademicYearId == fixture.AcademicYearId &&
                student.AcademicBoardId == factory.SeededBoardId && student.AcademicMediumId == factory.SeededMediumId &&
                student.StandardId == factory.SeededStandardId && student.DivisionId == fixture.DivisionId);
        (await fixtureStudents.CountAsync()).ShouldBe(2, "The smoke students must share the report's academic context.");
        var scopedStudents = await services.GetRequiredService<StudentScopeAccessService>().ApplyCurrentUserScopeAsync(fixtureStudents);
        (await scopedStudents.CountAsync()).ShouldBe(2, "The persisted report-user scope must authorize both smoke students.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var generator = services.GetRequiredService<AcademicReportGenerator>();
        var examService = services.GetRequiredService<IExamAppService>();
        var scheduleDate = await db.Set<ExamSubject>().Where(subject => subject.ExamId == fixture.ExamId)
            .Select(subject => subject.ExamDate!.Value).FirstAsync();
        var examPage = await examService.GetListAsync(new GetExamsInput
        {
            AcademicMediumId = factory.SeededMediumId, StandardId = factory.SeededStandardId,
            DivisionId = fixture.DivisionId, Status = ExamStatus.Completed,
            StartDate = new DateTimeOffset(scheduleDate.Date, TimeSpan.Zero),
            EndDate = new DateTimeOffset(scheduleDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero),
            MaxResultCount = 2
        });
        examPage.Items.ShouldContain(exam => exam.ExamId == fixture.ExamId && exam.Status == ExamStatus.Completed);
        examPage.CompletedCount.ShouldBeGreaterThanOrEqualTo(1);
        var kinds = typeof(AcademicReportTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string)).Select(field => (string)field.GetRawConstantValue()!).ToList();
        kinds.Count.ShouldBe(68);
        var totalRows = 0;
        foreach (var kind in kinds)
        {
            try
            {
                var export = await generator.GenerateExportAsync(new AcademicReportRequestDto
                {
                    ReportType = kind,
                    Filters = new() { ExamId = fixture.ExamId, ClassIds = [factory.SeededStandardId],
                        DivisionIds = [fixture.DivisionId], StudentIds = studentIds }
                }, userId, tenantId, cancellation.Token, batchSize: 1);
                export.Metadata.Columns.ShouldNotBeEmpty(kind);
                var rows = 0;
                var leadingRows = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows.WithCancellation(cancellation.Token))
                {
                    row.ShouldNotBeNull(kind);
                    if (leadingRows.Count < 2) leadingRows.Add(row);
                    row.Values.ShouldAllBe(value => value != "NaN" && value != "Infinity" && value != "NaN%" && value != "Infinity%", kind);
                    (++rows).ShouldBeLessThan(1000, kind);
                }
                totalRows += rows;
                output.WriteLine($"Static {kind}: {rows} rows");
                if (kind is AcademicReportTypes.ChapterProgressReport or AcademicReportTypes.SubjectPrimaryTeacherReport
                    or AcademicReportTypes.SubjectWiseChapterCoverage or AcademicReportTypes.ChapterDescriptionSummary
                    or AcademicReportTypes.TimelineReport or AcademicReportTypes.ClassWiseChapterAssignment
                    or AcademicReportTypes.CurriculumCompletionReport or AcademicReportTypes.ChapterRevisionTracker
                    or AcademicReportTypes.ClasswiseTimetable or AcademicReportTypes.TeacherwiseTimetable
                    or AcademicReportTypes.RoomUtilizationReport or AcademicReportTypes.SubjectwiseTimetableDistribution
                    or AcademicReportTypes.FreePeriodsReport or AcademicReportTypes.TimetableChangeLog ||
                    AcademicReportGenerator.IsExamScopedReport(kind) ||
                    kind is AcademicReportTypes.StudentAllBatchResult or AcademicReportTypes.StudentAllExamResult)
                {
                    foreach (var page in new[] { 1, 2, 999 })
                    {
                        var preview = await generator.GenerateAsync(new AcademicReportRequestDto
                        {
                            ReportType = kind, Page = page, PageSize = 1,
                            Filters = new() { ExamId = fixture.ExamId, ClassIds = [factory.SeededStandardId],
                                DivisionIds = [fixture.DivisionId], StudentIds = studentIds }
                        }, userId, tenantId, exportAllRows: false);
                        preview.TotalCount.ShouldBe(rows, kind);
                        preview.Columns.Select(column => (column.Id, column.Label))
                            .ShouldBe(export.Metadata.Columns.Select(column => (column.Id, column.Label)), kind);
                        var expectedRows = kind is AcademicReportTypes.ExamSummaryReport or AcademicReportTypes.ResultListPassFailOverallAnalysis
                            ? leadingRows.Take(1).ToList() : leadingRows.Skip(page - 1).Take(1).ToList();
                        preview.Rows.Count.ShouldBe(expectedRows.Count, kind);
                        for (var index = 0; index < expectedRows.Count; index++)
                            foreach (var cell in expectedRows[index]) preview.Rows[index][cell.Key].ShouldBe(cell.Value, kind);
                    }
                }
            }
            catch (Exception exception) { throw new InvalidOperationException($"PostgreSQL static export failed: {kind}", exception); }
        }
        totalRows.ShouldBeGreaterThan(30);
        var catalog = services.GetRequiredService<AcademicDynamicReportCatalog>();
        var queryService = services.GetRequiredService<AcademicDynamicReportQueryService>();
        var catalogScope = new AcademicAiReportCatalogScope(fixture.AcademicYearId, factory.SeededBoardId,
            factory.SeededMediumId, [factory.SeededStandardId], [fixture.DivisionId]);
        var snapshot = await catalog.BuildSnapshotAsync(catalogScope);
        snapshot.Classes.ShouldContain(item => item.Id == factory.SeededStandardId);
        snapshot.Divisions.ShouldContain(item => item.Id == fixture.DivisionId);
        snapshot.Subjects.ShouldNotBeEmpty();
        snapshot.Chapters.ShouldNotBeEmpty();
        var modules = new[] { AcademicReportModules.Curriculum, AcademicReportModules.Timetable,
            AcademicReportModules.HomeworkAssignment, AcademicReportModules.Examination,
            AcademicReportModules.StudentRecords, AcademicReportModules.StudentAttendance };
        var datasets = modules.SelectMany(catalog.GetDatasets).ToList();
        datasets.Count.ShouldBe(12);
        foreach (var dataset in datasets)
        {
            try
            {
                var result = await queryService.GenerateAsync(new AcademicDynamicReportSpecDto
                {
                    Module = dataset.Module, Dataset = dataset.Id, Columns = dataset.DefaultColumns.ToList()
                }, catalogScope);
                result.Columns.ShouldNotBeEmpty(dataset.Id);
                result.TotalCount.ShouldBe(result.Rows.Count, dataset.Id);
                result.TotalCount.ShouldBeGreaterThan(0, dataset.Id);
                result.TotalCount.ShouldBeLessThan(1000, dataset.Id);
                output.WriteLine($"Dynamic {dataset.Id}: {result.TotalCount} rows");
            }
            catch (Exception exception) { throw new InvalidOperationException($"PostgreSQL dynamic dataset failed: {dataset.Id}", exception); }
        }
        async Task CheckReadAsync(string name, Func<Task> read)
        {
            try { await read(); output.WriteLine($"Visible UI read translated: {name}"); }
            catch (Exception exception) { throw new InvalidOperationException($"PostgreSQL visible UI read failed: {name}", exception); }
        }
        await CheckReadAsync("subject lookup", async () => { _ = await services.GetRequiredService<ISubjectAppService>().GetLookupAsync(factory.SeededMediumId); });
        await CheckReadAsync("subject allocation page", async () => { _ = await services.GetRequiredService<ISubjectAssignmentAppService>().GetListAsync(factory.SeededStandardId, 1, 2); });
        var chapterSubjectId = await db.Set<AssignedSubject>().Where(subject => subject.StandardId == factory.SeededStandardId).Select(subject => subject.SubjectId).FirstAsync();
        await CheckReadAsync("chapter lookup", async () => { _ = await services.GetRequiredService<IChapterAppService>().GetLookupAsync(factory.SeededStandardId, chapterSubjectId, fixture.DivisionId); });
        await CheckReadAsync("chapter linked statuses", async () => { _ = await services.GetRequiredService<IChapterAppService>().GetByStandardAndSubjectAsync(factory.SeededStandardId, chapterSubjectId, fixture.DivisionId); });
        await CheckReadAsync("lesson workspace", async () => { _ = await services.GetRequiredService<ILessonPlanAppService>().GetWorkspaceAsync(new GetLessonPlanWorkspaceInput
            { AcademicYearId = fixture.AcademicYearId, AcademicMediumId = factory.SeededMediumId, MaxResultCount = 2 }); });
        await CheckReadAsync("academic audit page", async () => { _ = await services.GetRequiredService<IAcademicAuditTrailAppService>().GetListAsync(new AcademicAuditEventListRequestDto
            { AcademicMediumId = factory.SeededMediumId, MaxResultCount = 2 }); });
        var roomReads = services.GetRequiredService<IRoomAvailabilityAppService>();
        var uiDate = DateTime.SpecifyKind(scheduleDate.Date, DateTimeKind.Unspecified);
        await CheckReadAsync("room reservation page", async () => { _ = await roomReads.GetReservationsPageAsync(new GetRoomReservationsPageInputDto
            { CreatedDate = uiDate, SearchTerm = "28-10-2026", SearchColumns = ["date", "createdAt"], Sorting = "createdAt desc", MaxResultCount = 2 }); });
        await CheckReadAsync("room reservation badge", async () => { _ = await roomReads.GetReservationSummaryAsync(new GetReservationsInputDto { CreatedDate = uiDate }); });
        await CheckReadAsync("room audit page", async () => { _ = await roomReads.GetAuditLogsPageAsync(new GetRoomAuditLogsInputDto { FromDate = uiDate, ToDate = uiDate, MaxResultCount = 2 }); });
        await CheckReadAsync("room availability", async () => { _ = await roomReads.GetAvailabilityAsync(new RoomAvailabilityFilterDto { Date = uiDate }); });
        var academic360 = services.GetRequiredService<IStudentAcademic360AppService>();
        await CheckReadAsync("student 360 page and cohort summary", async () =>
        {
            var page = await academic360.GetListAsync(new GetStudentAcademic360ListInput
            {
                AcademicYearId = fixture.AcademicYearId, BoardId = factory.SeededBoardId, EducationMediumId = factory.SeededMediumId,
                StandardId = factory.SeededStandardId, DivisionId = fixture.DivisionId, MaxResultCount = 2
            });
            page.Items.ShouldContain(item => studentIds.Contains(item.StudentId));
        });
        await CheckReadAsync("student 360 bounded detail", async () => { _ = await academic360.GetAsync(studentIds[0], new StudentAcademic360Input
            { AcademicYearId = fixture.AcademicYearId, DetailPaging = true, TaskMaxResultCount = 20, ExamMaxResultCount = 10 }); });
        var dashboard = services.GetRequiredService<IAcademicDashboardAppService>();
        await CheckReadAsync("principal operations section", async () => { _ = await dashboard.GetPrincipalOperationsAsync(); });
        await CheckReadAsync("teacher assessment section", async () => { _ = await dashboard.GetTeacherAssessmentAsync(); });
        await uow.CompleteAsync(cancellation.Token);
    }

    private static async Task<ReportFixture> SeedAsync(AcademicPerformancePostgresTestFactory factory, IServiceProvider services,
        ShikshaMitraDbContext db, Guid tenantId)
    {
        var today = DateTime.UtcNow.Date;
        // The shared factory selects its first year and division independently. Reports need
        // a coherent board/year/class/division path for the catalog and student scope joins.
        var academicYearId = await db.Set<EducationBoard>().Where(board => board.Id == factory.SeededBoardId)
            .Select(board => board.AcademicYearId).SingleAsync();
        var divisionId = await db.Set<Division>().Where(division => division.StandardId == factory.SeededStandardId)
            .OrderBy(division => division.Id).Select(division => division.Id).FirstAsync();
        var userId = Guid.Parse(TestAuthHandler.TestUserId);
        // Student report scope is resolved from persisted assignments, not the principal's
        // admin claim. Give this fixture a concrete year scope before reading student rows.
        db.Add(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId, academicYearId: academicYearId,
            description: "PostgreSQL report smoke academic-year scope"));
        await db.SaveChangesAsync();
        await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
        var teacher = await db.Set<Employee>().Select(employee => new { employee.Id, employee.UserId }).FirstAsync();
        var subjects = Enumerable.Range(1, 2).Select(index => new Subject(Guid.NewGuid(), tenantId,
            $"Postgres report subject {index}", "#112233", factory.SeededMediumId)).ToList();
        db.AddRange(subjects);
        var examType = new ExamType(Guid.NewGuid(), tenantId, "Postgres report exam type", null, factory.SeededMediumId);
        db.Add(examType);
        var exam = new Exam(Guid.NewGuid(), examType.Id, "Postgres report exam", factory.SeededStandardId, tenantId);
        exam.AddDivision(Guid.NewGuid(), divisionId);
        exam.ChangeStatus(ExamStatus.Published);
        foreach (var subject in subjects)
        {
            exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.Id, today.AddDays(-1),
                today.AddDays(-1).AddHours(9), today.AddDays(-1).AddHours(10), 100, 35));
            var assigned = new AssignedSubject(Guid.NewGuid(), tenantId, factory.SeededStandardId, subject.Id, $"PG{subjects.IndexOf(subject)}");
            assigned.Teachers.Add(new AssignedSubjectTeacher(assigned.Id, subject.Id, teacher.Id, SubjectTeacherType.Primary, tenantId));
            assigned.Chapters.Add(new AssignedSubjectChapter(Guid.NewGuid(), assigned.Id, 1, "Provider chapter",
                new DateTimeOffset(today, TimeSpan.Zero), new DateTimeOffset(today.AddDays(1), TimeSpan.Zero))
                { AssignedSubjectId = assigned.Id, TenantId = tenantId, ProgressPercentage = 50 });
            db.Add(assigned);
        }
        db.Add(exam);
        var students = Enumerable.Range(1, 2).Select(index => new Student(Guid.NewGuid(), tenantId, $"Provider{index}", null, "Student",
            academicYearId, factory.SeededBoardId, factory.SeededMediumId, factory.SeededMediumStageId,
            factory.SeededStandardId, divisionId, 9000 + index, 9000 + index, StudentLifeCycleStatusEnum.Admission)).ToList();
        var resultHeader = new ExamResultHeader(Guid.NewGuid(), exam.Id, divisionId, userId, tenantId);
        resultHeader.Publish(userId);
        db.Add(resultHeader);
        foreach (var student in students)
        {
            student.ApproveAdmission();
            db.Add(student);
            foreach (var subject in subjects)
            {
                var mark = new ExamMark(Guid.NewGuid(), exam.Id, student.Id, subject.Id, divisionId, 75, false, tenantId: tenantId);
                mark.Publish();
                db.Add(mark);
            }
            db.Add(new ExamResultDetail(Guid.NewGuid(), resultHeader.Id, exam.Id, student.Id, divisionId,
                200, 150, 75, "A", students.IndexOf(student) + 1, students.IndexOf(student) + 1,
                ExamStudentResultStatus.Pass, tenantId));
        }
        var timetable = new TimetableEntity(Guid.NewGuid(), tenantId, factory.SeededStandardId, divisionId);
        var period = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 1, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotType.Period);
        period.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), period.Id, subjects[0].Id, teacher.Id));
        timetable.AddSlot(period);
        timetable.AddSlot(new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Tuesday, 1, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotType.Period));
        db.Add(timetable);
        await db.SaveChangesAsync();
        var homeworkType = await services.GetRequiredService<HomeworkTypeManager>().CreateAsync(factory.SeededMediumId, "Provider homework");
        var assignmentType = await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(factory.SeededMediumId, "Provider assignment",
            defaultSubmissionType: SubmissionType.OnlineText, defaultMarks: 100, defaultEvaluationMethod: EvaluationMethod.Marks);
        var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), factory.SeededStandardId, subjects[0].Id, "Provider homework", "Details",
            homeworkType.Id, SubmissionType.OnlineText, tenantId, academicYearId,
            attachmentsJson: "[{\"name\":\"example\"}]", educationBoardId: factory.SeededBoardId,
            educationMediumId: factory.SeededMediumId, educationMediumStageId: factory.SeededMediumStageId);
        homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, divisionId, null, today.AddDays(-1), tenantId));
        homework.PublishAll();
        homework.ChangeStatus(HomeworkStatus.Published);
        var assignment = new AssignmentEntity(Guid.NewGuid(), factory.SeededStandardId, subjects[0].Id, "Provider assignment", "Details",
            assignmentType.Id, SubmissionType.OnlineText, 100, tenantId, academicYearId,
            educationBoardId: factory.SeededBoardId, educationMediumId: factory.SeededMediumId,
            educationMediumStageId: factory.SeededMediumStageId);
        assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, divisionId, null, today.AddDays(-1), tenantId));
        assignment.PublishAll();
        assignment.ChangeStatus(AssignmentStatus.Published);
        db.AddRange(homework, assignment);
        foreach (var student in students)
        {
            foreach (var submission in new[] {
                new StudentSubmissionEntity(Guid.NewGuid(), homework.Id, student.Id, divisionId, today.AddDays(-1), tenantId),
                new StudentSubmissionEntity(Guid.NewGuid(), assignment.Id, student.Id, divisionId, today.AddDays(-1), tenantId, true) })
            {
                submission.Status = SubmissionStatus.Reviewed;
                submission.SubmissionDate = new DateTimeOffset(today, TimeSpan.Zero);
                submission.Marks = 75; submission.TotalMarks = 100;
                submission.ReviewedBy = teacher.UserId; submission.ReviewedAt = new DateTimeOffset(today, TimeSpan.Zero);
                submission.Feedback = "Provider feedback";
                db.Add(submission);
            }
        }
        await db.SaveChangesAsync();
        var attendanceManager = services.GetRequiredService<AttendanceManager>();
        var attendanceDay = await attendanceManager.GetOrCreateAttendanceDayAsync(academicYearId, factory.SeededStandardId, divisionId, today);
        await attendanceManager.MarkAttendanceAsync(attendanceDay.Id, students.Select((student, index) => new AttendanceManager.AttendanceEntryInput
        {
            StudentId = student.Id, Status = index == 0 ? AttendanceStatus.Present : AttendanceStatus.Absent,
            Remarks = "Provider attendance"
        }).ToList());
        await db.SaveChangesAsync();
        return new(exam.Id, academicYearId, divisionId);
    }

    private sealed record ReportFixture(Guid ExamId, Guid AcademicYearId, Guid DivisionId);
}
