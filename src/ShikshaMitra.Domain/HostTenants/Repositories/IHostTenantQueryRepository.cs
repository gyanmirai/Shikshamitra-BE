using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.HostTenants;

public interface IHostTenantQueryRepository
{
	Task<(long Count, List<Tenant> Items)> GetPageAsync(string? filter, bool? isActive, string? sorting, int skip, int take);
	Task<bool> IsActiveAsync(Guid id);
}
