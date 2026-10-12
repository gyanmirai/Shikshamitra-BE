using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Mobile;

public interface IMobileLeaveRequestLock
{
    Task AcquireAsync(Guid tenantId, Guid studentId);
}
