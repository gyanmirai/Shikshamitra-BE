using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HostTenants;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.EntityFrameworkCore.HostTenants;

public class EfCoreHostTenantQueryRepository(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IHostTenantQueryRepository, ITransientDependency
{
	public async Task<bool> IsActiveAsync(Guid id) => await (await provider.GetDbContextAsync()).Tenants.AsNoTracking()
		.Where(x => x.Id == id).Select(x => EF.Property<bool>(x, HostTenantConsts.IsActiveProperty)).SingleOrDefaultAsync();

	public async Task<(long Count, System.Collections.Generic.List<Tenant> Items)> GetPageAsync(
		string? filter, bool? isActive, string? sorting, int skip, int take)
	{
		var query = (await provider.GetDbContextAsync()).Tenants.AsNoTracking();
		if (!string.IsNullOrWhiteSpace(filter))
		{
			var search = filter.Trim().ToUpperInvariant();
			query = query.Where(x => x.Name.ToUpper().Contains(search) ||
				(EF.Property<string>(x, HostTenantConsts.InstituteNameProperty) ?? "").ToUpper().Contains(search));
		}
		if (isActive.HasValue) query = query.Where(x => EF.Property<bool>(x, HostTenantConsts.IsActiveProperty) == isActive.Value);
		var count = await query.LongCountAsync();
		var order = (sorting ?? "name asc").Trim().ToLowerInvariant();
		IOrderedQueryable<Tenant> ordered = order switch
		{
			"name" or "name asc" => query.OrderBy(x => x.Name),
			"name desc" => query.OrderByDescending(x => x.Name),
			"institutename" or "institutename asc" => query.OrderBy(x => EF.Property<string>(x, HostTenantConsts.InstituteNameProperty)),
			"institutename desc" => query.OrderByDescending(x => EF.Property<string>(x, HostTenantConsts.InstituteNameProperty)),
			"isactive" or "isactive asc" => query.OrderBy(x => EF.Property<bool>(x, HostTenantConsts.IsActiveProperty)),
			"isactive desc" => query.OrderByDescending(x => EF.Property<bool>(x, HostTenantConsts.IsActiveProperty)),
			_ => throw new BusinessException(HostTenantConsts.InvalidSorting)
		};
		return (count, await ordered.ThenBy(x => x.Name).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync());
	}
}
