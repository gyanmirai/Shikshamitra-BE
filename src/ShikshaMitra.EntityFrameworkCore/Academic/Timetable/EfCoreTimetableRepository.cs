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

    public async Task<List<TimetableConflictCandidate>> GetConflictCandidatesAsync(
        Guid standardId,
        Guid divisionId,
        IReadOnlyCollection<TimetableDayOfWeek> days,
        TimeOnly startTime,
        TimeOnly endTime,
        IReadOnlyCollection<Guid> teacherIds,
        IReadOnlyCollection<Guid> roomIds)
    {
        if (days.Count == 0 || (teacherIds.Count == 0 && roomIds.Count == 0))
        {
            return [];
        }

        // Child slot entities have no TenantId. Always begin at the ABP-filtered root;
        // conflicts also include other classes outside the caller's display scope.
        var dbSet = await GetDbSetAsync();
        var query = from timetable in dbSet.AsNoTracking()
                    where (timetable.StandardId != standardId || timetable.DivisionId != divisionId) &&
                          timetable.Status != TimetableStatus.NotCreated
                    from slot in timetable.Slots
                    where days.Contains(slot.Day) && slot.StartTime < endTime && slot.EndTime > startTime
                    from subject in slot.Subjects
                    where (subject.TeacherId.HasValue && teacherIds.Contains(subject.TeacherId.Value)) ||
                          (subject.RoomId.HasValue && roomIds.Contains(subject.RoomId.Value))
                    orderby timetable.Id, slot.Id, subject.Id
                    select new TimetableConflictCandidate(
                        timetable.StandardId,
                        timetable.DivisionId,
                        subject.SubjectId,
                        slot.Day,
                        slot.StartTime,
                        slot.EndTime,
                        subject.TeacherId,
                        subject.RoomId);

        return await query.ToListAsync();
    }

    public override async Task<IQueryable<Timetables.Timetable>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.Slots)
                .ThenInclude(s => s.Subjects);
    }
}
