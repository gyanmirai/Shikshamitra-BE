using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

public interface IPortalElectivePreferenceWriter
{
	Task AcquireAsync(Guid tenantId, Guid studentId, Guid clientRequestId);
	Task InsertAsync(StudentElectivePreferenceSubmission receipt, StudentElectiveSelection selection);
}
