using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
    [Theory]
    [InlineData(1234567, -1)]
    [InlineData(1234567, 0)]
    [InlineData(1234567, 1)]
    [InlineData(9999999, -1)]
    [InlineData(9999999, 0)]
    [InlineData(9999999, 1)]
    public async Task Exam_List_Should_Keep_Exact_Fractional_End_Boundaries(int fractionalTicks, int filterDeltaTicks)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Published);
                var date = DateTime.Today.AddDays(2);
                var end = date.AddHours(11).AddTicks(fractionalTicks);
                foreach (var subject in exam.ExamSubjects)
                    subject.UpdateSchedule(date, date.AddHours(10), end, 100, 33, null);
                await _examRepository.UpdateAsync(exam, autoSave: true);
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                var result = await _examAppService.GetListAsync(new GetExamsInput
                {
                    AcademicMediumId = mediumId, Status = ExamStatus.Published,
                    StartDate = new DateTimeOffset(end.AddTicks(filterDeltaTicks)),
                    EndDate = new DateTimeOffset(date.AddDays(1)), MaxResultCount = 2
                });
                result.TotalCount.ShouldBe(filterDeltaTicks <= 0 ? 1 : 0);
                result.PublishedCount.ShouldBe(filterDeltaTicks <= 0 ? 1 : 0);
                result.Items.Count.ShouldBe((int)result.TotalCount);
                if (result.Items.Count > 0) result.Items.Single().ExamId.ShouldBe(exam.Id);
            }
        });
    }

    [Fact]
    public async Task Exam_List_Should_Filter_Count_And_Page_Before_Reading_Subject_Schedules()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var (template, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
                var exams = new List<Exam>();
                for (var index = 0; index < 60; index++)
                {
                    var exam = new Exam(Guid.NewGuid(), template.ExamTypeId, $"Bounded list {index}", template.StandardId, TestTenantId);
                    exam.ChangeStatus(index % 2 == 0 ? ExamStatus.Published : ExamStatus.Pending);
                    typeof(Exam).GetProperty(nameof(Exam.CreationTime))!.SetValue(exam, DateTime.UtcNow.AddMinutes(index));
                    foreach (var division in template.ExamDivisions) exam.AddDivision(Guid.NewGuid(), division.DivisionId);
                    foreach (var scheduled in template.ExamSubjects)
                    {
                        var subject = new ExamSubject(Guid.NewGuid(), exam.Id, scheduled.SubjectId);
                        var date = DateTime.Today.AddDays(index < 50 ? 100 : 2);
                        subject.UpdateSchedule(date, date.AddHours(10), date.AddHours(11), 100, 33, new string('n', 900));
                        exam.AddSubject(subject);
                    }
                    exams.Add(exam);
                }
                await _examRepository.InsertManyAsync(exams, autoSave: true);
                var expected = exams.Skip(50).Where(exam => exam.Status == ExamStatus.Published).Skip(1).Take(2).Select(exam => exam.Id).ToList();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                var result = await _examAppService.GetListAsync(new GetExamsInput
                {
                    AcademicMediumId = mediumId, StartDate = DateTime.Today.AddDays(2), EndDate = DateTime.Today.AddDays(2).AddHours(11),
                    Status = ExamStatus.Published, SkipCount = 1, MaxResultCount = 2
                });
                result.TotalCount.ShouldBe(5);
                result.PendingCount.ShouldBe(6);
                result.PublishedCount.ShouldBe(5);
                result.Items.Select(item => item.ExamId).ShouldBe(expected);
                capture.Reads.ShouldNotBeEmpty();
                var scheduleReads = capture.Reads.Where(read => read.Sql.Contains("\"ExamDate\"", StringComparison.Ordinal)).ToList();
                scheduleReads.ShouldNotBeEmpty();
                scheduleReads.ShouldAllBe(read => read.Rows <= 2 * template.ExamSubjects.Count + 1);
            }
        });
    }
}
