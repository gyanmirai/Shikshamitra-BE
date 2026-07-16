using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public interface ITimetableRepository : IRepository<Timetable, Guid>
{
    Task<Timetable?> FindByStandardAndDivisionAsync(Guid standardId, Guid divisionId);

    Task<List<Timetable>> GetByStandardIdAsync(Guid standardId);

    Task<List<Timetable>> GetAllWithSlotsAsync();
}
