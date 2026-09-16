using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Chapter_Reports_Should_Preserve_Midpoint_Rounding_Null_Dates_And_Division_Filter()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var chapters = Enumerable.Range(1, 3).Select(number => new AssignedSubjectChapter(
                    Guid.NewGuid(), assignment.Id, number, $"Chapter {number}",
                    new DateTimeOffset(2026, 9, number, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero))
                {
                    AssignedSubjectId = assignment.Id,
                    TenantId = tenantId
                }).ToList();
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertManyAsync(chapters, autoSave: true);
                var divisionIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
                var progress = new List<ChapterProgress>
                {
                    new(chapters[0].Id, divisionIds[0]) { Progress = 50 },
                    new(chapters[0].Id, divisionIds[1]) { Progress = 51 },
                    new(chapters[1].Id, divisionIds[0]) { Progress = 51, StartDate = new DateTime(2026, 9, 5) },
                    new(chapters[1].Id, divisionIds[1]) { Progress = 52, StartDate = new DateTime(2026, 9, 3), CompletionDate = new DateTime(2026, 9, 9) },
                    new(chapters[1].Id, divisionIds[2]) { Progress = 51, CompletionDate = new DateTime(2026, 9, 12) },
                    new(chapters[1].Id, divisionIds[3]) { Progress = 52, StartDate = new DateTime(2026, 9, 4), CompletionDate = new DateTime(2026, 9, 10) },
                    new(chapters[0].Id, Guid.NewGuid()) { Progress = 100, StartDate = new DateTime(2026, 1, 1) }
                };
                await GetRequiredService<IRepository<ChapterProgress, Guid>>().InsertManyAsync(progress, autoSave: true);
                var generator = GetRequiredService<AcademicReportGenerator>();
                var request = new AcademicReportRequestDto
                {
                    ReportType = AcademicReportTypes.ChapterProgressReport,
                    Filters = new AcademicReportFilterDto { DivisionIds = divisionIds }
                };

                var result = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: false);
                result.TotalCount.ShouldBe(3);
                result.Rows.Select(row => row["Progress"]).ShouldBe(new[] { "50%", "52%", "0%" });

                request.ReportType = AcademicReportTypes.TimelineReport;
                var timeline = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: false);
                var first = timeline.Rows.Single(row => row["ChapterName"] == "Chapter 1");
                first["ActualStartDate"].ShouldBe(string.Empty);
                first["ActualEndDate"].ShouldBe(string.Empty);
                first["Status"].ShouldBe("In Progress");
                var second = timeline.Rows.Single(row => row["ChapterName"] == "Chapter 2");
                second["ActualStartDate"].ShouldBe("03-09-2026");
                second["ActualEndDate"].ShouldBe("12-09-2026");
                var missing = timeline.Rows.Single(row => row["ChapterName"] == "Chapter 3");
                missing["ActualStartDate"].ShouldBe(string.Empty);
                missing["ActualEndDate"].ShouldBe(string.Empty);
                missing["Status"].ShouldBe("Upcoming");
            }
        });
    }

    [Theory]
    [InlineData(" Ada ", " Lovelace ", " Ada   Lovelace ")]
    [InlineData(" \t", " Lovelace ", " Lovelace ")]
    [InlineData("Ada", null, "Ada")]
    [InlineData(null, null, "")]
    public async Task Teacher_Report_Should_Preserve_Name_Formatting_And_Only_Load_Teacher_Lookups(
        string? firstName, string? lastName, string expectedName)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var teacher = await SeedTeacherAsync(tenantId, firstName, lastName);
                assignment.Teachers.Add(new AssignedSubjectTeacher(assignment.Id, assignment.SubjectId,
                    teacher.Id, SubjectTeacherType.Primary, tenantId));
                await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(assignment, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var generator = GetRequiredService<AcademicReportGenerator>();
                using (var capture = new ReportQueryCapture(db.Database.GetDbConnection()))
                {
                    var result = await generator.GenerateAsync(new AcademicReportRequestDto
                    {
                        ReportType = AcademicReportTypes.SubjectPrimaryTeacherReport
                    }, Guid.NewGuid(), tenantId, exportAllRows: false);
                    result.TotalCount.ShouldBe(1);
                    result.Rows.Single()["PrimaryTeacher"].ShouldBe(expectedName);
                    // Count this report's lookup directly; provider scope queries are separate concerns.
                    var employeeTable = db.Model.FindEntityType(typeof(Employee))!.GetTableName();
                    var employeeQueries = capture.Commands.Where(sql => sql.Contains($"FROM \"{employeeTable}\"", StringComparison.Ordinal)).ToList();
                    employeeQueries.Count.ShouldBe(1);
                    employeeQueries[0].ShouldNotContain("\"RoleId\"");
                    foreach (var unusedType in new[] { typeof(Room), typeof(RoomType) })
                    {
                        var table = db.Model.FindEntityType(unusedType)!.GetTableName();
                        capture.Commands.ShouldAllBe(sql => !sql.Contains($"\"{table}\"", StringComparison.Ordinal));
                    }
                }
                db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
            }
        });
    }

    [Fact]
    public async Task Export_History_Should_Load_Only_Latest_100_For_Current_User_And_Tenant()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var currentTenant = GetRequiredService<ICurrentTenant>();
            var repository = GetRequiredService<IRepository<HrReportExportJob, Guid>>();
            var createdAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var jobs = Enumerable.Range(0, 105).Select(index => CreateExport(tenantId, userId, createdAt.AddMinutes(index))).ToList();
            using (currentTenant.Change(tenantId))
            {
                await repository.InsertManyAsync(jobs, autoSave: true);
                await repository.InsertAsync(CreateExport(tenantId, Guid.NewGuid(), createdAt.AddDays(1)), autoSave: true);
            }
            using (currentTenant.Change(Guid.NewGuid()))
            {
                await repository.InsertAsync(CreateExport(currentTenant.Id!.Value, userId, createdAt.AddDays(2)), autoSave: true);
            }
            using (currentTenant.Change(tenantId))
            using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) }, "Test"))))
            {
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var result = await GetRequiredService<IAcademicReportAppService>().GetExportsAsync();
                result.Select(job => job.Id).ShouldBe(jobs.OrderByDescending(job => job.CreationTime).Take(100).Select(job => job.Id));
                result.ShouldAllBe(job => job.RequestJson == null);
                // The limit must be applied before materialization, not after loading every export.
                db.ChangeTracker.Entries<HrReportExportJob>().Count().ShouldBeLessThanOrEqualTo(100);
            }
        });
    }

    private async Task<AssignedSubject> SeedAssignmentAsync(Guid tenantId)
    {
        var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
        var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
        var standard = new Standard(Guid.NewGuid(), tenantId, "Report class", 8, stageId, null);
        await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, autoSave: true);
        var subject = new Subject(Guid.NewGuid(), tenantId, "Report subject", "#00A76F", stage.EducationMediumId);
        await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
        var assignment = new AssignedSubject(Guid.NewGuid(), tenantId, standard.Id, subject.Id, "REP");
        return await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(assignment, autoSave: true);
    }

    [Fact]
    public async Task Curriculum_Export_Should_Match_All_Report_Rows_Across_Sql_Chunks()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var subject = await GetRequiredService<IRepository<Subject, Guid>>().GetAsync(assignment.SubjectId);
                var anotherSubject = new Subject(Guid.NewGuid(), tenantId, "Aardvark", "#00A76F", subject.EducationMediumId);
                await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(anotherSubject, autoSave: true);
                var another = new AssignedSubject(Guid.NewGuid(), tenantId, assignment.StandardId, anotherSubject.Id, "AAR");
                await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(another, autoSave: true);
                var teacher = await SeedTeacherAsync(tenantId, " Ada ", " Lovelace ");
                assignment.Teachers.Add(new AssignedSubjectTeacher(assignment.Id, assignment.SubjectId, teacher.Id, SubjectTeacherType.Primary, tenantId));
                await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(assignment, autoSave: true);
                var chapters = new[] { assignment, another }.SelectMany(item => Enumerable.Range(1, 4).Select(number =>
                    new AssignedSubjectChapter(Guid.NewGuid(), item.Id, number, $"Chapter {number}",
                        new DateTimeOffset(2026, 9, 10 - number, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero), "<p>Content &amp; details</p>")
                    { AssignedSubjectId = item.Id, TenantId = tenantId })).ToList();
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertManyAsync(chapters, autoSave: true);
                var divisionId = Guid.NewGuid();
                await GetRequiredService<IRepository<ChapterProgress, Guid>>().InsertManyAsync(chapters.Select(chapter =>
                    new ChapterProgress(chapter.Id, divisionId)
                    {
                        Progress = chapter.ChapterNumber % 2 == 0 ? 100 : 25,
                        StartDate = chapter.ChapterNumber == 1 ? null : new DateTime(2026, 9, 3),
                        CompletionDate = chapter.ChapterNumber % 2 == 0 ? new DateTime(2026, 9, 14) : null
                    }), autoSave: true);
                var types = new[]
                {
                    AcademicReportTypes.ChapterProgressReport, AcademicReportTypes.SubjectPrimaryTeacherReport,
                    AcademicReportTypes.SubjectWiseChapterCoverage, AcademicReportTypes.ChapterDescriptionSummary,
                    AcademicReportTypes.TimelineReport, AcademicReportTypes.ClassWiseChapterAssignment,
                    AcademicReportTypes.CurriculumCompletionReport, AcademicReportTypes.ChapterRevisionTracker
                };
                var generator = GetRequiredService<AcademicReportGenerator>();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                foreach (var type in types)
                {
                    var request = new AcademicReportRequestDto
                    {
                        ReportType = type,
                        Filters = new AcademicReportFilterDto { DivisionIds = [divisionId], ChapterIds = [chapters[0].Id, chapters[3].Id, chapters[5].Id] }
                    };
                    var expected = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: true);
                    db.ChangeTracker.Clear();
                    var export = await generator.GenerateExportAsync(request, Guid.NewGuid(), tenantId, batchSize: 2);
                    var actualRows = new List<Dictionary<string, string?>>();
                    await foreach (var row in export.Rows) actualRows.Add(row);
                    export.Metadata.Columns.Select(column => (column.Id, column.Label))
                        .ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)), type);
                    actualRows.Count.ShouldBe(expected.TotalCount, type);
                    for (var index = 0; index < expected.Rows.Count; index++)
                    {
                        actualRows[index].Count.ShouldBe(expected.Rows[index].Count, type);
                        foreach (var cell in expected.Rows[index]) actualRows[index][cell.Key].ShouldBe(cell.Value, $"{type}: {index}, {cell.Key}");
                    }
                    db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<ChapterProgress>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
                }
            }
        });
    }

    private async Task<Employee> SeedTeacherAsync(Guid tenantId, string? firstName, string? lastName, string panNo = "ABCDE1234F", string adharNo = "123456789012")
    {
        var roleId = Guid.NewGuid();
        await GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(
            new IdentityRole(roleId, $"report-teacher-{roleId:N}", tenantId), autoSave: true);
        var teacher = new Employee(Guid.NewGuid(), tenantId, null, $"REP-{Guid.NewGuid():N}",
            new EmployeeBasicInfo(
                ProfilePictureBlobName: null, LastName: lastName, FirstName: firstName, MiddleName: "Ignored",
                ShortName: null, EmployeeStatusType: null, DateOfBirth: null, FamilyCast: null, CastType: null,
                GenderType: null, Religion: null, MaritalStatus: null, BloodGroup: null, MobileNo: null,
                SecondaryMobileNo: null, Email: null, PanNo: panNo, AdharNo: adharNo,
                Address1: null, Address2: null, Address3: null, Department: null, Designation: null,
                DateOfJoining: null, EmployeeGroup: null, Qualification: null, RoleId: roleId,
                IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null),
            bankDetails: null, workExperiences: null);
        return await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(teacher, autoSave: true);
    }

    private static HrReportExportJob CreateExport(Guid tenantId, Guid userId, DateTime creationTime)
    {
        var job = new HrReportExportJob(Guid.NewGuid(), tenantId, userId, AcademicReportTypes.ChapterProgressReport,
            AcademicReportFormat.Excel, "{}", "{}", creationTime.AddDays(30));
        typeof(HrReportExportJob).GetProperty(nameof(HrReportExportJob.CreationTime))!.SetValue(job, creationTime);
        return job;
    }

    private sealed class ReportQueryCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly DbConnection _connection;
        private readonly IDisposable _listenerSubscription;
        private readonly ConcurrentBag<IDisposable> _subscriptions = new();
        private readonly ConcurrentDictionary<Guid, string> _commands = new();

        public ReportQueryCapture(DbConnection connection)
        {
            _connection = connection;
            _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public IReadOnlyCollection<string> Commands => _commands.Values.ToList();

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
            {
                _subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is CommandExecutedEventData data && data.Context != null &&
                ReferenceEquals(data.Context.Database.GetDbConnection(), _connection))
            {
                _commands.TryAdd(data.CommandId, data.Command.CommandText);
            }
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription.Dispose();
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
