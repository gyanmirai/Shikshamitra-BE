using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionWorkflowPublicationLock
{
	Task AcquireAsync(Guid? tenantId, string workflowCode);
}
