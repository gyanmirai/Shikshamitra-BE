using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable.Timetables;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomConflictService
{
	public async Task<IReadOnlyDictionary<Guid, ConflictCheckResult>> CheckWeeklyRoomSnapshotsAsync(
		IReadOnlyCollection<RoomConflictRoom> rooms, TimetableDayOfWeek day,
		TimeSpan startTime, TimeSpan endTime, Guid? standardId = null, Guid? divisionId = null)
	{
		if (rooms.Count == 0) return new Dictionary<Guid, ConflictCheckResult>();

		var occupations = (await _timetableRepository.GetRoomOccupationsAsync(
			rooms.Select(room => room.Id).ToArray(), day, TimeOnly.FromTimeSpan(startTime), TimeOnly.FromTimeSpan(endTime)))
			.Where(row => row.StandardId != standardId || row.DivisionId != divisionId).ToList();
		var timetableDivisionIds = occupations.Select(row => row.DivisionId).Distinct().ToList();
		var divisions = await ReadRoomDivisionsAsync((await _divisionRepository.GetQueryableAsync())
			.Where(row => (row.RoomNumber != null && row.RoomNumber != string.Empty) || timetableDivisionIds.Contains(row.Id)));
		var standards = await ReadRoomStandardsAsync(divisions.Select(row => row.StandardId)
			.Concat(occupations.Select(row => row.StandardId)).Distinct().ToList());
		var context = new RoomConflictReadContext(null,
			Array.Empty<AcademicCalendarDay>().ToLookup(row => row.Date),
			Array.Empty<RoomMaintenanceBlock>().ToLookup(row => row.RoomId),
			Array.Empty<RoomReservation>().ToLookup(row => row.RoomId),
			occupations.ToLookup(row => row.RoomId!.Value), rooms.ToDictionary(room => room.Id),
			divisions, standards, Array.Empty<RoomReservation>().ToLookup(row => row.TeacherId.GetValueOrDefault()));
		var results = new Dictionary<Guid, ConflictCheckResult>();
		foreach (var room in rooms)
		{
			var result = new ConflictCheckResult();
			var occupation = context.Occupations[room.Id].FirstOrDefault();
			if (occupation != null)
			{
				var message = await BuildTimetableRoomConflictMessageAsync(occupation, context);
				result.Conflicts.Add(new ConflictItem("roomAvailability", "critical", message,
					"Choose another room or a non-overlapping weekly period."));
				result.RoomAvailability = new ValidationPass(false, message);
			}
			var shift = await FindClassShiftOccupancyAsync(room.Id, startTime, endTime, standardId, divisionId, context);
			if (shift != null)
			{
				var conflict = CreateClassShiftConflict(shift);
				result.Conflicts.Add(conflict);
				result.ClassAvailability = new ValidationPass(false, conflict.Message);
			}
			result.CanSave = result.Conflicts.Count == 0;
			result.OverallSeverity = result.CanSave ? "none" : "critical";
			results[room.Id] = result;
		}
		return results;
	}

	private static ConflictItem CreateClassShiftConflict(ClassShiftOccupancy occupancy)
	{
		var shiftName = string.IsNullOrWhiteSpace(occupancy.ShiftName) ? "configured class shift" : occupancy.ShiftName;
		return new ConflictItem("classAvailability", "critical",
			$"Room is occupied by {occupancy.StandardName} Division {occupancy.DivisionName} " +
			$"from {occupancy.StartTime:hh\\:mm} to {occupancy.EndTime:hh\\:mm} based on institute setup shift '{shiftName}'.",
			"Choose another room or change the division room assignment / class shift in institute setup.");
	}
}
