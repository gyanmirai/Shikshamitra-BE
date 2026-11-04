using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionWorkflowNameLock
{
	Task AcquireAsync(Guid? tenantId, string normalizedName);
}
