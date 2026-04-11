using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Communication.AnnouncementTypes;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Communication.AnnouncementTypes;

/// <summary>
/// EF Core implementation of IAnnouncementTypeRepository
/// </summary>
public class EfCoreAnnouncementTypeRepository
    : EfCoreRepository<ShikshaMitraDbContext, AnnouncementType, Guid>, IAnnouncementTypeRepository
{
    public EfCoreAnnouncementTypeRepository(
        IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<AnnouncementType>> GetByAcademicMediumAsync(
        Guid? academicMediumId,
        string? search = null,
        int skipCount = 0,
        int maxResultCount = 10)
    {
        var dbSet = await GetDbSetAsync();
        var query = dbSet.AsQueryable();

        // Filter by academic medium ID
        if (academicMediumId.HasValue)
        {
            query = query.Where(x => x.AcademicMediumId == academicMediumId.Value);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.AnnouncementTypeName.Contains(search));
        }

        return await query
            .OrderBy(x => x.AnnouncementTypeName)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync();
    }

    public async Task<long> GetCountByAcademicMediumAsync(
        Guid? academicMediumId,
        string? search = null)
    {
        var dbSet = await GetDbSetAsync();
        var query = dbSet.AsQueryable();

        // Filter by academic medium ID
        if (academicMediumId.HasValue)
        {
            query = query.Where(x => x.AcademicMediumId == academicMediumId.Value);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.AnnouncementTypeName.Contains(search));
        }

        return await query.CountAsync();
    }

    public async Task<bool> ExistsByNameAsync(
        string announcementTypeName,
        Guid? academicMediumId = null,
        Guid? excludeId = null)
    {
        var dbSet = await GetDbSetAsync();
        var query = dbSet.AsQueryable();

        query = query.Where(x => x.AnnouncementTypeName == announcementTypeName);

        if (academicMediumId.HasValue)
        {
            query = query.Where(x => x.AcademicMediumId == academicMediumId.Value);
        }

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
