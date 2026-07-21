using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public interface ITimetableAppService
{
    Task<TimetableResponseDto> GetByStandardAndDivisionAsync(Guid standardId, Guid divisionId);

    Task SaveAsync(Guid standardId, Guid divisionId, SaveTimetableDto input);

    Task ValidateAsync(Guid standardId, Guid divisionId, SaveTimetableDto input);

    Task<TimetableManagementResponseDto> GetManagementByStandardAsync(
        Guid standardId, GetTimetableManagementInput input);

    Task AssignTeachersAsync(AssignTeachersDto input);

    Task AssignRoomsAsync(AssignRoomsDto input);

    Task<SlotConfigResponseDto> GetSlotConfigByStandardAsync(Guid standardId);

    Task<byte[]> GetClasswiseTimetablePdfAsync(Guid standardId, Guid divisionId);
}
