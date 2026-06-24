using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Communication;

public interface ICalendarCommunicationAppService : IApplicationService
{
	Task<CalendarCommunicationResultDto> AnnounceEventAsync(Guid eventId, CalendarCommunicationRequestDto input);
	Task<CalendarCommunicationResultDto> AnnounceHolidayAsync(Guid holidayId, CalendarCommunicationRequestDto input);
}
