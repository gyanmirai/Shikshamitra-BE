using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public class AcademicExamExportParity_Tests : SubjectTeacherReassignmentTestBase
{
    public static IEnumerable<object[]> ReportCases()
    {
        string[] types =
        [
            AcademicReportTypes.GenderWiseGradeAnalysis, AcademicReportTypes.ResultStatisticsReport,
            AcademicReportTypes.ResultList, AcademicReportTypes.GradewiseResultList, AcademicReportTypes.MarksRangeResultList,
            AcademicReportTypes.SubjectwiseMarksReport, AcademicReportTypes.DetailedResultSheet, AcademicReportTypes.ResultWithAllSubjectList,
            AcademicReportTypes.ExamSummaryReport, AcademicReportTypes.AverageSubjectMarkAnalysis, AcademicReportTypes.SubjectAverageClassMarkAnalysis,
            AcademicReportTypes.StudentGradeAnalysisSubjectWise, AcademicReportTypes.SubjectWiseGradeList,
            AcademicReportTypes.ResultListPassFailAnalysisSubjectWise, AcademicReportTypes.ResultListSubjectGradeAnalysis,
            AcademicReportTypes.ResultListWithSubject, AcademicReportTypes.ResultListWithSubjectFailedDetails,
            AcademicReportTypes.ResultListWithSubjectGrade, AcademicReportTypes.ResultListWithSubjectSmall, AcademicReportTypes.ResultListWithSubjectWithGrace,
            AcademicReportTypes.StudentAllBatchResult, AcademicReportTypes.StudentAllExamResult, AcademicReportTypes.BrightStudentAnalysis,
            AcademicReportTypes.BrightStudentListSubjectWise, AcademicReportTypes.WeakStudentAnalysis, AcademicReportTypes.WeakStudentAnalysisSubjectWise,
            AcademicReportTypes.StudentGradeAnalysis, AcademicReportTypes.ResultListPassFailOverallAnalysis, AcademicReportTypes.ResultListOverallGradeAnalysis
        ];
        foreach (var type in types)
        {
            yield return [type, false];
            yield return [type, true];
        }
    }

    [Theory]
    [MemberData(nameof(ReportCases))]
    public async Task Export_Should_Preserve_All_Exam_Report_Rows_And_Columns_Across_Student_Batches(string reportType, bool filtered)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var (examId, studentIds, secondSubjectId) = await SeedExamAsync(filtered);
                var request = new AcademicReportRequestDto
                {
                    ReportType = reportType, Page = 2, PageSize = 1,
                    Filters = new AcademicReportFilterDto
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
                db.ChangeTracker.Clear();
                using var queries = new AcademicQueryCounter(db.Database.GetDbConnection());
                var export = await generator.GenerateExportAsync(request, ActorId, TenantId, batchSize: filtered ? 2 : 1);
                var actual = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows) actual.Add(row);
                export.Metadata.Columns.Select(column => (column.Id, column.Label))
                    .ShouldBe(expected.Columns.Select(column => (column.Id, column.Label)), reportType);
                actual.Count.ShouldBe(expected.Rows.Count, reportType);
                for (var index = 0; index < actual.Count; index++)
                    actual[index].OrderBy(pair => pair.Key).ShouldBe(expected.Rows[index].OrderBy(pair => pair.Key), $"{reportType}, row {index}");
                if (reportType is AcademicReportTypes.DetailedResultSheet or AcademicReportTypes.ResultWithAllSubjectList)
                {
                    var profile = actual.Single(row => row["StudentId"] == studentIds[0].ToString());
                    profile["FatherName"].ShouldBe("Export Father");
                    profile["MotherName"].ShouldBe("Export Mother");
                    profile["ContactNo"].ShouldBe("9000000000");
                    profile["Email"].ShouldBe("export@example.test");
                    profile["Address"].ShouldBe("First address, Second address, Pune, 411001");
                    profile["DateOfBirth"].ShouldBe(new DateTimeOffset(2012, 4, 3, 0, 0, 0, TimeSpan.Zero).ToString("dd MMM yyyy"));
                    profile["Session"].ShouldBe("Export year");
                }
                queries.Count.ShouldBeGreaterThan(0);
                // Export batching must not retain every participant graph in the request's unit of work.
                db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
                db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
            }
        });
    }

    private async Task<(Guid ExamId, List<Guid> StudentIds, Guid SecondSubjectId)> SeedExamAsync(bool customScale)
    {
        await SeedAllocationAsync();
        await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(YearId, TenantId, "Export year",
            new DateTime(2026, 6, 1), new DateTime(2027, 5, 31)), autoSave: true);
        var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
        var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
        var second = new Subject(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), TenantId, "Álgebra", "#112233", AcademicMediumId);
        await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(second, autoSave: true);
        await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(new AssignedSubject(Guid.NewGuid(), TenantId, StandardId, second.Id, "ALG"), autoSave: true);
        var names = new[] { "Zoe", "Ada", "Émile", "Álvaro", "Bela" };
        int?[] rolls = [10, 2, 1, null, 11];
        var studentIds = new List<Guid>();
        for (var index = 0; index < names.Length; index++)
        {
            var studentId = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");
            studentIds.Add(studentId);
            var student = new Student(studentId, TenantId, names[index], null, "Student", YearId, medium.EducationBoardId,
                AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId, rollNo: rolls[index]);
            await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
            if (index < 3)
                await GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(new StudentAdditionalDetail(student.Id, TenantId,
                    new DateTimeOffset(2012, 4, 3, 0, 0, 0, TimeSpan.Zero), index == 0 ? GenderEnum.Male : GenderEnum.Female, null, $"EXPORT-{index}", null, null), autoSave: true);
            if (index == 0)
            {
                await GetRequiredService<IRepository<StudentContactDetail, Guid>>().InsertAsync(new StudentContactDetail(student.Id, TenantId,
                    "9000000000", email: "export@example.test", address1: "First address", address2: "Second address", city: "Pune", pincode: "411001"), autoSave: true);
                var family = new StudentFamilyDetail(student.Id, TenantId);
                family.UpdateFatherDetails(new ParentGuardianDetails("Export Father", null, null, null, null, null, null, null, null, null, null, null, null));
                family.UpdateMotherDetails(new ParentGuardianDetails("Export Mother", null, null, null, null, null, null, null, null, null, null, null, null));
                await GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().InsertAsync(family, autoSave: true);
            }
        }
        Guid? scaleId = null;
        if (customScale)
        {
            var scale = new GradingScale(Guid.NewGuid(), TenantId, "Export grade scale", "150", AcademicMediumId);
            scale.GradeRanges.Add(new GradeRange(Guid.NewGuid(), scale.Id, 40, 75, "B"));
            scale.GradeRanges.Add(new GradeRange(Guid.NewGuid(), scale.Id, 76, 100, "Distinction"));
            await GetRequiredService<IRepository<GradingScale, Guid>>().InsertAsync(scale, autoSave: true);
            scaleId = scale.Id;
        }
        var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), "September Exam", StandardId, TenantId, scaleId);
        exam.AddDivision(Guid.NewGuid(), DivisionId);
        exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, SubjectId, new DateTime(2026, 9, 3), null, null, 100, 35));
        exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, second.Id, new DateTime(2026, 9, 1), null, null, 50, 20));
        await GetRequiredService<IRepository<Exam, Guid>>().InsertAsync(exam, autoSave: true);
        double?[] firstMarks = [92.5, 40, null, 10, 76];
        double?[] secondMarks = [49, 20.5, null, 15, null];
        var marks = new List<ExamMark>();
        for (var index = 0; index < studentIds.Count; index++)
        {
            var division = index == 4 ? Guid.NewGuid() : DivisionId;
            marks.Add(new ExamMark(Guid.NewGuid(), exam.Id, studentIds[index], SubjectId, division, firstMarks[index], index == 2, tenantId: TenantId));
            marks.Add(new ExamMark(Guid.NewGuid(), exam.Id, studentIds[index], second.Id, division, secondMarks[index], index == 2 || index == 4, tenantId: TenantId));
        }
        await GetRequiredService<IRepository<ExamMark, Guid>>().InsertManyAsync(marks, autoSave: true);
        return (exam.Id, studentIds, second.Id);
    }

    [Theory]
    [InlineData(AcademicReportTypes.StudentAllBatchResult)]
    [InlineData(AcademicReportTypes.StudentAllExamResult)]
    [InlineData(AcademicReportTypes.ResultListOverallGradeAnalysis)]
    [InlineData(AcademicReportTypes.StudentGradeAnalysisSubjectWise)]
    [InlineData(AcademicReportTypes.BrightStudentListSubjectWise)]
    [InlineData(AcademicReportTypes.ResultListWithSubjectGrade)]
    [InlineData(AcademicReportTypes.WeakStudentAnalysis)]
    [InlineData(AcademicReportTypes.GradewiseResultList)]
    [InlineData(AcademicReportTypes.BrightStudentAnalysis)]
    public async Task Ranked_Exports_Should_Preserve_Ties_And_Separate_Same_Name_Subject_Groups(string reportType)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var (examId, students, secondId) = await SeedExamAsync(false);
                var subjects = GetRequiredService<IRepository<Subject, Guid>>();
                var second = await subjects.GetAsync(secondId);
                second.SetName((await subjects.GetAsync(SubjectId)).Name);
                await subjects.UpdateAsync(second, autoSave: true);
                var repository = GetRequiredService<IRepository<ExamMark, Guid>>();
                var marks = await repository.GetListAsync(mark => mark.ExamId == examId);
                foreach (var mark in marks.Where(mark => mark.StudentId == students[0] || mark.StudentId == students[1]))
                {
                    mark.Update(mark.StudentId == students[0] ? 40 : mark.SubjectId == SubjectId ? 80 : 0, false, null);
                    await repository.UpdateAsync(mark, autoSave: true);
                }
                // Mix an absent zero with a present zero, whose rank ranges depend on tie order.
                foreach (var mark in marks.Where(mark => mark.StudentId == students[3]))
                { mark.Update(0, false, null); await repository.UpdateAsync(mark, autoSave: true); }
                var request = new AcademicReportRequestDto { ReportType = reportType,
                    Filters = new() { ExamId = examId, StudentIds = students } };
                var generator = GetRequiredService<AcademicReportGenerator>();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, true);
                db.ChangeTracker.Clear();
                var export = await generator.GenerateExportAsync(request, ActorId, TenantId, batchSize: 1);
                var actual = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows) actual.Add(row);
                actual.Count.ShouldBe(expected.Rows.Count);
                for (var index = 0; index < actual.Count; index++)
                    actual[index].OrderBy(pair => pair.Key).ShouldBe(expected.Rows[index].OrderBy(pair => pair.Key), $"{reportType}, row {index}");
                db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
            }
        });
    }

    [Fact]
    public async Task Pivot_Export_Should_Keep_Locked_Elective_Applicability_And_Not_Track_Selections_Across_Batches()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantId))
            using (AsAdmin())
            {
                var (examId, students, secondId) = await SeedExamAsync(false);
                var elective = new ElectiveGroup(Guid.NewGuid(), TenantId, "Export elective", "#112233", AcademicMediumId);
                elective.AddSubject(SubjectId); elective.AddSubject(secondId);
                await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(elective, autoSave: true);
                var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
                foreach (var assignment in await assignments.GetListAsync(item => item.StandardId == StandardId))
                {
                    assignment.ElectiveGroupId = elective.Id;
                    assignment.SubjectType = SubjectType.Elective;
                    await assignments.UpdateAsync(assignment, autoSave: true);
                }
                var selections = GetRequiredService<IRepository<StudentElectiveSelection, Guid>>();
                for (var index = 0; index < 3; index++)
                {
                    var selection = new StudentElectiveSelection(Guid.NewGuid(), TenantId, students[index], elective.Id,
                        index == 1 ? secondId : SubjectId, AcademicMediumId);
                    if (index < 2) selection.LockSelection();
                    await selections.InsertAsync(selection, autoSave: true);
                }
                using (Tenant.Change(Guid.NewGuid()))
                {
                    var selection = new StudentElectiveSelection(Guid.NewGuid(), Tenant.Id, students[3], elective.Id, secondId, AcademicMediumId);
                    selection.LockSelection();
                    await selections.InsertAsync(selection, autoSave: true);
                }
                var request = new AcademicReportRequestDto { ReportType = AcademicReportTypes.DetailedResultSheet,
                    Filters = new() { ExamId = examId } };
                var generator = GetRequiredService<AcademicReportGenerator>();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var expected = await generator.GenerateAsync(request, ActorId, TenantId, true);
                db.ChangeTracker.Clear();
                var export = await generator.GenerateExportAsync(request, ActorId, TenantId, batchSize: 1);
                var actual = new List<Dictionary<string, string?>>();
                await foreach (var row in export.Rows) actual.Add(row);
                actual.Count.ShouldBe(expected.Rows.Count);
                for (var index = 0; index < actual.Count; index++)
                    actual[index].OrderBy(pair => pair.Key).ShouldBe(expected.Rows[index].OrderBy(pair => pair.Key), $"Elective row {index}");
                actual.Single(row => row["StudentId"] == students[0].ToString())[$"subj_{secondId}"].ShouldBe("—");
                actual.Single(row => row["StudentId"] == students[1].ToString())["TotalMaxMarks"].ShouldBe("50.00");
                actual.Single(row => row["StudentId"] == students[2].ToString())["TotalMaxMarks"].ShouldBe("150.00");
                actual.Single(row => row["StudentId"] == students[3].ToString())["TotalMaxMarks"].ShouldBe("150.00");
                db.ChangeTracker.Entries<StudentElectiveSelection>().ShouldBeEmpty();
                db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
                db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
            }
        });
    }
}
