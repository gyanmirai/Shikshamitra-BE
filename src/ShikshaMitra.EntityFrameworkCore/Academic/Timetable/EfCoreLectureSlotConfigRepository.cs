using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Timetable;

public class EfCoreLectureSlotConfigRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, LectureSlotConfig, Guid>(dbContextProvider),
    ILectureSlotConfigRepository
{
    public async Task<LectureSlotConfig?> FindByStandardIdAsync(Guid standardId)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(x => x.SlotGroups)
                .ThenInclude(g => g.TimeSlots)
            .FirstOrDefaultAsync(x => x.StandardId == standardId);
    }

    public override async Task<IQueryable<LectureSlotConfig>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.SlotGroups)
                .ThenInclude(g => g.TimeSlots);
    }
}
