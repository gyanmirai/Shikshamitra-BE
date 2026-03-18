using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Calendar.Holidays;

public interface IHolidayRepository : IRepository<Holiday, Guid>
{
    /// <summary>
    /// Get holidays by name search (case-insensitive)
    /// </summary>
    Task<List<Holiday>> GetByNameAsync(string name, bool includeDetails = false);

    /// <summary>
    /// Get all holidays for the current tenant
    /// </summary>
    Task<List<Holiday>> GetAllAsync(bool includeDetails = false);

    /// <summary>
    /// Get holidays by type (EventType)
    /// </summary>
    Task<List<Holiday>> GetByTypeAsync(Guid typeId, bool includeDetails = false);

    /// <summary>
    /// Get holidays within a date range
    /// </summary>
    Task<List<Holiday>> GetByDateRangeAsync(DateTimeOffset startDate, DateTimeOffset endDate, bool includeDetails = false);

    /// <summary>
    /// Check if a holiday with the given name exists
    /// </summary>
    Task<bool> ExistsByNameAsync(string name);
}
