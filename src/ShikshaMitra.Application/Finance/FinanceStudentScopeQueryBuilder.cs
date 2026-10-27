using System;
using System.Linq;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance;

public static class FinanceStudentScopeQueryBuilder
{
	public static IQueryable<Student> ApplyPayerScope(IQueryable<Student> query, UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
		if (!scope.HasAnyAccess) return query.Where(_ => false);
		// Ancestor IDs describe placement; only explicit wildcard grants authorize descendants.
		return query.Where(student => student.AcademicYearId.HasValue &&
			scope.AcademicYearIds.Contains(student.AcademicYearId.Value) &&
			(scope.WildcardBoardAcademicYearIds.Contains(student.AcademicYearId.Value) ||
			 student.AcademicBoardId.HasValue && scope.WildcardMediumBoardIds.Contains(student.AcademicBoardId.Value) ||
			 scope.WildcardMediumStageMediumIds.Contains(student.AcademicMediumId) ||
			 student.AcademicMediumStageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(student.AcademicMediumStageId.Value) ||
			 student.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(student.StandardId.Value) ||
			 student.DivisionId.HasValue && scope.DivisionIds.Contains(student.DivisionId.Value)));
	}

	public static IQueryable<Student> ApplyClassDivisionFilter(
		this IQueryable<Student> query,
		IFinanceClassDivisionFilter? filter)
	{
		if (filter == null)
		{
			return query;
		}

		FinanceClassDivisionFilterValidator.Normalize(filter);

		if (filter.StandardId.HasValue)
		{
			query = query.Where(s => s.StandardId == filter.StandardId.Value);
		}

		if (filter.DivisionId.HasValue)
		{
			query = query.Where(s => s.DivisionId == filter.DivisionId.Value);
		}

		return query;
	}
}
