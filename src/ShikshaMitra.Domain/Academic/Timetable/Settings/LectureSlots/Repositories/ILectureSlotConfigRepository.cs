using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public interface ILectureSlotConfigRepository : IRepository<LectureSlotConfig, Guid>
{
    Task<LectureSlotConfig?> FindByStandardIdAsync(Guid standardId);
}
