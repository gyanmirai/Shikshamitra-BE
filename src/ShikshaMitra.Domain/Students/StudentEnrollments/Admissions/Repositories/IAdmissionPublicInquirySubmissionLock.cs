using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPublicInquirySubmissionLock
{
	Task AcquireAsync(Guid? tenantId, Guid linkId);
}
