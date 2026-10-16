using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Bounded scalar source reads; callers own tenant and academic authorization.</summary>
[RemoteService(false)]
public class StudentRollSourceReader(IStudentRepository students, IRepository<AdmissionApplication, Guid> applications,
	IRepository<StudentIdentifierAllocation, Guid> identifiers) : ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<StudentRollCandidate>> ReadAsync(Guid[] studentIds)
	{
		var people = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, s.Id))
			.Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.GrNumber, s.GrNo,
				Gender = s.AdditionalDetail == null ? null : s.AdditionalDetail.Gender }));
		var admission = await AsyncExecuter.ToListAsync((await applications.GetQueryableAsync())
			.Where(a => a.TenantId == CurrentTenant.Id && a.StudentId.HasValue && Enumerable.Contains(studentIds, a.StudentId.Value) && a.FinalizedAt.HasValue)
			.Select(a => new { a.StudentId, a.FinalizedAt }));
		var allocated = await AsyncExecuter.ToListAsync((await identifiers.GetQueryableAsync())
			.Where(a => a.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, a.StudentId) && a.IdentifierType == StudentIdentifierType.AdmissionNumber && a.Status != StudentIdentifierAllocationStatus.Released)
			.Select(a => new { a.StudentId, a.Value, a.AllocatedAt }));
		return people.Select(s => new StudentRollCandidate(s.Id, s.FirstName, s.MiddleName, s.LastName, s.Gender,
			s.GrNumber ?? s.GrNo?.ToString(CultureInfo.InvariantCulture),
			allocated.Where(a => a.StudentId == s.Id).OrderBy(a => a.AllocatedAt).Select(a => a.Value).FirstOrDefault(),
			admission.Where(a => a.StudentId == s.Id).Min(a => a.FinalizedAt))).ToList();
	}
}
