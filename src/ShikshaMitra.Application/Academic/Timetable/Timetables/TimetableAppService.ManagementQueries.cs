using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public partial class TimetableAppService
{
	private async Task<ILookup<(TimetableDayOfWeek, TimeOnly, TimeOnly), ManagementConflictRow>> LoadManagementConflictCandidatesAsync(
		IReadOnlyCollection<Timetable> timetables)
	{
		var slots = timetables.SelectMany(timetable => timetable.Slots).Where(slot => slot.SlotType == SlotType.Period).ToList();
		var windows = slots.Select(slot => (slot.Day, slot.StartTime, slot.EndTime)).Distinct().ToList();
		var teachers = slots.SelectMany(slot => slot.Subjects).Where(subject => subject.TeacherId.HasValue)
			.Select(subject => subject.TeacherId!.Value).Distinct().ToArray();
		var rooms = slots.SelectMany(slot => slot.Subjects).Where(subject => subject.RoomId.HasValue)
			.Select(subject => subject.RoomId!.Value).Distinct().ToArray();
		if (windows.Count == 0 || (teachers.Length == 0 && rooms.Length == 0))
			return Array.Empty<ManagementConflictRow>().ToLookup(row => (row.Day, row.StartTime, row.EndTime));
		var query = await timetableRepository.GetQueryableAsync();
		var candidates = from timetable in query
			where timetable.Status != TimetableStatus.NotCreated
			from slot in timetable.Slots
			// The legacy check ignored later duplicate slots with the same exact timing.
			where slot.Id == timetable.Slots.Where(other => other.Day == slot.Day && other.StartTime == slot.StartTime && other.EndTime == slot.EndTime)
				.OrderBy(other => other.Id).Select(other => other.Id).FirstOrDefault()
			from subject in slot.Subjects
			where (subject.TeacherId.HasValue && Enumerable.Contains(teachers, subject.TeacherId.Value)) ||
				(subject.RoomId.HasValue && Enumerable.Contains(rooms, subject.RoomId.Value))
			select new ManagementConflictRow
			{
				TimetableId = timetable.Id, StandardId = timetable.StandardId, DivisionId = timetable.DivisionId,
				Day = slot.Day, StartTime = slot.StartTime, EndTime = slot.EndTime, TeacherId = subject.TeacherId, RoomId = subject.RoomId
			};
		var parameter = Expression.Parameter(typeof(ManagementConflictRow), "candidate");
		Expression predicate = Expression.Constant(false);
		foreach (var window in windows)
		{
			var day = Expression.Equal(Expression.Property(parameter, nameof(ManagementConflictRow.Day)), Expression.Constant(window.Day));
			var start = Expression.Equal(Expression.Property(parameter, nameof(ManagementConflictRow.StartTime)), Expression.Constant(window.StartTime));
			var end = Expression.Equal(Expression.Property(parameter, nameof(ManagementConflictRow.EndTime)), Expression.Constant(window.EndTime));
			predicate = Expression.OrElse(predicate, Expression.AndAlso(day, Expression.AndAlso(start, end)));
		}
		var rows = await AsyncExecuter.ToListAsync(candidates.Where(Expression.Lambda<Func<ManagementConflictRow, bool>>(predicate, parameter)));
		return rows.ToLookup(row => (row.Day, row.StartTime, row.EndTime));
	}

	private sealed class ManagementConflictRow
	{
		public Guid TimetableId { get; init; }
		public Guid StandardId { get; init; }
		public Guid DivisionId { get; init; }
		public TimetableDayOfWeek Day { get; init; }
		public TimeOnly StartTime { get; init; }
		public TimeOnly EndTime { get; init; }
		public Guid? TeacherId { get; init; }
		public Guid? RoomId { get; init; }
	}
}
