using System.Linq;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionOfferingScopeQuery
{
	public static IQueryable<AdmissionOffering> Apply(IQueryable<AdmissionOffering> offerings, IQueryable<AdmissionCycle> cycles,
		IQueryable<Division> divisions, UserScopeContext scope) => scope.IsAdmin || scope.HasTenantWideScope() ? offerings :
		offerings.Where(offering => cycles.Any(cycle => cycle.Id == offering.AdmissionCycleId && scope.AcademicYearIds.Contains(cycle.AcademicYearId)) && (
			cycles.Any(cycle => cycle.Id == offering.AdmissionCycleId && scope.WildcardBoardAcademicYearIds.Contains(cycle.AcademicYearId)) ||
			scope.WildcardMediumBoardIds.Contains(offering.BoardId) || scope.WildcardMediumStageMediumIds.Contains(offering.MediumId) ||
			scope.WildcardStandardMediumStageIds.Contains(offering.StageId) || scope.WildcardDivisionStandardIds.Contains(offering.StandardId) ||
			divisions.Any(division => division.StandardId == offering.StandardId && scope.DivisionIds.Contains(division.Id))));
}
