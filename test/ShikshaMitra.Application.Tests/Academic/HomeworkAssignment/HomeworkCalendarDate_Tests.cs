using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkCalendarDate_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData("previous-day")]
	[InlineData("selected-day")]
	[InlineData("other-division")]
	[InlineData("archived-calendar")]
	[InlineData("other-medium")]
	[InlineData("future-vacation")]
	[InlineData("calendar-changed")]
	public async Task Midnight_Due_Date_Should_Use_The_School_Date_And_Applicable_Calendar(string scenario)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var calendars = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
				var days = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
				var selectedDate = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
				var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, AcademicMediumId, null,
					"Selected school calendar", selectedDate.AddMonths(-6), selectedDate.AddMonths(6));
				calendar.Publish(ActorId);
				await calendars.InsertAsync(calendar, autoSave: true);
				var markerCalendar = calendar;
				if (scenario is "archived-calendar" or "other-medium")
				{
					markerCalendar = new AcademicCalendar(Guid.NewGuid(), TenantId,
						scenario == "other-medium" ? Guid.NewGuid() : AcademicMediumId, null,
						"Unrelated calendar", calendar.StartDate, calendar.EndDate);
					if (scenario == "archived-calendar") markerCalendar.Archive();
					else markerCalendar.Publish(ActorId);
					await calendars.InsertAsync(markerCalendar, autoSave: true);
				}
				var markerDate = scenario == "previous-day" ? selectedDate.AddDays(-1)
					: scenario is "future-vacation" or "calendar-changed" ? selectedDate.AddDays(8) : selectedDate;
				await days.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), TenantId, markerCalendar.Id,
					markerDate, DayType.Vacation, "Diwali vacation", appliesToAll: false,
					standardId: StandardId, divisionId: scenario == "other-division" ? Guid.NewGuid() : DivisionId), autoSave: true);
				var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Revision");
				var due = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.FromMinutes(330));
				var input = new CreateHomeworkDto
				{
					StandardId = StandardId, SubjectId = SubjectId, HomeworkTypeId = type.Id,
					Title = "Midnight due date", Description = "Read the chapter", SubmissionType = 0,
					DivisionSchedules = new List<CreateHomeworkDivisionScheduleDto> { new()
					{
						DivisionId = DivisionId, PublishAt = due.AddMinutes(-384).UtcDateTime, DueAt = due.UtcDateTime
					} }
				};
				var service = GetRequiredService<IHomeworkAppService>();
				if (scenario == "selected-day")
				{
					var error = await Should.ThrowAsync<UserFriendlyException>(() => service.CreateAsync(AcademicMediumId, input, null));
					error.Message.ShouldContain("30/10/2026");
					error.Message.ShouldContain("Diwali vacation");
					error.Message.ShouldNotContain("29/10/2026");
				}
				else
				{
					var created = (await service.CreateAsync(AcademicMediumId, input, null)).Data;
					created.DivisionSchedules[0].DueAt.ShouldBe(due);
					if (scenario == "calendar-changed")
					{
						await days.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), TenantId, calendar.Id,
							selectedDate, DayType.Vacation, "Diwali vacation", appliesToAll: false,
							standardId: StandardId, divisionId: DivisionId), autoSave: true);
						var error = await Should.ThrowAsync<UserFriendlyException>(() => service.ChangeStatusAsync(created.Id, new HomeworkStatusUpdateDto { Status = 1 }));
						error.Message.ShouldContain("30/10/2026");
						error.Message.ShouldContain("Diwali vacation");
					}
					else await service.ChangeStatusAsync(created.Id, new HomeworkStatusUpdateDto { Status = 1 });
				}
			}
		});
	}
}
