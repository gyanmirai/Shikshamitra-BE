using System;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Reports;

public interface IHrReportingRunLock
{
	Task LockAsync(Guid runId, Guid? tenantId);
}
