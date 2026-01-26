using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Events;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Linq;

namespace ShikshaMitra.EntityFrameworkCore.HR.Calendar.Events;

/// <summary>
/// EF Core implementation of IEventRepository
/// </summary>
public class EFCoreEventRepository : EfCoreRepository<ShikshaMitraDbContext, Event, Guid>, IEventRepository
{
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public EFCoreEventRepository(
        IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
        IAsyncQueryableExecuter asyncExecuter) : base(dbContextProvider)
    {
        _asyncExecuter = asyncExecuter;
    }

    public async Task<List<Event>> GetAllAsync(bool includeDetails = false)
    {
        var queryable = await GetQueryableAsync();
        return await _asyncExecuter.ToListAsync(queryable.AsQueryable());
    }

    public async Task<List<Event>> GetByTypeAsync(Guid eventTypeId, bool includeDetails = false)
    {
        var queryable = await GetQueryableAsync();
        var query = queryable.Where(x => x.EventTypeId == eventTypeId);
        return await _asyncExecuter.ToListAsync(query.AsQueryable());
    }

    public async Task<List<Event>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, bool includeDetails = false)
    {
        var queryable = await GetQueryableAsync();
        var query = queryable
            .Where(x => x.MeetingDate >= fromDate && x.MeetingDate <= toDate);
        return await _asyncExecuter.ToListAsync(query.AsQueryable());
    }

    public async Task<bool> ExistsByTitleAsync(string title)
    {
        var queryable = await GetQueryableAsync();
        return await _asyncExecuter.AnyAsync(queryable.Where(x => x.Title == title));
    }
}
