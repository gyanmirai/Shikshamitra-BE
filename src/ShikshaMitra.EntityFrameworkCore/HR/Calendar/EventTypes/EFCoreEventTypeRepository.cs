using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Calendar.EventTypes;

public class EFCoreEventTypeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) :
    EfCoreRepository<ShikshaMitraDbContext, EventType, Guid>(dbContextProvider),
    IEventTypeRepository
{
    /// <summary>
    /// Get event types by name search (case-insensitive)
    /// </summary>
    public async Task<List<EventType>> GetByNameAsync(string name, bool includeDetails = false)
    {
        var query = (await GetQueryableAsync()).Where(e =>
            e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        return await AsyncExecuter.ToListAsync(query);
    }

    /// <summary>
    /// Get all event types for the current tenant
    /// </summary>
    public async Task<List<EventType>> GetAllAsync(bool includeDetails = false)
    {
        return await GetListAsync(includeDetails: includeDetails);
    }

    /// <summary>
    /// Check if an event type with the given name exists
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name)
    {
        var query = (await GetQueryableAsync()).Any(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return query;
    }
}
