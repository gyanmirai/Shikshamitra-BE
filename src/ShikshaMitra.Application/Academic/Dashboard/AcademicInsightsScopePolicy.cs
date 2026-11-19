using System;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Academic.Dashboard;

public static class AcademicInsightsScopePolicy
{
	public static bool HasWholeMedium(UserScopeContext scope, Guid year, Guid board, Guid medium) =>
		scope.HasAcademicYearWideScope(year) || scope.WildcardMediumBoardIds.Contains(board) || scope.WildcardMediumStageMediumIds.Contains(medium);
	public static bool CanReadDivision(UserScopeContext scope, Guid year, Guid board, Guid medium, Guid stage, Guid standard, Guid division) =>
		HasWholeMedium(scope, year, board, medium) || scope.WildcardStandardMediumStageIds.Contains(stage) ||
		scope.WildcardDivisionStandardIds.Contains(standard) || scope.DivisionIds.Contains(division);
}
