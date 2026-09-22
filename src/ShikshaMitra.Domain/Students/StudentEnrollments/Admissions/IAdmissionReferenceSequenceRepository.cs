using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionReferenceSequenceRepository : IRepository<AdmissionReferenceSequence, Guid>
{
	Task<long> IncrementAndGetAsync(
		int year,
		AdmissionReferenceKind kind,
		CancellationToken cancellationToken = default);
}
