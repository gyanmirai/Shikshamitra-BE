using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;
using Volo.Abp.Timing;
using SchoolTimetable = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>Propagates current teaching responsibility in the allocation update's unit of work.</summary>
public class SubjectTeacherReassignmentManager(
	ITimetableRepository timetableRepository,
	TimetableManager timetableManager,
	IRepository<LessonPlan, Guid> lessonPlanRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock) : DomainService
{
	public async Task SynchronizeAsync(IReadOnlyCollection<SubjectTeacherChange> changes)
	{
		var activeChanges = changes.Where(change => change.PrimaryTeacherId.HasValue).ToList();
		var changedSlots = new List<(SchoolTimetable Timetable, TimetableSlot Slot)>();
		foreach (var classChanges in activeChanges.GroupBy(change => change.Assignment.StandardId))
		{
			var bySubject = classChanges.ToDictionary(change => change.Assignment.SubjectId);
			var timetables = await timetableRepository.GetByStandardIdAsync(classChanges.Key);
			foreach (var timetable in timetables)
			{
				foreach (var slot in timetable.Slots)
				{
					var changed = false;
					foreach (var subject in slot.Subjects)
					{
						if (bySubject.TryGetValue(subject.SubjectId, out var change) && change.Replaces(subject.TeacherId))
						{
							subject.AssignTeacher(change.PrimaryTeacherId);
							changed = true;
						}
					}
					if (changed) changedSlots.Add((timetable, slot));
				}
			}
		}

		// Validate the final grouped assignment, including paired electives and other divisions.
		// No slot IDs, rooms, times, or publication states are replaced.
		foreach (var (timetable, slot) in changedSlots)
		{
			var teacherIds = slot.Subjects.Where(subject => subject.TeacherId.HasValue)
				.Select(subject => subject.TeacherId!.Value).ToList();
			if (teacherIds.Distinct().Count() != teacherIds.Count)
			{
				throw new BusinessException(TimetableErrorCodes.TeacherConflict)
					.WithData("message", "The new primary teacher already teaches another subject in the same timetable period.");
			}
			await timetableManager.CheckTeacherConflictAsync(timetable.StandardId, timetable.DivisionId,
				slot.Day, slot.StartTime, slot.EndTime, teacherIds);
		}

		foreach (var change in activeChanges)
		{
			await TransferUnrecordedPlansAsync(change);
		}
		foreach (var timetable in changedSlots.Select(item => item.Timetable).DistinctBy(item => item.Id))
		{
			await timetableRepository.UpdateAsync(timetable);
		}
	}

	private async Task TransferUnrecordedPlansAsync(SubjectTeacherChange change)
	{
		var todayStartUtc = GetTodayStartUtc();
		var plans = await lessonPlanRepository.GetListAsync(plan =>
			plan.StandardId == change.Assignment.StandardId && plan.SubjectId == change.Assignment.SubjectId &&
			plan.PlannedDate >= todayStartUtc && plan.Status != LessonPlanStatus.Rejected);
		var query = await lessonPlanRepository.WithDetailsAsync(plan => plan.TeacherDiary!);
		var transferableIds = plans.Where(plan => change.Replaces(plan.TeacherId)).Select(plan => plan.Id).ToList();
		var transferable = await asyncExecuter.ToListAsync(query.Where(plan =>
			transferableIds.Contains(plan.Id) && plan.TeacherDiary == null));
		foreach (var plan in transferable)
		{
			if (plans.Any(other => other.Id != plan.Id && other.TeacherId == change.PrimaryTeacherId &&
				other.PlannedDate.Date == plan.PlannedDate.Date &&
				((plan.TimetableSlotId.HasValue && other.TimetableSlotId == plan.TimetableSlotId) ||
				string.Equals(other.Topic.Trim(), plan.Topic.Trim(), StringComparison.OrdinalIgnoreCase))))
			{
				throw new BusinessException(CurriculumErrorCodes.LessonPlanReassignmentConflict)
					.WithData("message", "The new primary teacher already has a lesson plan for this date and period or topic. Resolve the duplicate before changing the allocation.");
			}
			plan.ReassignTeacher(change.PrimaryTeacherId!.Value);
			await lessonPlanRepository.UpdateAsync(plan);
		}
	}

	public async Task EnsureCurrentTimetableTeacherAsync(LessonPlan plan)
	{
		if (!plan.TimetableSlotId.HasValue || plan.PlannedDate < GetTodayStartUtc()) return;
		var timetable = await timetableRepository.FindByStandardAndDivisionAsync(plan.StandardId, plan.DivisionId);
		var subject = timetable?.Slots.FirstOrDefault(slot => slot.Id == plan.TimetableSlotId)?.Subjects
			.FirstOrDefault(item => item.SubjectId == plan.SubjectId);
		if (subject != null && subject.TeacherId != plan.TeacherId)
		{
			throw new BusinessException(CurriculumErrorCodes.TimetableTeacherChanged)
				.WithData("message", "The teacher assigned to this timetable period has changed. Refresh the timeline before creating a lesson plan.");
		}
	}

	private DateTime GetTodayStartUtc()
	{
		// Use the institution timezone, as other academic scheduling services do, so today's
		// local midnight is included even when persisted on the previous UTC calendar date.
		var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
		var localToday = TimeZoneInfo.ConvertTimeFromUtc(clock.Now.ToUniversalTime(), zone).Date;
		return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), zone);
	}
}
