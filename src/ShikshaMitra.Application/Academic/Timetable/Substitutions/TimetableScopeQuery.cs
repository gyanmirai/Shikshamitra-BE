using System.Linq;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

internal static class TimetableScopeQuery
{
	internal static IQueryable<Timetables.Timetable> Apply(IQueryable<Timetables.Timetable> query, UserScopeContext scope)
	{
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
		var standardIds = scope.StandardIds.ToArray();
		var divisionIds = scope.DivisionIds.ToArray();
		var wildcardDivisionStandardIds = scope.WildcardDivisionStandardIds.ToArray();
		return query.Where(timetable => Enumerable.Contains(standardIds, timetable.StandardId) ||
			Enumerable.Contains(divisionIds, timetable.DivisionId) ||
			(scope.HasWildcardDivision && Enumerable.Contains(wildcardDivisionStandardIds, timetable.StandardId)));
	}
}
