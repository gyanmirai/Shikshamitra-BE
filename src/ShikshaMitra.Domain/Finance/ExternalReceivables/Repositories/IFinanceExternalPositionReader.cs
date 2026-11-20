using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.ExternalReceivables;

public interface IFinanceExternalPositionReader
{
	Task<IReadOnlyList<FinanceExternalSourcePosition>> ReadAsync(
		IReadOnlyList<FinanceExternalPositionSource> authorizedSources, CancellationToken cancellationToken = default);
}
