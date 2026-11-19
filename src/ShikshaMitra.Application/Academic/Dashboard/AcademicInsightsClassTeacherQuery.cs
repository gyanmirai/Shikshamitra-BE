using System.Linq;
using ShikshaMitra.Academic.ClassTeacher;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Dashboard;

internal static class AcademicInsightsClassTeacherQuery
{
	internal static IQueryable<ClassTeacherEntity> ForContext(IQueryable<ClassTeacherEntity> query, AcademicInsightsContext context)
	{
		var divisions = context.DivisionIds; var standards = context.StandardIds;
		return query.Where(assignment => assignment.TenantId == context.TenantId &&
			assignment.AcademicMediumId == context.Input.EducationMediumId && assignment.Status == ClassTeacherAssignmentStatus.Active &&
			(!assignment.AcademicYearId.HasValue || assignment.AcademicYearId == context.Input.AcademicYearId) &&
			Enumerable.Contains(divisions, assignment.DivisionId) && Enumerable.Contains(standards, assignment.StandardId));
	}
}
