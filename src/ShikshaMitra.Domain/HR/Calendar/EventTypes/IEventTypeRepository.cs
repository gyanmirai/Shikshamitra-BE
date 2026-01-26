using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Calendar.EventTypes;

public interface IEventTypeRepository : IRepository<EventType, Guid>
{
    /// <summary>
    /// Get event types by name search (case-insensitive)
    /// </summary>
    Task<List<EventType>> GetByNameAsync(string name, bool includeDetails = false);

    /// <summary>
    /// Get all event types for the current tenant
    /// </summary>
    Task<List<EventType>> GetAllAsync(bool includeDetails = false);

    /// <summary>
    /// Check if an event type with the given name exists
    /// </summary>
    Task<bool> ExistsByNameAsync(string name);
}
