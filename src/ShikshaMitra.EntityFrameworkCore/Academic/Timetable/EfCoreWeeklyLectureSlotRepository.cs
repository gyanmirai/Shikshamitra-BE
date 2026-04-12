using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Timetable;

public class EfCoreWeeklyLectureSlotRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, WeeklyLectureSlot, Guid>(dbContextProvider),
    IWeeklyLectureSlotRepository
{
    public async Task<WeeklyLectureSlot?> FindByStandardIdAsync(Guid standardId)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(x => x.Subjects)
            .FirstOrDefaultAsync(x => x.StandardId == standardId);
    }

    public override async Task<IQueryable<WeeklyLectureSlot>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).Include(x => x.Subjects);
    }
}
