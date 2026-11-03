using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.Students.Attendance;

public partial class AttendanceReportService
{
	internal async Task<AttendanceReportBatch> BuildManyAsync(Guid yearId, DateTime start, DateTime end,
		IReadOnlyCollection<AttendanceReportSliceRequest> requests, bool includeDaily)
	{
		var allowed = new List<AttendanceReportSliceRequest>();
		var partial = new HashSet<Guid>();
		foreach (var request in requests)
		{
			try
			{
				await scope.EnsureCanAccessAttendanceScopeAsync(yearId, request.StandardId, request.DivisionId, "attendance report");
				allowed.Add(request);
			}
			catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied)
			{
				// Register/dashboard have always retained accessible historical slices while
				// marking enrollments with another denied path as provisional.
				partial.UnionWith(request.Members.Select(member => member.Id));
			}
		}
		return new(allowed.Count == 0 ? [] : await BuildAuthorizedAsync(yearId, start, end, allowed, includeDaily), partial);
	}

	private static IQueryable<AttendanceDay> FilterPaths(IQueryable<AttendanceDay> query,
		IReadOnlyCollection<AttendanceReportSliceRequest> requests)
	{
		// Keep exact authorized class/division pairs in SQL. Independent IN lists can
		// accidentally read a different historical combination of these dimensions.
		var day = Expression.Parameter(typeof(AttendanceDay), "day");
		var paths = requests.Select(request => (request.StandardId, request.DivisionId)).Distinct();
		Expression predicate = Expression.Constant(false);
		foreach (var path in paths)
		{
			var match = Expression.AndAlso(
				Expression.Equal(Expression.Property(day, nameof(AttendanceDay.StandardId)), Expression.Constant(path.StandardId)),
				Expression.Equal(Expression.Property(day, nameof(AttendanceDay.DivisionId)), Expression.Constant(path.DivisionId)));
			predicate = Expression.OrElse(predicate, match);
		}
		return query.Where(Expression.Lambda<Func<AttendanceDay, bool>>(predicate, day));
	}
}

internal sealed record AttendanceReportSliceRequest(Guid StandardId, Guid DivisionId, IReadOnlyCollection<StudentEnrollment> Members);
internal sealed record AttendanceReportSlice(Guid StandardId, Guid DivisionId, List<AttendanceSummaryDto> Rows);
internal sealed record AttendanceReportBatch(List<AttendanceReportSlice> Slices, HashSet<Guid> PartialEnrollmentIds);
