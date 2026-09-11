using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Students;

public static class StudentRecordScope
{
	public static bool HasAccess(Student student, UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasTenantWideScope()) return true;
		if (!student.AcademicYearId.HasValue || !scope.AcademicYearIds.Contains(student.AcademicYearId.Value)) return false;

		// Ancestor IDs also describe narrow assignments; only their explicit wildcard grants authorize descendants.
		return scope.WildcardBoardAcademicYearIds.Contains(student.AcademicYearId.Value) ||
			(student.AcademicBoardId.HasValue && scope.WildcardMediumBoardIds.Contains(student.AcademicBoardId.Value)) ||
			scope.WildcardMediumStageMediumIds.Contains(student.AcademicMediumId) ||
			(student.AcademicMediumStageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(student.AcademicMediumStageId.Value)) ||
			(student.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(student.StandardId.Value)) ||
			(student.DivisionId.HasValue && scope.DivisionIds.Contains(student.DivisionId.Value));
	}
}
