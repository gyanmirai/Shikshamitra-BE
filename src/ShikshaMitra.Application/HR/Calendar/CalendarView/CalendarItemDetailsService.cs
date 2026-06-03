using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Event = ShikshaMitra.HR.Calendar.Events.Event;

namespace ShikshaMitra.Application.HR.Calendar.CalendarView;

[RemoteService(false)]
public class CalendarItemDetailsService(
	IEventRepository eventRepository,
	IHolidayRepository holidayRepository,
	IRepository<EventType, Guid> eventTypeRepository,
	IHolidayTypeRepository holidayTypeRepository,
	IGroupAppService groupAppService) : ApplicationService, ITransientDependency
{
	public async Task<CalendarItemDetailsDto> GetItemDetailsAsync(Guid itemId)
	{
		var eventEntity = await eventRepository.FindAsync(itemId);
		if (eventEntity != null)
		{
			return await MapEventToDetailsAsync(eventEntity);
		}

		var holiday = await holidayRepository.FindAsync(itemId);
		if (holiday != null)
		{
			return await MapHolidayToDetailsAsync(holiday);
		}

		throw new BusinessException(HRErrorCodes.CalendarItemNotFound)
			.WithData("itemId", itemId);
	}

	private async Task<CalendarItemDetailsDto> MapEventToDetailsAsync(Event eventEntity)
	{
		var eventType = await eventTypeRepository.FindAsync(eventEntity.EventTypeId);
		return new CalendarItemDetailsDto
		{
			Id = eventEntity.Id,
			CalendarItemType = CalendarItemType.Event,
			Status = (int)eventEntity.GetStatus(CurrentInstant()),
			Title = eventEntity.Title,
			Date = eventEntity.MeetingDate,
			Time = new CalendarItemTimeDto
			{
				StartTime = eventEntity.StartTime,
				EndTime = eventEntity.EndTime
			},
			AudienceCount = await CountAudienceMembersAsync(eventEntity.AudienceIds),
			EventType = eventType != null ? new EventTypeDto
			{
				Id = eventType.Id,
				Name = eventType.Name,
				Color = eventType.Color,
				Description = eventType.Description,
				TenantId = eventType.TenantId
			} : null,
			Mode = eventEntity.Mode,
			Venue = new CalendarVenueDto
			{
				Type = eventEntity.Mode == EventMode.Online ? "ONLINE" : "OFFLINE",
				MeetingLink = eventEntity.Mode == EventMode.Online ? eventEntity.Venue : null,
				Address = eventEntity.Mode == EventMode.Offline ? eventEntity.Venue : null
			},
			Description = eventEntity.Description
		};
	}

	private async Task<CalendarItemDetailsDto> MapHolidayToDetailsAsync(Holiday holiday)
	{
		var holidayType = await holidayTypeRepository.FindAsync(holiday.HolidayTypeId);
		return new CalendarItemDetailsDto
		{
			Id = holiday.Id,
			CalendarItemType = CalendarItemType.Holiday,
			Status = (int)holiday.GetStatus(CurrentInstant()),
			Title = holiday.HolidayName,
			Date = holiday.StartDate,
			Time = null,
			AudienceCount = await CountAudienceMembersAsync(holiday.AudienceIds),
			HolidayType = holidayType != null ? new HolidayTypeDto
			{
				Id = holidayType.Id,
				Name = holidayType.Name,
				Color = holidayType.Color,
				Description = holidayType.Description
			} : null,
			Mode = null,
			Venue = null,
			Description = holiday.Description
		};
	}

	private async Task<int> CountAudienceMembersAsync(System.Collections.Generic.IEnumerable<Guid> audienceIds)
	{
		var audienceCount = 0;
		foreach (var audienceId in audienceIds)
		{
			try
			{
				audienceCount += await groupAppService.GetGroupMemberCountAsync(audienceId);
			}
			catch
			{
				// Missing groups are ignored to preserve the legacy calendar-detail response.
			}
		}

		return audienceCount;
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
