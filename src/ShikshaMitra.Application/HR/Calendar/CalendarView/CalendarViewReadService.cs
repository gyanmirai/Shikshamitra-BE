using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Event = ShikshaMitra.HR.Calendar.Events.Event;
using Volo.Abp;

namespace ShikshaMitra.Application.HR.Calendar.CalendarView;

[RemoteService(false)]
public class CalendarViewReadService(
	IEventRepository eventRepository,
	IHolidayRepository holidayRepository,
	IRepository<EventType, Guid> eventTypeRepository,
	IHolidayTypeRepository holidayTypeRepository,
	CalendarViewVisibilityService visibilityService) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<CalendarViewItemDto>> GetViewAsync(GetCalendarViewRequestDto input)
	{
		var now = CurrentInstant();
		var today = CurrentBusinessDate();
		var visibility = await visibilityService.ResolveAsync(input.AcademicYearId);
		var eventTypeDict = (await AsyncExecuter.ToListAsync(await eventTypeRepository.GetQueryableAsync()))
			.ToDictionary(et => et.Id);
		var holidayTypeDict = (await AsyncExecuter.ToListAsync(await holidayTypeRepository.GetQueryableAsync()))
			.ToDictionary(ht => ht.Id);

		var events = await GetVisibleEventsAsync(input, visibility);
		var holidays = await GetVisibleHolidaysAsync(input, visibility, today);
		var allItems = events
			.Select(e => new CalendarViewItemDto
			{
				Id = e.Id,
				Title = e.Title,
				CalendarItemType = CalendarItemType.Event,
				Color = eventTypeDict.TryGetValue(e.EventTypeId, out var eventType) ? eventType.Color : string.Empty,
				TypeName = eventType?.Name ?? string.Empty,
				Description = e.Description,
				Mode = e.Mode.ToString(),
				Venue = e.Venue,
				StartTime = e.StartTime,
				EndTime = e.EndTime,
				AudienceCount = e.AudienceIds.Count,
				Status = (int)e.GetStatus(now),
				StartDate = e.MeetingDate ?? e.StartDate ?? now,
				EndDate = e.MeetingDate ?? e.EndDate ?? e.StartDate ?? now
			})
			.Concat(holidays.Select(h => new CalendarViewItemDto
			{
				Id = h.Id,
				Title = h.HolidayName,
				CalendarItemType = CalendarItemType.Holiday,
				Color = holidayTypeDict.TryGetValue(h.HolidayTypeId, out var holidayType) ? holidayType.Color : string.Empty,
				TypeName = holidayType?.Name ?? string.Empty,
				Description = h.Description,
				AudienceCount = h.AudienceIds.Count,
				Status = (int)h.GetStatus(today),
				StartDate = ToDateTimeOffset(h.StartDate),
				EndDate = ToDateTimeOffset(h.EndDate)
			}))
			.ToList();

		allItems = ApplySorting(allItems, input.Sorting);
		var totalCount = allItems.Count;
		var pagedItems = allItems
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		return new PagedResultDto<CalendarViewItemDto>(totalCount, pagedItems);
	}

	private async Task<List<Event>> GetVisibleEventsAsync(
		GetCalendarViewRequestDto input,
		CalendarVisibilityContext visibility)
	{
		var now = CurrentInstant();
		var eventQuery = await eventRepository.GetQueryableAsync();
		eventQuery = eventQuery.Where(e => e.AcademicYearId == input.AcademicYearId);

		if (input.EventTypeIds != null && input.EventTypeIds.Count > 0)
		{
			eventQuery = eventQuery.Where(e => input.EventTypeIds.Contains(e.EventTypeId));
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			eventQuery = eventQuery.Where(e => e.Title.Contains(input.Search));
		}

		var statuses = GetRequestedStatuses(input);
		var events = await AsyncExecuter.ToListAsync(eventQuery);
		if (statuses.Count > 0)
		{
			events = events.Where(e => statuses.Contains((int)e.GetStatus(now))).ToList();
		}

		return CalendarViewVisibilityService.Apply(events, visibility, e => e.AudienceIds);
	}

	private async Task<List<Holiday>> GetVisibleHolidaysAsync(
		GetCalendarViewRequestDto input,
		CalendarVisibilityContext visibility,
		DateOnly today)
	{
		var holidayQuery = await holidayRepository.GetQueryableAsync();
		holidayQuery = holidayQuery.Where(h => h.AcademicYearId == input.AcademicYearId);

		if (input.HolidayTypeIds != null && input.HolidayTypeIds.Count > 0)
		{
			holidayQuery = holidayQuery.Where(h => input.HolidayTypeIds.Contains(h.HolidayTypeId));
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			holidayQuery = holidayQuery.Where(h => h.HolidayName.Contains(input.Search));
		}

		var statuses = GetRequestedStatuses(input);
		var holidays = await AsyncExecuter.ToListAsync(holidayQuery);
		if (statuses.Count > 0)
		{
			holidays = holidays.Where(h => statuses.Contains((int)h.GetStatus(today))).ToList();
		}

		return CalendarViewVisibilityService.Apply(holidays, visibility, h => h.AudienceIds);
	}

	private static IReadOnlyCollection<int> GetRequestedStatuses(GetCalendarViewRequestDto input)
	{
		if (input.Statuses is { Count: > 0 })
		{
			return input.Statuses.Distinct().ToArray();
		}

		return input.Status.HasValue ? new[] { input.Status.Value } : Array.Empty<int>();
	}

	private static List<CalendarViewItemDto> ApplySorting(
		List<CalendarViewItemDto> allItems,
		string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return allItems.OrderBy(i => i.Title).ToList();
		}

		return sorting.ToLowerInvariant() switch
		{
			"title desc" => allItems.OrderByDescending(i => i.Title).ToList(),
			"title" or "title asc" => allItems.OrderBy(i => i.Title).ToList(),
			_ => allItems.OrderBy(i => i.Title).ToList()
		};
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);

	private DateOnly CurrentBusinessDate() =>
		DateOnly.FromDateTime(Clock.Now);

	private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
		new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
