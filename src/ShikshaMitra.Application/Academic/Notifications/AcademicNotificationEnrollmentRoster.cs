using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationLearner(Guid StudentId, Guid DivisionId);

[RemoteService(false)]
public sealed class AcademicNotificationEnrollmentRoster(IRepository<StudentEnrollment, Guid> enrollments,
	IDataFilter filters, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyList<AcademicNotificationLearner>?> FindAsync(Guid tenantId, Guid yearId, Guid mediumId,
		Guid classId, Guid[] divisions, IQueryable<Student> eligible, CancellationToken cancellationToken)
	{
		// Revoked enrollments still supersede the legacy Student placement.
		using var softDelete = filters.Disable<ISoftDelete>();
		var history = (await enrollments.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.AcademicYearId == yearId);
		var active = history.Where(x => !x.IsDeleted && x.IsActive);
		var enrolled = from enrollment in active
			join student in eligible on enrollment.StudentId equals student.Id
			where enrollment.StandardId == classId && enrollment.DivisionId.HasValue &&
				Enumerable.Contains(divisions, enrollment.DivisionId.Value) &&
				!active.Any(other => other.StudentId == student.Id && other.Id != enrollment.Id)
			select new { StudentId = student.Id, DivisionId = enrollment.DivisionId!.Value };
		var legacy = eligible.Where(x => x.AcademicYearId == yearId && x.AcademicMediumId == mediumId &&
			x.StandardId == classId && x.DivisionId.HasValue && Enumerable.Contains(divisions, x.DivisionId.Value) &&
			!history.Any(enrollment => enrollment.StudentId == x.Id))
			.Select(x => new { StudentId = x.Id, DivisionId = x.DivisionId!.Value });
		var rows = await queries.ToListAsync(enrolled.Concat(legacy).Take(10001)
			.Select(x => new AcademicNotificationLearner(x.StudentId, x.DivisionId)), cancellationToken);
		return rows.Count <= 10000 ? rows : null;
	}
}
