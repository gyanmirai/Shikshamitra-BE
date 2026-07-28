using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal static class AssignmentScopePolicy
{
	public static bool CanAccessAggregate(
		UserScopeContext scope,
		Guid? academicYearId,
		Guid? boardId,
		Guid? mediumId,
		Guid? mediumStageId,
		Guid standardId,
		IEnumerable<Guid> divisionIds)
	{
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return true;
		}

		var divisions = divisionIds.Distinct().ToList();
		if (divisions.Count == 0)
		{
			return false;
		}

		if (divisions.All(scope.DivisionIds.Contains))
		{
			return true;
		}

		if (!academicYearId.HasValue || !boardId.HasValue || !mediumId.HasValue || !mediumStageId.HasValue)
		{
			return false;
		}

		if (!HasAcademicYearAccess(scope, academicYearId.Value))
		{
			return false;
		}

		return scope.WildcardBoardAcademicYearIds.Contains(academicYearId.Value) ||
			scope.WildcardMediumBoardIds.Contains(boardId.Value) ||
			scope.WildcardMediumStageMediumIds.Contains(mediumId.Value) ||
			scope.WildcardStandardMediumStageIds.Contains(mediumStageId.Value) ||
			scope.WildcardDivisionStandardIds.Contains(standardId);
	}

	public static bool CanAccessAggregate(UserScopeContext scope, AssignmentEntity assignment) =>
		CanAccessAggregate(
			scope,
			assignment.AcademicYearId,
			assignment.EducationBoardId,
			assignment.EducationMediumId,
			assignment.EducationMediumStageId,
			assignment.StandardId,
			assignment.DivisionSchedules.Select(schedule => schedule.DivisionId));

	public static bool CanAccessDivision(
		UserScopeContext scope,
		AssignmentEntity assignment,
		Guid divisionId)
	{
		if (scope.IsAdmin || scope.HasTenantWideScope())
		{
			return true;
		}

		if (!assignment.DivisionSchedules.Any(schedule => schedule.DivisionId == divisionId))
		{
			return false;
		}

		if (scope.DivisionIds.Contains(divisionId))
		{
			return true;
		}

		if (!assignment.AcademicYearId.HasValue || !assignment.EducationBoardId.HasValue ||
			!assignment.EducationMediumId.HasValue || !assignment.EducationMediumStageId.HasValue ||
			!HasAcademicYearAccess(scope, assignment.AcademicYearId.Value))
		{
			return false;
		}

		return scope.WildcardBoardAcademicYearIds.Contains(assignment.AcademicYearId.Value) ||
			scope.WildcardMediumBoardIds.Contains(assignment.EducationBoardId.Value) ||
			scope.WildcardMediumStageMediumIds.Contains(assignment.EducationMediumId.Value) ||
			scope.WildcardStandardMediumStageIds.Contains(assignment.EducationMediumStageId.Value) ||
			scope.WildcardDivisionStandardIds.Contains(assignment.StandardId);
	}

	private static bool HasAcademicYearAccess(UserScopeContext scope, Guid academicYearId) =>
		scope.AcademicYearIds.Contains(academicYearId) ||
		scope.WildcardBoardAcademicYearIds.Contains(academicYearId);
}
