using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Mobile;

public interface IMobileSubmissionLock
{
	Task AcquireAsync(Guid tenantId, Guid studentId, Guid taskId);
}
