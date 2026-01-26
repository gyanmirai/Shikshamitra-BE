using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Calendar.Events;

/// <summary>
/// Repository interface for Event entity
/// </summary>
public interface IEventRepository : IRepository<Event, Guid>
{
    /// <summary>
    /// Get all events asynchronously
    /// </summary>
    Task<List<Event>> GetAllAsync(bool includeDetails = false);

    /// <summary>
    /// Get events by event type
    /// </summary>
    Task<List<Event>> GetByTypeAsync(Guid eventTypeId, bool includeDetails = false);

    /// <summary>
    /// Get events by date range
    /// </summary>
    Task<List<Event>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, bool includeDetails = false);

    /// <summary>
    /// Check if event with title exists
    /// </summary>
    Task<bool> ExistsByTitleAsync(string title);
}
