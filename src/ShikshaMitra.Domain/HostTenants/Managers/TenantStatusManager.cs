using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.HostTenants;

public class TenantStatusManager(IRepository<TenantStatusChange, System.Guid> history, ITenantRepository tenants) : DomainService
{
	public async Task ChangeAsync(Tenant tenant, bool isActive, string? reason, string concurrencyStamp)
	{
		if (tenant.ConcurrencyStamp != concurrencyStamp) throw new AbpDbConcurrencyException();
		var previous = tenant.GetProperty(HostTenantConsts.IsActiveProperty, true);
		if (previous == isActive) return;
		var change = new TenantStatusChange(GuidGenerator.Create(), tenant.Id, previous, isActive, reason);
		tenant.SetProperty(HostTenantConsts.IsActiveProperty, isActive);
		await tenants.UpdateAsync(tenant);
		await history.InsertAsync(change);
	}
}
