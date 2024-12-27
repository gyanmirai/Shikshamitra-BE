using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.EventAndHolidays;
// private readonly IFileStorage _fileStorage;

public class EventAndHolidayAppService(IEventAndHolidayRepository eventAndHolidayRepository) : CrudAppService<HR.EventAndHolidays.EventAndHoliday, EventAndHolidayDto, Guid, PagedAndSortedResultRequestDto,CreateUpdateEventAndHolidayDto>(eventAndHolidayRepository), IEventAndHolidayAppService
{
	
	
	public async Task<PagedResultDto<EventAndHolidayDto>> GetEventAndHolidayByMonth(DateTimeOffset date)
	{
		List<EventAndHoliday> eventAndHoliday =
			await eventAndHolidayRepository.GetListAsync(
				e => e.StartDate.Year == date.Year && e.StartDate.Month == date.Month,
				includeDetails: true
			);
		
		return new PagedResultDto<EventAndHolidayDto>
		{
			TotalCount = eventAndHoliday.Count,
			Items = ObjectMapper.Map<List<EventAndHoliday>, List<EventAndHolidayDto>>(eventAndHoliday)
		};
	}
	
}