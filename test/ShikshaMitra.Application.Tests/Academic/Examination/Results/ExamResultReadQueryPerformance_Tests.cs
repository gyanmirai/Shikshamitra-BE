using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Results;

public partial class ExamResultAppService_ElectiveSubjects_Tests
{
    [Fact]
    public async Task Result_Readiness_Should_Keep_Elective_Checks_Without_Student_Or_Mark_Graphs()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                await EnsureReadQueryActorAsync();
                var fixture = await CreateElectiveExamAsync(75, 50, false);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var readiness = await _resultAppService.GetReadinessAsync(fixture.Exam.Id, fixture.Division.Id);
                readiness.IsReady.ShouldBeTrue();
                readiness.Percentage.ShouldBe(100);
                readiness.RequiredSubjects.ShouldBe(2);
                readiness.CompletedSubjects.ShouldBe(2);
                readiness.MissingMarks.ShouldBe(0);
                db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
                db.ChangeTracker.Entries<ExamMark>().ShouldBeEmpty();
                capture.Commands.ShouldAllBe(sql => !sql.Contains("\"PrimaryContactNo\"", StringComparison.Ordinal) &&
                    !sql.Contains("\"Feedback\"", StringComparison.Ordinal));
            }
        });
    }

    [Theory]
    [InlineData(ExamStudentResultStatus.Pass)]
    [InlineData(ExamStudentResultStatus.Fail)]
    [InlineData(ExamStudentResultStatus.Compartment)]
    [InlineData(ExamStudentResultStatus.Absent)]
    [InlineData(ExamStudentResultStatus.Withheld)]
    public async Task Result_Header_And_Analytics_Should_Aggregate_Statuses_And_Keep_Midpoint_Rounding(ExamStudentResultStatus status)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                await EnsureReadQueryActorAsync();
                var fixture = await CreateElectiveExamAsync(75, 50, false);
                var generated = await _resultAppService.GenerateAsync(new GenerateResultRequestDto
                { ExamId = fixture.Exam.Id, DivisionId = fixture.Division.Id });
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                var table = db.Model.FindEntityType(typeof(ExamResultDetail))!.GetTableName();
                await db.Database.ExecuteSqlRawAsync($"UPDATE \"{table}\" SET \"Percentage\" = {{0}}, \"ResultStatus\" = {{1}} WHERE \"ResultHeaderId\" = {{2}} AND \"StudentId\" = {{3}}",
                    55.5, (int)status, generated.Id, fixture.ComputerScienceStudent.Id);
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var header = await _resultAppService.GetHeaderAsync(fixture.Exam.Id, fixture.Division.Id);
                var analytics = await _resultAppService.GetAnalyticsAsync(generated.Id);
                header.ShouldNotBeNull();
                var appeared = status == ExamStudentResultStatus.Absent ? 1 : 2;
                var passed = status == ExamStudentResultStatus.Pass ? 2 : 1;
                var compartment = status == ExamStudentResultStatus.Compartment ? 1 : 0;
                var failed = status == ExamStudentResultStatus.Fail ? 1 : 0;
                var absent = status == ExamStudentResultStatus.Absent ? 1 : 0;
                var passPercentage = Math.Round((double)(passed + compartment) / appeared * 100, 1);
                header.TotalStudents.ShouldBe(2);
                header.Appeared.ShouldBe(appeared); header.Passed.ShouldBe(passed); header.Compartment.ShouldBe(compartment);
                header.Failed.ShouldBe(failed); header.Absent.ShouldBe(absent);
                header.HighestPercentage.ShouldBe(75);
                header.AveragePercentage.ShouldBe(appeared == 1 ? 75 : 65.2);
                header.PassPercentage.ShouldBe(passPercentage);
                analytics.TotalStudents.ShouldBe(2); analytics.Appeared.ShouldBe(appeared);
                analytics.Passed.ShouldBe(passed); analytics.Compartment.ShouldBe(compartment);
                analytics.Failed.ShouldBe(failed); analytics.Absent.ShouldBe(absent);
                analytics.HighestPercentage.ShouldBe(75);
                analytics.LowestPercentage.ShouldBe(appeared == 1 ? 75 : 55.5);
                analytics.AveragePercentage.ShouldBe(appeared == 1 ? 75 : 65.2);
                analytics.PassPercentage.ShouldBe(passPercentage);
                analytics.GradeDistribution["B+"].ShouldBe(1);
                analytics.GradeDistribution["C+"].ShouldBe(1);
                db.ChangeTracker.Entries<ExamResultDetail>().ShouldBeEmpty();
                capture.Reads.ShouldNotBeEmpty();
                capture.Commands.ShouldAllBe(sql => !sql.Contains("\"ClassRank\"", StringComparison.Ordinal));
            }
        });
    }

    private async Task EnsureReadQueryActorAsync()
    {
        var identities = GetRequiredService<IRepository<IdentityUser, Guid>>();
        if (!await identities.AnyAsync(user => user.Id == FakeAdminUserId))
            await identities.InsertAsync(new IdentityUser(FakeAdminUserId, "result-reader", "result-reader@example.test", TestTenantId), autoSave: true);
        await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);
    }
}
