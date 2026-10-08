using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HostTenants;

public class TenantActivityChecker(IHostTenantQueryRepository repository, ICurrentTenant currentTenant, IUnitOfWorkManager unitOfWorkManager) : DomainService
{
	public async Task<bool> IsActiveAsync(Guid? tenantId)
	{
		if (tenantId == null) return true;
		// Tenant resolution runs before UseUnitOfWork. A fresh host read also avoids reusing
		// a tenant-bound context or a tracked status from an earlier check in the request.
		using (currentTenant.Change(null))
		using (var unit = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
		{
			var active = await repository.IsActiveAsync(tenantId.Value);
			await unit.CompleteAsync();
			return active;
		}
	}
}
