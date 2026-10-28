using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicExamExportParity_Tests
{
    [Theory]
    [MemberData(nameof(ReportCases))]
    public async Task Exam_Preview_Should_Preserve_Pages_Without_Tracking_Whole_Exam(string reportType, bool filtered)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var (examId, studentIds, secondSubjectId) = await SeedExamAsync(filtered);
                var request = new AcademicReportRequestDto
                {
                    ReportType = reportType, PageSize = 1,
                    Filters = new()
                    {
                        ExamId = examId, StudentIds = studentIds,
                        ClassIds = filtered ? [StandardId] : [], DivisionIds = filtered ? [DivisionId] : [],
                        SubjectIds = filtered ? [secondSubjectId] : [], MinMarks = filtered ? 15 : null,
                        MaxMarks = filtered ? 140 : null, Grade = filtered ? "b" : null
                    }
                };
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                var generator = GetRequiredService<AcademicReportGenerator>();
                db.ChangeTracker.Clear();
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: true);
                foreach (var page in new[] { 1, 2, 999 })
                {
                    db.ChangeTracker.Clear(); request.Page = page;
                    var actual = await generator.GenerateAsync(request, ActorId, TenantId, exportAllRows: false);
                    actual.TotalCount.ShouldBe(expected.TotalCount);
                    actual.Columns.Select(column => (column.Id, column.Label)).ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)));
                    var expectedRows = reportType is AcademicReportTypes.ExamSummaryReport or AcademicReportTypes.ResultListPassFailOverallAnalysis
                        ? expected.Rows : expected.Rows.Skip((page - 1) * request.PageSize).Take(request.PageSize).ToList();
                    actual.Rows.Count.ShouldBe(expectedRows.Count);
                    for (var index = 0; index < expectedRows.Count; index++)
                        foreach (var cell in expectedRows[index]) actual.Rows[index][cell.Key].ShouldBe(cell.Value);
                    db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
                    db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
                }
            }
        });
    }
}
