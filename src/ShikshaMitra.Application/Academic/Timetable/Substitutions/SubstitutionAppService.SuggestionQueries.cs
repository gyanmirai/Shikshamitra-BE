using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class SubstitutionAppService
{
	private async Task<ILookup<Guid, TeacherBlockingInterval>> LoadTeacherSlotBlocksAsync(
		TimetableDayOfWeek? day, TimeOnly startTime, TimeOnly endTime, Guid[] teacherIds)
	{
		if (!day.HasValue || teacherIds.Length == 0) return Array.Empty<TeacherBlockingInterval>().ToLookup(item => item.TeacherId);
		var query = await timetableRepository.GetQueryableAsync();
		var intervals = from timetable in query
			where timetable.Status == TimetableStatus.Published
			from slot in timetable.Slots
			where slot.Day == day.Value && slot.SlotType == SlotType.Period && slot.StartTime < endTime && slot.EndTime > startTime
			from subject in slot.Subjects
			where subject.TeacherId.HasValue && Enumerable.Contains(teacherIds, subject.TeacherId.Value)
			select new TeacherBlockingInterval(subject.TeacherId!.Value, slot.StartTime, slot.EndTime);
		var rows = await AsyncExecuter.ToListAsync(intervals.Distinct());
		return rows.ToLookup(item => item.TeacherId);
	}

	private sealed record TeacherBlockingInterval(Guid TeacherId, TimeOnly StartTime, TimeOnly EndTime);
}
