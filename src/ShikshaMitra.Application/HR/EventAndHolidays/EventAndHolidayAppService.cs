using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.EventAndHolidays;
// private readonly IFileStorage _fileStorage;

public class EventAndHolidayAppService(IEventAndHolidayRepository eventAndHolidayRepository) : CrudAppService<HR.EventAndHolidays.EventAndHoliday, EventAndHolidayDto, Guid, PagedAndSortedResultRequestDto,CreateUpdateEventAndHolidayDto>(eventAndHolidayRepository), IEventAndHolidayAppService
{
	
}