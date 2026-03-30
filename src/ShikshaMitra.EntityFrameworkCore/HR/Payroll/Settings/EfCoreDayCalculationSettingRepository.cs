using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EfCoreDayCalculationSettingRepository
	: EfCoreRepository<ShikshaMitraDbContext, DayCalculationSetting, Guid>, IDayCalculationSettingRepository
{
	public EfCoreDayCalculationSettingRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<DayCalculationSetting?> FindByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.TenantId == tenantId)
			.FirstOrDefaultAsync(cancellationToken);
	}
}
