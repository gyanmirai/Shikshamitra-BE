using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;

namespace ShikshaMitra.Library.Charges;

public interface ILibraryChargePositionSourceQuery
{
	Task<IQueryable<FinanceExternalPositionSource>> GetQueryAsync(CancellationToken cancellationToken = default);
}
