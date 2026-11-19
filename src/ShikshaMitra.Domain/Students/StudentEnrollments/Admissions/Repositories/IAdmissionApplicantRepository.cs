using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionApplicantRepository : IRepository<AdmissionApplicant, Guid>
{
	/// <summary>Returns a composable, tenant-filtered pending-applicant query matching saved Father/Mother contacts.
	/// The caller must intersect it with current admission scope before materializing any results.</summary>
	Task<IQueryable<AdmissionApplicant>> GetParentContactMatchesAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> mobiles);
	/// <summary>Locks a deterministic tenant-owned identity set through the caller's transactional commit.</summary>
	Task LockForSiblingParentSelectionAsync(IReadOnlyCollection<Guid> applicantIds, CancellationToken cancellationToken = default);
}
