using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
	[Theory]
	[InlineData("other-medium")]
	[InlineData("archived")]
	[InlineData("deleted")]
	[InlineData("outside-range")]
	[InlineData("older-published")]
	[InlineData("non-default-draft")]
	[InlineData("other-division")]
	[InlineData("cancelled")]
	[InlineData("other-tenant")]
	public async Task Schedule_Should_Ignore_Diwali_From_Unselected_Calendar_Context(string context)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
				var date = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);
				var selected = new AcademicCalendar(Guid.NewGuid(), TestTenantId, mediumId, null,
					"Displayed calendar", date.AddMonths(-1), date.AddMonths(2), isDefault: true);
				if (context != "non-default-draft") selected.Publish(FakeAdminUserId);
				await _calendarRepository.InsertAsync(selected, autoSave: true);
				for (var index = 0; index < 7; index++)
				{
					await _calendarDayRepository.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), TestTenantId,
						selected.Id, new DateTime(2026, 11, 7).AddDays(index), DayType.Vacation, "Diwali Vacations"), autoSave: true);
				}

				var holidayTenant = context == "other-tenant" ? Guid.NewGuid() : TestTenantId;
				using (_currentTenant.Change(holidayTenant))
				{
					var competing = new AcademicCalendar(Guid.NewGuid(), holidayTenant,
						context == "other-medium" ? Guid.NewGuid() : mediumId, null,
						"Unrelated calendar", date.AddMonths(-1), context == "outside-range" ? date.AddDays(-1) : date.AddMonths(2));
					if (context == "older-published")
					{
						competing.CreationTime = selected.CreationTime.AddDays(-1);
						competing.Publish(FakeAdminUserId);
					}
					if (context == "archived") competing.Archive();
					var dayCalendar = context is "other-division" or "cancelled" ? selected : competing;
					if (dayCalendar == competing) await _calendarRepository.InsertAsync(competing, autoSave: true);
					var day = new AcademicCalendarDay(Guid.NewGuid(), holidayTenant, dayCalendar.Id, date,
						DayType.Vacation, "Diwali vacation", appliesToAll: context != "other-division",
						standardId: context == "other-division" ? exam.StandardId : null,
						divisionId: context == "other-division" ? Guid.NewGuid() : null);
					if (context == "cancelled") day.Cancel("Vacation moved to November.");
					await _calendarDayRepository.InsertAsync(day, autoSave: true);
					if (context == "deleted") await _calendarRepository.DeleteAsync(competing, autoSave: true);
				}

				await _examAppService.SaveScheduleAsync(exam.Id, await CalendarScheduleAsync(exam, mediumId, date));
				var saved = await _examAppService.GetScheduleAsync(exam.Id);
				saved.Subjects.First().ExamDate.ShouldBe(date);
				await _examAppService.ChangeStatusAsync(exam.Id, ExamStatus.Published);
				(await _examRepository.GetAsync(exam.Id)).Status.ShouldBe(ExamStatus.Published);
			}
		});
	}

	[Fact]
	public async Task Schedule_And_Publish_Should_Report_The_Exact_Selected_Vacation_Day()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
				var date = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
				var calendar = new AcademicCalendar(Guid.NewGuid(), TestTenantId, mediumId, null,
					"Displayed calendar", date.AddMonths(-1), date.AddMonths(1), isDefault: true);
				calendar.Publish(FakeAdminUserId);
				await _calendarRepository.InsertAsync(calendar, autoSave: true);
				var day = new AcademicCalendarDay(Guid.NewGuid(), TestTenantId, calendar.Id, date,
					DayType.Vacation, "Diwali Vacations", appliesToAll: false, standardId: exam.StandardId,
					divisionId: exam.ExamDivisions.Single().DivisionId);
				await _calendarDayRepository.InsertAsync(day, autoSave: true);
				var input = await CalendarScheduleAsync(exam, mediumId, date);
				var saveError = await Should.ThrowAsync<BusinessException>(() => _examAppService.SaveScheduleAsync(exam.Id, input));
				AssertCalendarConflict(saveError, calendar, day);
				foreach (var row in input.Subjects)
				{
					exam.ExamSubjects.Single(subject => subject.SubjectId == row.SubjectId).UpdateSchedule(
						row.ExamDate?.DateTime, row.StartTime?.DateTime, row.EndTime?.DateTime, row.TotalMarks, row.PassingMarks, null, row.GradeScalingId);
				}
				await _examRepository.UpdateAsync(exam, autoSave: true);
				var publishError = await Should.ThrowAsync<BusinessException>(() => _examAppService.ChangeStatusAsync(exam.Id, ExamStatus.Published));
				AssertCalendarConflict(publishError, calendar, day);
			}
		});
	}

	private async Task<ExamScheduleRequestDto> CalendarScheduleAsync(Exam exam, Guid mediumId, DateTime firstDate)
	{
		var scale = await _gradingScaleRepository.InsertAsync(new GradingScale(Guid.NewGuid(), TestTenantId,
			$"Calendar grade {Guid.NewGuid():N}"[..30], "100", mediumId), autoSave: true);
		return new ExamScheduleRequestDto
		{
			Subjects = exam.ExamSubjects.Select((subject, index) => new ExamSubjectScheduleDto
			{
				SubjectId = subject.SubjectId, ExamDate = firstDate.AddDays(index),
				StartTime = firstDate.AddDays(index).AddHours(10), EndTime = firstDate.AddDays(index).AddHours(11),
				TotalMarks = 100, PassingMarks = 33, GradeScalingId = scale.Id
			}).ToList()
		};
	}

	private static void AssertCalendarConflict(BusinessException error, AcademicCalendar calendar, AcademicCalendarDay day)
	{
		error.Code.ShouldBe("ShikshaMitra.Exam:ScheduleDateBlockedByCalendar");
		error.Data["ExamDate"].ShouldBe("2026-11-07");
		error.Data["CalendarDay"].ShouldBe("Diwali Vacations");
		error.Data["AcademicCalendarId"].ShouldBe(calendar.Id);
		error.Data["AcademicCalendarDayId"].ShouldBe(day.Id);
		error.Data["CalendarDate"].ShouldBe("2026-11-07");
	}
}
