using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.EventAndHolidays;

public interface IEventAndHolidayAppService : ICrudAppService<EventAndHolidayDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEventAndHolidayDto>
{
	
}