using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Calendar_Batching_Should_Preserve_Row_Validation_Error_Order(bool calendarFirst)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
                var rows = exam.ExamSubjects.Select(subject => new ExamSubjectScheduleDto
                {
                    SubjectId = subject.SubjectId, ExamDate = subject.ExamDate, StartTime = subject.StartTime,
                    EndTime = subject.EndTime, TotalMarks = 100, PassingMarks = 33
                }).ToList();
                var invalidIndex = calendarFirst ? 1 : 0;
                rows[invalidIndex].EndTime = rows[invalidIndex].StartTime;
                var blockedDate = rows[calendarFirst ? 0 : 1].ExamDate!.Value.Date;
                var calendar = new AcademicCalendar(Guid.NewGuid(), TestTenantId, mediumId, null, "Exam validation order", DateTime.Today, DateTime.Today.AddMonths(1));
                await _calendarRepository.InsertAsync(calendar, autoSave: true);
                await _calendarDayRepository.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), TestTenantId, calendar.Id, blockedDate,
                    DayType.Holiday, "Calendar order holiday"), autoSave: true);
                var error = await Should.ThrowAsync<BusinessException>(() => _examAppService.SaveScheduleAsync(exam.Id,
                    new ExamScheduleRequestDto { Subjects = rows }));
                error.Code.ShouldBe(calendarFirst ? "ShikshaMitra.Exam:ScheduleDateBlockedByCalendar" : "ShikshaMitra.Exam:InvalidScheduleTime");
                if (calendarFirst)
                {
                    error.Data["ExamDate"].ShouldBe(blockedDate.ToString("yyyy-MM-dd"));
                    error.Data["CalendarDay"].ShouldBe("Calendar order holiday");
                    error.Message.ShouldBe($"Cannot publish this exam because the scheduled date {blockedDate:dd MMM yyyy} conflicts with holiday 'Calendar order holiday' in the Academic Calendar.");
                }
            }
        });
    }

    [Fact]
    public async Task Schedule_Should_Check_All_Calendar_Dates_In_One_Read()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TestTenantId))
            {
                var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
                for (var index = 0; index < 8; index++)
                {
                    var subject = await CreateSubjectAsync(mediumId, $"Calendar batch {index}");
                    exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.Id));
                }
                await _examRepository.UpdateAsync(exam, autoSave: true);
                var rows = exam.ExamSubjects.Select((subject, index) => new ExamSubjectScheduleDto
                {
                    SubjectId = subject.SubjectId, ExamDate = DateTime.Today.AddDays(index + 30),
                    StartTime = DateTime.Today.AddDays(index + 30).AddHours(10), EndTime = DateTime.Today.AddDays(index + 30).AddHours(11),
                    TotalMarks = 100, PassingMarks = 33
                }).ToList();
                var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
                var calendarTable = db.Model.FindEntityType(typeof(AcademicCalendarDay))!.GetTableName();
                db.ChangeTracker.Clear();
                using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
                await _examAppService.SaveScheduleAsync(exam.Id, new ExamScheduleRequestDto { Subjects = rows });
                var calendarCommands = capture.Commands.Where(sql => sql.Contains($"FROM \"{calendarTable}\"", StringComparison.Ordinal)).ToList();
                calendarCommands.Count.ShouldBe(1);
                var updated = await _examRepository.GetAsync(exam.Id, includeDetails: true);
                updated.ExamSubjects.Count.ShouldBe(10);
                foreach (var row in rows) updated.ExamSubjects.Single(subject => subject.SubjectId == row.SubjectId).ExamDate.ShouldBe(row.ExamDate?.Date);
            }
        });
    }
}
