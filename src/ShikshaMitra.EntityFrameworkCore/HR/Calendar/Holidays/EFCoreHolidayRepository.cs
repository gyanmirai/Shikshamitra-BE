using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.HR.Calendar.Holidays;

/// <summary>
/// EF Core repository implementation for Holiday
/// </summary>
public class EFCoreHolidayRepository : EfCoreRepository<ShikshaMitraDbContext, Holiday, Guid>, IHolidayRepository
{
    public EFCoreHolidayRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    /// <summary>
    /// Get holidays by name search (case-insensitive)
    /// </summary>
    public async Task<List<Holiday>> GetByNameAsync(string name, bool includeDetails = false)
    {
        var query = (await GetQueryableAsync())
            .Where(x => x.HolidayName.Contains(name));

        return await AsyncExecuter.ToListAsync(query);
    }

    /// <summary>
    /// Get all holidays for the current tenant
    /// </summary>
    public async Task<List<Holiday>> GetAllAsync(bool includeDetails = false)
    {
        return await AsyncExecuter.ToListAsync(await GetQueryableAsync());
    }

    /// <summary>
    /// Get holidays by type (EventType)
    /// </summary>
    public async Task<List<Holiday>> GetByTypeAsync(Guid typeId, bool includeDetails = false)
    {
        var query = (await GetQueryableAsync())
            .Where(x => x.HolidayTypeId == typeId);

        return await AsyncExecuter.ToListAsync(query);
    }

    /// <summary>
    /// Get holidays within a date range
    /// </summary>
    public async Task<List<Holiday>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate, bool includeDetails = false)
    {
        var query = (await GetQueryableAsync())
            .Where(x => x.StartDate >= startDate && x.EndDate <= endDate);

        return await AsyncExecuter.ToListAsync(query);
    }

    /// <summary>
    /// Check if a holiday with the given name exists
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name)
    {
        var query = (await GetQueryableAsync())
            .Where(x => x.HolidayName == name);

        return await AsyncExecuter.AnyAsync(query);
    }
}
