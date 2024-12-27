using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.EventAndHolidays;

public interface IEventAndHolidayAppService : ICrudAppService<EventAndHolidayDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEventAndHolidayDto>
{
	Task<PagedResultDto<EventAndHolidayDto>> GetEventAndHolidayByMonth(DateTimeOffset date);
}