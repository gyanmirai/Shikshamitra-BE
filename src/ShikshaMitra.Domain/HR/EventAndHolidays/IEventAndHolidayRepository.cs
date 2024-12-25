using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.EventAndHolidays;

public interface IEventAndHolidayRepository : IRepository<EventAndHoliday,Guid>
{
	 // if we are using some additional method then we have to write it here
}