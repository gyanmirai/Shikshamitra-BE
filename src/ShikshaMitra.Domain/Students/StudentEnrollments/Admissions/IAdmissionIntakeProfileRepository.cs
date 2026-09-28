using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionIntakeProfileRepository : IRepository<AdmissionIntakeProfile, Guid>
{
	Task AcquireAssignmentLockAsync(Guid profileId, CancellationToken cancellationToken = default);
}
