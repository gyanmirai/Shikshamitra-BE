using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Students;

public static class StudentRecordScope
{
	public static bool HasAccess(Student student, UserScopeContext scope)
		=> HasAccess((IUserScopedEntity)student, scope);

	public static bool HasAccess(IUserScopedEntity placement, UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasTenantWideScope()) return true;
		if (!placement.AcademicYearId.HasValue || !scope.AcademicYearIds.Contains(placement.AcademicYearId.Value)) return false;

		// Ancestor IDs also describe narrow assignments; only their explicit wildcard grants authorize descendants.
		return scope.WildcardBoardAcademicYearIds.Contains(placement.AcademicYearId.Value) ||
			(placement.EducationBoardId.HasValue && scope.WildcardMediumBoardIds.Contains(placement.EducationBoardId.Value)) ||
			(placement.EducationMediumId.HasValue && scope.WildcardMediumStageMediumIds.Contains(placement.EducationMediumId.Value)) ||
			(placement.EducationMediumStageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(placement.EducationMediumStageId.Value)) ||
			(placement.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(placement.StandardId.Value)) ||
			(placement.DivisionId.HasValue && scope.DivisionIds.Contains(placement.DivisionId.Value));
	}
}
