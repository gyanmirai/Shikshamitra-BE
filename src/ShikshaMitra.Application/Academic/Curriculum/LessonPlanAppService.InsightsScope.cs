using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

public partial class LessonPlanAppService
{
	internal async Task<IQueryable<LessonPlan>> ApplyInsightsOwnerScopeAsync(IQueryable<LessonPlan> query, bool serverResolvedAdmin, Guid[] assignedClassDivisionIds)
	{
		if (serverResolvedAdmin) return query;
		var teachers = (await GetCurrentAccessibleTeacherIdsAsync()).ToArray();
		return FilterInsightsOwners(query, teachers, assignedClassDivisionIds);
	}
	internal static IQueryable<LessonPlan> FilterInsightsOwners(IQueryable<LessonPlan> query, Guid[] accessibleTeacherIds, Guid[] assignedClassDivisionIds) =>
		query.Where(plan => Enumerable.Contains(accessibleTeacherIds, plan.TeacherId) || Enumerable.Contains(assignedClassDivisionIds, plan.DivisionId));
}
