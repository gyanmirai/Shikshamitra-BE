using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using Volo.Abp;

namespace ShikshaMitra.Academic.Timetable.Timetables;

internal static class TimetableWeeklyLectureRequirements
{
	private sealed record Mismatch(string Code, string Reference, Guid Id, int? Required = null, int? Assigned = null);

	internal static List<WeeklyLectureSlotSubject> Effective(IEnumerable<WeeklyLectureSlotSubject> entries,
		IReadOnlyCollection<AssignedSubject> publishedAssignments)
	{
		var core = publishedAssignments.Where(x => !x.ElectiveGroupId.HasValue).Select(x => x.SubjectId).ToHashSet();
		var electives = publishedAssignments.Where(x => x.ElectiveGroupId.HasValue).Select(x => x.ElectiveGroupId!.Value).ToHashSet();
		return entries.Where(x => x.SubjectId is Guid subject && core.Contains(subject) ||
			x.ElectiveGroupId is Guid group && electives.Contains(group)).ToList();
	}

	internal static bool Matches(Timetable timetable, IReadOnlyCollection<WeeklyLectureSlotSubject> requirements)
	{
		var periods = timetable.Slots.Where(x => x.SlotType == SlotType.Period).ToArray();
		return periods.Length != 0 && requirements.Count != 0 && FirstMismatch(periods, requirements) == null;
	}

	internal static void EnsureMatches(Timetable timetable, IReadOnlyCollection<TimetableSlot> periods,
		IReadOnlyCollection<WeeklyLectureSlotSubject> requirements)
	{
		if (FirstMismatch(periods, requirements) is not { } mismatch) return;
		var error = new BusinessException(mismatch.Code);
		if (mismatch.Required.HasValue)
		{
			error.WithData("StandardId", timetable.StandardId).WithData("DivisionId", timetable.DivisionId)
				.WithData(mismatch.Reference, mismatch.Id).WithData("RequiredWeeklyLectures", mismatch.Required.Value)
				.WithData("AssignedWeeklyLectures", mismatch.Assigned!.Value);
		}
		else error.WithData(mismatch.Reference, mismatch.Id);
		throw error;
	}

	private static Mismatch? FirstMismatch(IReadOnlyCollection<TimetableSlot> periods,
		IReadOnlyCollection<WeeklyLectureSlotSubject> requirements)
	{
		var core = periods.Where(x => !x.ElectiveGroupId.HasValue).SelectMany(x => x.Subjects)
			.GroupBy(x => x.SubjectId).ToDictionary(x => x.Key, x => x.Count());
		var electives = periods.Where(x => x.ElectiveGroupId.HasValue)
			.GroupBy(x => x.ElectiveGroupId!.Value).ToDictionary(x => x.Key, x => x.Count());
		foreach (var requirement in requirements)
		{
			var id = requirement.ElectiveGroupId ?? requirement.SubjectId;
			var assigned = id.HasValue ? (requirement.ElectiveGroupId.HasValue ? electives : core).GetValueOrDefault(id.Value) : 0;
			if (assigned != requirement.WeeklyLectures)
				return new("Academic:Timetable:WeeklyLectureMismatch", "SubjectOrElectiveGroupId", id ?? Guid.Empty,
					requirement.WeeklyLectures, assigned);
		}
		var allowed = requirements.Where(x => x.SubjectId.HasValue).Select(x => x.SubjectId!.Value).ToHashSet();
		var allowedGroups = requirements.Where(x => x.ElectiveGroupId.HasValue).Select(x => x.ElectiveGroupId!.Value).ToHashSet();
		var other = core.Keys.FirstOrDefault(x => !allowed.Contains(x));
		if (other != Guid.Empty) return new("Academic:Timetable:SubjectNotInWeeklyPlan", "SubjectId", other);
		var otherGroup = electives.Keys.FirstOrDefault(x => !allowedGroups.Contains(x));
		return otherGroup == Guid.Empty ? null : new("Academic:Timetable:ElectiveGroupNotInWeeklyPlan", "ElectiveGroupId", otherGroup);
	}
}
