using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;
using SchoolTimetable = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanTimelineDate_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid TeacherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private readonly ILessonPlanAppService _service;
	private readonly IRepository<LessonPlan, Guid> _plans;
	private readonly ICurrentTenant _tenant;

	public LessonPlanTimelineDate_Tests()
	{
		_service = GetRequiredService<ILessonPlanAppService>();
		_plans = GetRequiredService<IRepository<LessonPlan, Guid>>();
		_tenant = GetRequiredService<ICurrentTenant>();
	}

	[Theory]
	[InlineData(2026, 9, 8, 330, 0)]
	[InlineData(2026, 9, 9, 330, 15)]
	[InlineData(2026, 9, 8, 0, 0)]
	[InlineData(2026, 9, 8, -420, 0)]
	[InlineData(2026, 10, 1, 840, 0)]
	[InlineData(2027, 1, 1, -720, 17)]
	public async Task GetTimelineAsync_Should_Return_Only_Plans_In_The_Selected_Local_Day(
		int year, int month, int day, int offsetMinutes, int requestHour)
	{
		var dayStart = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.FromMinutes(offsetMinutes));
		await WithUnitOfWorkAsync(async () =>
		{
			using (_tenant.Change(TestTenantId))
			{
				var previous = await InsertPlanAsync(dayStart.AddDays(-1).UtcDateTime, "Previous day's plan");
				var atStart = await InsertPlanAsync(dayStart.UtcDateTime, "Selected day's plan");
				var beforeEnd = await InsertPlanAsync(dayStart.AddDays(1).AddSeconds(-1).UtcDateTime, "Late plan");
				var next = await InsertPlanAsync(dayStart.AddDays(1).UtcDateTime, "Future plan");
				await InsertPlanAsync(dayStart.UtcDateTime, "Another teacher's plan", Guid.NewGuid());

				var result = await _service.GetTimelineAsync(dayStart.AddHours(requestHour), TeacherId);
				var returnedIds = result.Slots.Where(slot => slot.LessonPlan != null)
					.Select(slot => slot.LessonPlan!.Id).ToArray();
				returnedIds.ShouldBe(new[] { atStart.Id, beforeEnd.Id }, ignoreOrder: true);

				var previousDay = await _service.GetTimelineAsync(dayStart.AddDays(-1), TeacherId);
				previousDay.Slots.ShouldContain(slot => slot.LessonPlan != null && slot.LessonPlan.Id == previous.Id);
				previousDay.Slots.ShouldNotContain(slot => slot.LessonPlan != null && returnedIds.Contains(slot.LessonPlan.Id));

				var nextDay = await _service.GetTimelineAsync(dayStart.AddDays(1), TeacherId);
				nextDay.Slots.ShouldContain(slot => slot.LessonPlan != null && slot.LessonPlan.Id == next.Id);
				nextDay.Slots.ShouldNotContain(slot => slot.LessonPlan != null && returnedIds.Contains(slot.LessonPlan.Id));
			}
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GetTimelineAsync_Should_Not_Attach_A_Future_Plan_To_Todays_Timetable(bool obsoleteSlotId)
	{
		var targetDate = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.FromMinutes(330));
		await WithUnitOfWorkAsync(async () =>
		{
			using (_tenant.Change(TestTenantId))
			{
				var timetable = new SchoolTimetable(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), Guid.NewGuid());
				var subjectId = Guid.NewGuid();
				var todaySlot = AddTeachingSlot(timetable, TimetableDayOfWeek.Tuesday, subjectId);
				var targetSlot = AddTeachingSlot(timetable, TimetableDayOfWeek.Wednesday, subjectId);
				timetable.Publish();
				await GetRequiredService<ITimetableRepository>().InsertAsync(timetable, autoSave: true);
				var plan = await _plans.InsertAsync(new LessonPlan(
					Guid.NewGuid(), TestTenantId, Guid.NewGuid(), TeacherId,
					timetable.StandardId, timetable.DivisionId, subjectId, Guid.NewGuid(),
					targetDate.UtcDateTime, "Future mathematics topic", 40,
					obsoleteSlotId ? Guid.NewGuid() : targetSlot.Id), autoSave: true);

				var priorTimeline = await _service.GetTimelineAsync(targetDate.AddDays(-1), TeacherId);
				var priorSlot = priorTimeline.Slots.Single(slot => slot.TimetableSlotId == todaySlot.Id);
				priorSlot.LessonPlan.ShouldBeNull();
				priorSlot.IsTimetableSlotOnly.ShouldBeTrue();
				priorTimeline.Slots.ShouldNotContain(slot => slot.LessonPlan != null && slot.LessonPlan.Id == plan.Id);

				var targetTimeline = await _service.GetTimelineAsync(targetDate, TeacherId);
				var scheduledSlot = targetTimeline.Slots.Single(slot => slot.TimetableSlotId == targetSlot.Id);
				scheduledSlot.LessonPlan.ShouldNotBeNull();
				scheduledSlot.LessonPlan.Id.ShouldBe(plan.Id);
			}
		});
	}

	private static TimetableSlot AddTeachingSlot(SchoolTimetable timetable, TimetableDayOfWeek day, Guid subjectId)
	{
		var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, day, 4,
			new TimeOnly(8, 30), new TimeOnly(9, 0), SlotType.Period);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, subjectId, TeacherId));
		timetable.AddSlot(slot);
		return slot;
	}

	private Task<LessonPlan> InsertPlanAsync(DateTime plannedDate, string topic, Guid? teacherId = null) =>
		_plans.InsertAsync(new LessonPlan(
			Guid.NewGuid(), TestTenantId, Guid.NewGuid(), teacherId ?? TeacherId,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			plannedDate, topic, 40), autoSave: true);
}
