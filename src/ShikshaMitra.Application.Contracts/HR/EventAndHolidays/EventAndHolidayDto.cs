using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.EventAndHolidays;

public class EventAndHolidayDto : AuditedEntityDto<Guid>
{
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	public string Reason { get; set; }
	public bool IsHoliday { get; set; }
}