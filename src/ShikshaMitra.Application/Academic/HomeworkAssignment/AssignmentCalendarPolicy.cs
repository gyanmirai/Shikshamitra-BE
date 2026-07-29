using System;
using ShikshaMitra.Academic.Calendar;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal static class AssignmentCalendarPolicy
{
	public static bool AppliesTo(
		AcademicCalendarDay day,
		Guid standardId,
		Guid divisionId) =>
		day.AppliesToAll ||
		(day.StandardId.HasValue && day.StandardId.Value == standardId &&
		 (!day.DivisionId.HasValue || day.DivisionId.Value == divisionId));
}
