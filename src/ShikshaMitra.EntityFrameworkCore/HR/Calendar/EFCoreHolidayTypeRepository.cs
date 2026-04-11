using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of IHolidayTypeRepository
/// </summary>
public class EFCoreHolidayTypeRepository : EfCoreRepository<ShikshaMitraDbContext, HolidayType, Guid>, IHolidayTypeRepository
{
    public EFCoreHolidayTypeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    /// <summary>
    /// Get HolidayType by name
    /// </summary>
    public async Task<HolidayType?> GetByNameAsync(string name)
    {
        return await DbSet.FirstOrDefaultAsync(x => x.Name == name);
    }

    /// <summary>
    /// Check if HolidayType with given name exists
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await DbSet.AnyAsync(x => x.Name == name);
    }
}
