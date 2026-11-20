using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.ExternalReceivables;

public interface IFinanceExternalRefundEvidenceQuery
{
	Task<IQueryable<FinanceExternalRefundChargeTotal>> GetQueryAsync(IQueryable<Guid> relevantChargeIds,
		CancellationToken cancellationToken = default);
}
