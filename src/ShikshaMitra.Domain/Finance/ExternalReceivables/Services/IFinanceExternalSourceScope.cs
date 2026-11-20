using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.ExternalReceivables;

public interface IFinanceExternalSourceScope
{
	Task<IQueryable<FinanceExternalReadableSource>> GetReadableSourcesAsync(FinanceExternalSourceKind kind,
		CancellationToken cancellationToken = default);
}
