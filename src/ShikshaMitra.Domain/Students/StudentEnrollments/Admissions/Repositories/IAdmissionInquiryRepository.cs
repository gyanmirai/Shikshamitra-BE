using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionInquiryRepository : IRepository<AdmissionInquiry, Guid>
{
	/// <summary>Locks and refreshes the persisted inquiry for an atomic application handoff.</summary>
	Task<AdmissionInquiry?> FindForApplicationStartAsync(Guid inquiryId, CancellationToken cancellationToken = default);
}
