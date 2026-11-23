using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public interface IFeeStructureRevisionLock
{
	Task AcquireAsync(Guid revisionRootId, CancellationToken cancellationToken = default);
}
