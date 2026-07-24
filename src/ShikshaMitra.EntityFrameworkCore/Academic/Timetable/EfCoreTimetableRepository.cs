using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Timetable;

public class EfCoreTimetableRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider
) : EfCoreRepository<ShikshaMitraDbContext, Timetables.Timetable, Guid>(dbContextProvider),
    ITimetableRepository
{
    public async Task<Timetables.Timetable?> FindByStandardAndDivisionAsync(Guid standardId, Guid divisionId)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(x => x.Slots)
                .ThenInclude(s => s.Subjects)
            .FirstOrDefaultAsync(x => x.StandardId == standardId && x.DivisionId == divisionId);
    }

    public async Task<List<Timetables.Timetable>> GetByStandardIdAsync(Guid standardId)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(x => x.Slots)
                .ThenInclude(s => s.Subjects)
            .Where(x => x.StandardId == standardId)
            .ToListAsync();
    }

    public async Task<List<Timetables.Timetable>> GetByEmployeeIdAsync(Guid employeeId, Guid? academicYearId = null)
    {
        var dbSet = await GetDbSetAsync();
        var query = dbSet
            .Include(x => x.Slots)
                .ThenInclude(s => s.Subjects)
            .Where(x => x.Slots.Any(slot => slot.Subjects.Any(subject => subject.TeacherId == employeeId)));

        if (academicYearId.HasValue)
        {
            var dbContext = await GetDbContextAsync();
            var standardIds = dbContext.Set<Standard>()
                .Where(s => s.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == academicYearId.Value)
                .Select(s => s.Id);

            query = query.Where(x => standardIds.Contains(x.StandardId));
        }

        return await query.ToListAsync();
    }

    public async Task<List<Timetables.Timetable>> GetAllWithSlotsAsync()
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(x => x.Slots)
                .ThenInclude(s => s.Subjects)
            .ToListAsync();
    }

    public override async Task<IQueryable<Timetables.Timetable>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.Slots)
                .ThenInclude(s => s.Subjects);
    }
}
