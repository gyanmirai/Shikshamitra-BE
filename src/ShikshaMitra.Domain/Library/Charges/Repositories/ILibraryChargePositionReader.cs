using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;

namespace ShikshaMitra.Library.Charges;

public interface ILibraryChargePositionReader
{
	Task<IQueryable<Guid>> GetReadableSourceIdsAsync(
		LibraryChargePositionReadInput input, CancellationToken cancellationToken = default);
	Task<IQueryable<FinanceExternalPositionQueryRow>> GetQueryAsync(
		LibraryChargePositionReadInput input, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<FinanceExternalSourcePosition>> ReadAsync(
		LibraryChargePositionReadInput input, CancellationToken cancellationToken = default);
}
