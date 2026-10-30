using System;
using System.Linq;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Mobile;

internal static class MobileHomeworkVisibility
{
	public static IQueryable<HomeworkAssignmentEntity> Homework(IQueryable<HomeworkAssignmentEntity> query,
		MobilePlacement p, Guid? tenantId, Guid[] selected, DateTime now) => query.Where(x => x.TenantId == tenantId &&
		x.StandardId == p.StandardId && (!x.AcademicYearId.HasValue || x.AcademicYearId == p.Year.Id) && Enumerable.Contains(selected, x.SubjectId) &&
		((!x.DivisionSchedules.Any() && x.DivisionId == p.DivisionId && x.IsPublished) ||
		 x.DivisionSchedules.Any(s => s.DivisionId == p.DivisionId && s.IsPublished && (!s.PublishedAt.HasValue || s.PublishedAt <= now))));

	public static IQueryable<AssignmentEntity> Assignments(IQueryable<AssignmentEntity> query,
		MobilePlacement p, Guid? tenantId, Guid[] selected, DateTime now) => query.Where(x => x.TenantId == tenantId &&
		x.StandardId == p.StandardId && (!x.AcademicYearId.HasValue || x.AcademicYearId == p.Year.Id) && Enumerable.Contains(selected, x.SubjectId) &&
		x.DivisionSchedules.Any(s => s.DivisionId == p.DivisionId && s.IsPublished && (!s.PublishedAt.HasValue || s.PublishedAt <= now)));
}
