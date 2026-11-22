using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPublicInquiryLinkNameLock
{
	Task AcquireAsync(Guid? tenantId, string normalizedName);
}
