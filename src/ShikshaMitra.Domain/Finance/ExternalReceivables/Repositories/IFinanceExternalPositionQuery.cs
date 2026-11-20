using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.ExternalReceivables;

public interface IFinanceExternalPositionQuery
{
	Task<IQueryable<FinanceExternalPositionQueryRow>> GetQueryAsync(
		IQueryable<FinanceExternalPositionSource> authorizedSources, CancellationToken cancellationToken = default);
}
