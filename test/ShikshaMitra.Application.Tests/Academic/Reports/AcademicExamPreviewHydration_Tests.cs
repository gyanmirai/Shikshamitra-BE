using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicExamExportParity_Tests
{
    [Theory]
    [InlineData(AcademicReportTypes.ResultList)]
    [InlineData(AcademicReportTypes.SubjectwiseMarksReport)]
    [InlineData(AcademicReportTypes.DetailedResultSheet)]
    public async Task Exam_Preview_Should_Hydrate_Only_Page_Students_When_321_Participants_Are_Added(string reportType)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var (examId, _, _) = await SeedExamAsync(false);
                var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
                var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
                var students = Enumerable.Range(0, 321).Select(index => new Student(Guid.NewGuid(), TenantId,
                    $"Large preview {index}", null, "Participant", YearId, medium.EducationBoardId, AcademicMediumId,
                    standard.EducationMediumStageId, StandardId, DivisionId, rollNo: 1000 + index)).ToList();
                await GetRequiredService<IRepository<Student, Guid>>().InsertManyAsync(students, autoSave: true);
                await GetRequiredService<IRepository<ExamMark, Guid>>().InsertManyAsync(students.Select((student, index) =>
                    new ExamMark(Guid.NewGuid(), examId, student.Id, SubjectId, DivisionId, index % 101, false, tenantId: TenantId)), autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var result = await GetRequiredService<AcademicReportGenerator>().GenerateAsync(new()
                {
                    ReportType = reportType, Page = 2, PageSize = 2,
                    Filters = new() { ExamId = examId, ClassIds = [StandardId] }
                }, ActorId, TenantId, exportAllRows: false);
                result.TotalCount.ShouldBe(321 + (reportType == AcademicReportTypes.SubjectwiseMarksReport ? 10 : 5));
                result.Rows.Count.ShouldBe(2);
                var bodyReads = capture.Reads.Where(read => read.Sql.Contains("\"ExtraProperties\"", StringComparison.Ordinal)
                    && read.Sql.Contains("\"FirstName\"", StringComparison.Ordinal)).ToList();
                bodyReads.ShouldNotBeEmpty();
                bodyReads.ShouldAllBe(read => read.Rows <= 3);
                db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
                db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
            }
        });
    }
}
