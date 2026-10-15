using System.Linq;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Finance;

public static class FinanceFeeAccountScopeQueryBuilder
{
    public static IQueryable<FinanceFeeAccount> ApplyScope(IQueryable<FinanceFeeAccount> query, UserScopeContext scope)
    {
        if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
        if (!scope.HasAnyAccess) return query.Where(_ => false);
        return query.Where(a => scope.AcademicYearIds.Contains(a.AcademicYearId) &&
            (scope.WildcardBoardAcademicYearIds.Contains(a.AcademicYearId) ||
             scope.WildcardMediumBoardIds.Contains(a.BoardId) ||
             scope.WildcardMediumStageMediumIds.Contains(a.EducationMediumId) ||
             a.MediumStageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(a.MediumStageId.Value) ||
             scope.WildcardDivisionStandardIds.Contains(a.StandardId) ||
             a.DivisionId.HasValue && scope.DivisionIds.Contains(a.DivisionId.Value)));
    }
}
