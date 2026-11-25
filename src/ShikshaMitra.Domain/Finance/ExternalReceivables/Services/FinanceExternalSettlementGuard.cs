using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalSettlementGuard(
	IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<IQueryable<Guid>> GetLinkedChargeIdsAsync() =>
		(await links.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id).Select(row => row.FinanceChargeId);

	public async Task EnsureLegacyChargesAsync(IEnumerable<Guid> chargeIds)
	{
		var ids = chargeIds.Distinct().ToArray();
		if (ids.Length > 0 && await links.AnyAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(ids, row.FinanceChargeId)))
			throw new BusinessException(LibraryFinanceErrorCodes.SettlementRequiresFinance,
				"Use the source receivable settlement workflow for this charge.");
	}
}
