using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Groups;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Groups;

/// <summary>
/// EF Core repository implementation for Group entity
/// </summary>
public class EFCoreGroupRepository : EfCoreRepository<ShikshaMitraDbContext, Group, Guid>, IGroupRepository
{
    public EFCoreGroupRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Group?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Groups
            .Include(x => x.StudentRules)
            .Include(x => x.EmployeeRules)
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Group>> GetListWithDetailsAsync(
        Guid academicYearId,
        string? filter = null,
        GroupSelectionMode? selectionMode = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query = dbContext.Groups
            .Include(x => x.StudentRules)
            .Include(x => x.EmployeeRules)
            .Include(x => x.Members)
            .Where(x => x.AcademicYearId == academicYearId);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Name.Contains(filter) || 
                                     (x.Description != null && x.Description.Contains(filter)));
        }

        if (selectionMode.HasValue)
        {
            query = query.Where(x => x.SelectionMode == selectionMode.Value);
        }

        return await query
            .OrderBy(x => x.Name)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountAsync(
        Guid academicYearId,
        string? filter = null,
        GroupSelectionMode? selectionMode = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query = dbContext.Groups
            .Where(x => x.AcademicYearId == academicYearId);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Name.Contains(filter) || 
                                     (x.Description != null && x.Description.Contains(filter)));
        }

        if (selectionMode.HasValue)
        {
            query = query.Where(x => x.SelectionMode == selectionMode.Value);
        }

        return await query.LongCountAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query = dbContext.Groups.Where(x => x.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
