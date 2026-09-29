using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using SchoolTimetable = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanInsightBatching_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Timeline_Insights_Should_Keep_Query_Count_Bounded_As_Periods_Grow()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = Guid.NewGuid();
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			{
				var service = GetRequiredService<ILessonPlanAppService>();
				var timetables = GetRequiredService<ITimetableRepository>();
				var date = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.FromMinutes(330));
				var teacherId = Guid.NewGuid();
				var standardId = Guid.NewGuid();
				var divisionId = Guid.NewGuid();
				var timetable = new SchoolTimetable(Guid.NewGuid(), tenantId, standardId, divisionId);
				AddSlot(1);
				timetable.Publish();
				await timetables.InsertAsync(timetable, autoSave: true);

				var calendar = new AcademicCalendar(Guid.NewGuid(), tenantId, Guid.NewGuid(), null,
					"Timeline holiday", date.Date, date.Date);
				calendar.Days.Add(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id,
					date.Date, DayType.Holiday, "Class holiday", appliesToAll: false,
					standardId: standardId, divisionId: divisionId));
				await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);

				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				int onePeriodQueryCount;
				using (var counter = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					var result = await service.GetTimelineAsync(date, teacherId);
					result.Slots.Single().InsightType.ShouldBe("Holiday");
					onePeriodQueryCount = counter.Count;
				}

				AddSlot(2);
				AddSlot(3);
				await timetables.UpdateAsync(timetable, autoSave: true);
				using (var counter = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					var result = await service.GetTimelineAsync(date, teacherId);
					result.Slots.Count.ShouldBe(3);
					result.Slots.All(slot => slot.InsightType == "Holiday").ShouldBeTrue();
					counter.Count.ShouldBe(onePeriodQueryCount);
				}

				void AddSlot(int sequence)
				{
					var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Wednesday,
						sequence, new TimeOnly(8 + sequence, 0), new TimeOnly(8 + sequence, 45), SlotType.Period);
					slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, Guid.NewGuid(), teacherId));
					timetable.AddSlot(slot);
				}
			}
		});
	}

	[Fact]
	public async Task List_Insights_Should_Keep_Query_Count_Bounded_And_Match_Each_Local_Date_And_Division()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = Guid.NewGuid();
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			{
				var service = GetRequiredService<ILessonPlanAppService>();
				var plans = GetRequiredService<IRepository<LessonPlan, Guid>>();
				var date = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.FromMinutes(330));
				var standardId = Guid.NewGuid();
				var divisionId = Guid.NewGuid();
				var first = new LessonPlan(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(),
					standardId, divisionId, Guid.NewGuid(), Guid.NewGuid(), date.UtcDateTime, "First", 25);
				await plans.InsertAsync(first, autoSave: true);
				var calendar = new AcademicCalendar(Guid.NewGuid(), tenantId, Guid.NewGuid(), null,
					"Insights", date.Date, date.Date.AddDays(2));
				calendar.Days.Add(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id,
					date.Date, DayType.Holiday, "Division holiday", appliesToAll: false,
					standardId: standardId, divisionId: divisionId));
				await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var input = new GetLessonPlansInput { MaxResultCount = 100 };
				int queryCount;
				using (var counter = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					var result = await service.GetListAsync(input);
					result.Items.Single(item => item.Id == first.Id).InsightType.ShouldBe("Holiday");
					queryCount = counter.Count;
				}

				var otherDivision = new LessonPlan(Guid.NewGuid(), tenantId, Guid.NewGuid(), first.TeacherId,
					standardId, Guid.NewGuid(), first.SubjectId, first.ChapterId, date.UtcDateTime, "Other division", 25);
				var nextDay = new LessonPlan(Guid.NewGuid(), tenantId, first.AcademicYearId, first.TeacherId,
					standardId, divisionId, first.SubjectId, first.ChapterId, date.AddDays(1).UtcDateTime, "Next day", 25);
				await plans.InsertManyAsync(new[] { otherDivision, nextDay }, autoSave: true);
				using (var counter = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					var result = await service.GetListAsync(input);
					var holidayPlan = result.Items.Single(item => item.Id == first.Id);
					holidayPlan.InsightType.ShouldBe("Holiday");
					holidayPlan.IsPlanCreationBlocked.ShouldBeTrue();
					result.Items.Single(item => item.Id == otherDivision.Id).IsPlanCreationBlocked.ShouldBeFalse();
					result.Items.Single(item => item.Id == nextDay.Id).IsPlanCreationBlocked.ShouldBeFalse();
					queryCount.ShouldBeGreaterThan(0);
					counter.Count.ShouldBe(queryCount);
				}
			}
		});
	}
}
