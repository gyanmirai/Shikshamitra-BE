using System;
using ShikshaMitra.Academic.Calendar;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal static class AssignmentCalendarPolicy
{
	public static bool AppliesTo(
		AcademicCalendarDay day,
		Guid standardId,
		Guid divisionId) => AppliesTo(day.AppliesToAll, day.StandardId, day.DivisionId, standardId, divisionId);

	public static bool AppliesTo(bool appliesToAll, Guid? dayStandardId, Guid? dayDivisionId, Guid standardId, Guid divisionId) =>
		appliesToAll || (dayStandardId.HasValue && dayStandardId.Value == standardId &&
			(!dayDivisionId.HasValue || dayDivisionId.Value == divisionId));
}
