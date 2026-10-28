using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
    [Fact]
    public async Task Dynamic_Timetable_Preview_Should_Not_Read_Curriculum_Chapters()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var scope = await DynamicScopeAsync(assignment);
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(new AssignedSubjectChapter(
                    Guid.NewGuid(), assignment.Id, 1, "Unrelated chapter", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                { AssignedSubjectId = assignment.Id, TenantId = tenantId }, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new ReportQueryCapture(db.Database.GetDbConnection());
                var result = await GetRequiredService<AcademicDynamicReportQueryService>().GenerateAsync(new()
                {
                    Module = AcademicReportModules.Timetable, Dataset = AcademicDynamicDatasets.TimetableSlots
                }, scope);
                result.TotalCount.ShouldBe(0);
                var chapterTable = db.Model.FindEntityType(typeof(AssignedSubjectChapter))!.GetTableName();
                capture.Commands.ShouldAllBe(sql => !sql.Contains($"\"{chapterTable}\"", StringComparison.Ordinal));
            }
        });
    }

    [Theory]
    [InlineData(AcademicReportTypes.ChapterProgressReport)]
    [InlineData(AcademicReportTypes.SubjectPrimaryTeacherReport)]
    [InlineData(AcademicReportTypes.SubjectWiseChapterCoverage)]
    [InlineData(AcademicReportTypes.ChapterDescriptionSummary)]
    [InlineData(AcademicReportTypes.TimelineReport)]
    [InlineData(AcademicReportTypes.ClassWiseChapterAssignment)]
    [InlineData(AcademicReportTypes.CurriculumCompletionReport)]
    [InlineData(AcademicReportTypes.ChapterRevisionTracker)]
    public async Task Curriculum_Preview_Should_Page_In_Sql_Without_Hydrating_Entities(string reportType)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantId = Guid.NewGuid();
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var assignment = await SeedAssignmentAsync(tenantId);
                var chapters = Enumerable.Range(1, 321).Select(number => new AssignedSubjectChapter(
                    Guid.NewGuid(), assignment.Id, number, $"Chapter {number}",
                    new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero))
                { AssignedSubjectId = assignment.Id, TenantId = tenantId }).ToList();
                await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertManyAsync(chapters, autoSave: true);
                var originalSubject = await GetRequiredService<IRepository<Subject, Guid>>().GetAsync(assignment.SubjectId);
                foreach (var name in new[] { "Zebra", "alpha", "Älgebra" })
                {
                    var subject = new Subject(Guid.NewGuid(), tenantId, name, "#112233", originalSubject.EducationMediumId);
                    await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
                    var additional = new AssignedSubject(Guid.NewGuid(), tenantId, assignment.StandardId, subject.Id, name);
                    await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(additional, autoSave: true);
                    if (name != "Zebra")
                        await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().InsertAsync(new AssignedSubjectChapter(
                            Guid.NewGuid(), additional.Id, 1, $"{name} first chapter", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
                        { AssignedSubjectId = additional.Id, TenantId = tenantId }, autoSave: true);
                }
                var divisionId = Guid.NewGuid();
                await GetRequiredService<IRepository<ChapterProgress, Guid>>().InsertManyAsync(chapters.Take(3).Select(chapter =>
                    new ChapterProgress(chapter.Id, divisionId) { Progress = 100 }), autoSave: true);
                var generator = GetRequiredService<AcademicReportGenerator>();
                var request = new AcademicReportRequestDto
                {
                    ReportType = reportType, PageSize = 2,
                    Filters = new AcademicReportFilterDto { DivisionIds = [divisionId] }
                };
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var expected = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: true);
                foreach (var page in new[] { 1, 2, 999 })
                {
                    db.ChangeTracker.Clear();
                    request.Page = page;
                    using var capture = new ReportQueryCapture(db.Database.GetDbConnection());
                    var actual = await generator.GenerateAsync(request, Guid.NewGuid(), tenantId, exportAllRows: false);
                    actual.TotalCount.ShouldBe(expected.TotalCount);
                    actual.Columns.Select(column => (column.Id, column.Label)).ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)));
                    var expectedRows = expected.Rows.Skip((page - 1) * request.PageSize).Take(request.PageSize).ToList();
                    actual.Rows.Count.ShouldBe(expectedRows.Count);
                    for (var index = 0; index < expectedRows.Count; index++)
                        foreach (var cell in expectedRows[index]) actual.Rows[index][cell.Key].ShouldBe(cell.Value);
                    db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<AssignedSubjectChapter>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<ChapterProgress>().ShouldBeEmpty();
                    if (actual.Rows.Count > 0)
                    {
                        capture.Reads.ShouldNotBeEmpty();
                        capture.Commands.ShouldContain(sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
                        if (reportType is not (AcademicReportTypes.SubjectPrimaryTeacherReport or AcademicReportTypes.SubjectWiseChapterCoverage
                            or AcademicReportTypes.CurriculumCompletionReport))
                            capture.Reads.ShouldContain(read => read.Sql.Contains("\"ChapterName\"", StringComparison.Ordinal));
                    }
                    capture.Reads.Where(read => read.Sql.Contains("\"ChapterName\"", StringComparison.Ordinal))
                        .ShouldAllBe(read => read.Rows <= request.PageSize + 1);
                }
            }
        });
    }
}
