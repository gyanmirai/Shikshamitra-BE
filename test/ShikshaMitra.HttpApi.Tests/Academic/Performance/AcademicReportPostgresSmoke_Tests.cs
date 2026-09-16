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
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
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
                await foreach (var row in export.Rows.WithCancellation(cancellation.Token))
                {
                    row.ShouldNotBeNull(kind);
                    row.Values.ShouldAllBe(value => value != "NaN" && value != "Infinity" && value != "NaN%" && value != "Infinity%", kind);
                    (++rows).ShouldBeLessThan(1000, kind);
                }
                totalRows += rows;
                output.WriteLine($"Static {kind}: {rows} rows");
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
