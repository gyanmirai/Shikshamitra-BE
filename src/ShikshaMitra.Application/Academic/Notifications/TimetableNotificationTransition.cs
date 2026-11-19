using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Data;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record TimetableNotificationEntry(TimetableDayOfWeek Day, int Period, TimeOnly Start, TimeOnly End,
	SlotType Type, BreakType? BreakType, SubjectType? SubjectType, Guid? GroupId, Guid? SubjectId, Guid? TeacherId, Guid? RoomId);

internal sealed record TimetableNotificationChoice(Guid GroupId, Guid SubjectId);
internal sealed record TimetableNotificationElectives(Guid[] Groups, TimetableNotificationChoice[] Choices);

internal sealed record TimetableNotificationTransition(string EventKey, string Revision, string ScheduleHash,
	bool ClassWide, TimetableNotificationChoice[] AffectedChoices, Guid[] Teachers, TimetableNotificationElectives VisibleElectives,
	TimetableNotificationElectives? PreviousElectives)
{
	private const string SavedTransition = "AcademicTimetableNotificationTransition";

	internal static TimetableNotificationEntry[] Schedule(TimetableEntity timetable, IReadOnlyDictionary<Guid, Guid?>? previousTeachers = null) => timetable.Slots
		.SelectMany(slot => slot.SlotType != SlotType.Period || slot.Subjects.Count == 0
			? [Entry(slot, null, null)]
			: slot.Subjects.Select(subject => Entry(slot, subject,
				previousTeachers != null && previousTeachers.TryGetValue(subject.Id, out var previous) ? previous : subject.TeacherId))).ToArray();

	internal static TimetableNotificationTransition? Record(TimetableEntity timetable, TimetableStatus beforeStatus, TimetableNotificationEntry[] before)
	{
		var wasPublished = beforeStatus == TimetableStatus.Published;
		var isPublished = timetable.Status == TimetableStatus.Published;
		if (!wasPublished && !isPublished) return null;

		var after = Schedule(timetable);
		var fingerprint = Fingerprint(after);
		if (wasPublished && isPublished && fingerprint == Fingerprint(before)) return null;
		var visible = isPublished ? after : before;
		var affected = wasPublished && isPublished ? before.Except(after).Concat(after.Except(before)).ToArray() : visible;
		var eventKey = (wasPublished, isPublished) switch
		{
			(false, true) => AcademicNotificationEventKeys.TimetablePublished,
			(true, true) => AcademicNotificationEventKeys.TimetableChanged,
			_ => AcademicNotificationEventKeys.TimetableWithdrawn
		};
		var result = new TimetableNotificationTransition(eventKey, Guid.NewGuid().ToString("N"), fingerprint,
			affected.Any(x => !x.GroupId.HasValue),
			affected.Where(x => x.GroupId.HasValue && x.SubjectId.HasValue)
				.Select(x => new TimetableNotificationChoice(x.GroupId!.Value, x.SubjectId!.Value)).Distinct().ToArray(),
			affected.Where(x => x.TeacherId.HasValue).Select(x => x.TeacherId!.Value).Distinct().ToArray(),
			Electives(visible), wasPublished && isPublished ? Electives(before) : null);
		timetable.SetProperty(SavedTransition, JsonSerializer.Serialize(result));
		return result;
	}

	private static TimetableNotificationElectives Electives(IEnumerable<TimetableNotificationEntry> schedule)
	{
		var groups = schedule.Where(x => x.GroupId.HasValue).GroupBy(x => x.GroupId!.Value).ToArray();
		var allowed = groups.SelectMany(group =>
		{
			var periods = group.GroupBy(x => (x.Day, x.Period, x.Start, x.End)).ToArray();
			return group.Where(x => x.SubjectId.HasValue).Select(x => x.SubjectId!.Value).Distinct()
				.Where(subjectId => periods.All(period => period.Count(x => x.SubjectId == subjectId) == 1))
				.Select(subjectId => new TimetableNotificationChoice(group.Key, subjectId));
		}).ToArray();
		return new(groups.Select(x => x.Key).ToArray(), allowed);
	}

	internal static TimetableNotificationTransition? Read(TimetableEntity timetable)
	{
		var json = timetable.GetProperty<string>(SavedTransition);
		if (string.IsNullOrWhiteSpace(json)) return null;
		try { return JsonSerializer.Deserialize<TimetableNotificationTransition>(json); }
		catch (JsonException) { return null; }
	}

	internal static string Fingerprint(IEnumerable<TimetableNotificationEntry> schedule) =>
		Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
			schedule.Select(x => JsonSerializer.Serialize(x)).Order(StringComparer.Ordinal).ToArray())));

	private static TimetableNotificationEntry Entry(TimetableSlot slot, TimetableSlotSubject? subject, Guid? teacherId) =>
		new(slot.Day, slot.Sequence, slot.StartTime, slot.EndTime, slot.SlotType, slot.BreakType,
			slot.SubjectType, slot.ElectiveGroupId, subject?.SubjectId, teacherId, subject?.RoomId);
}
