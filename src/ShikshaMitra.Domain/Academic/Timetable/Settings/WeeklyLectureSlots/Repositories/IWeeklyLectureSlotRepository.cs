using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public interface IWeeklyLectureSlotRepository : IRepository<WeeklyLectureSlot, Guid>
{
    Task<WeeklyLectureSlot?> FindByStandardIdAsync(Guid standardId);
}
