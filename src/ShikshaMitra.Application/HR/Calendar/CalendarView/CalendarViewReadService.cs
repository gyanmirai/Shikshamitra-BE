using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.Security.UserScope;
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
	IGroupAppService groupAppService,
	IUserScopeService userScopeService) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<CalendarViewItemDto>> GetViewAsync(GetCalendarViewRequestDto input)
	{
		var visibility = await ResolveVisibilityAsync();
		var eventTypeDict = (await AsyncExecuter.ToListAsync(await eventTypeRepository.GetQueryableAsync()))
			.ToDictionary(et => et.Id);
		var holidayTypeDict = (await AsyncExecuter.ToListAsync(await holidayTypeRepository.GetQueryableAsync()))
			.ToDictionary(ht => ht.Id);

		var events = await GetVisibleEventsAsync(input, visibility);
		var holidays = await GetVisibleHolidaysAsync(input, visibility);
		var allItems = events
			.Select(e => new CalendarViewItemDto
			{
				Id = e.Id,
				Title = e.Title,
				CalendarItemType = CalendarItemType.Event,
				Color = eventTypeDict.TryGetValue(e.EventTypeId, out var eventType) ? eventType.Color : string.Empty
			})
			.Concat(holidays.Select(h => new CalendarViewItemDto
			{
				Id = h.Id,
				Title = h.HolidayName,
				CalendarItemType = CalendarItemType.Holiday,
				Color = holidayTypeDict.TryGetValue(h.HolidayTypeId, out var holidayType) ? holidayType.Color : string.Empty
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

	private async Task<CalendarVisibilityContext> ResolveVisibilityAsync()
	{
		var isAdmin = false;
		var userGroupIds = new List<Guid>();

		if (CurrentUser.Id.HasValue)
		{
			isAdmin = await userScopeService.IsAdminUserAsync(CurrentUser.Id.Value);
			if (!isAdmin)
			{
				userGroupIds = await groupAppService.GetUserGroupIdsAsync(CurrentUser.Id.Value);
			}
		}

		return new CalendarVisibilityContext(isAdmin, userGroupIds);
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

		var events = await AsyncExecuter.ToListAsync(eventQuery);
		if (input.Status.HasValue)
		{
			events = events.Where(e => (int)e.GetStatus(now) == input.Status.Value).ToList();
		}

		return ApplyVisibility(events, visibility, e => e.AudienceIds);
	}

	private async Task<List<Holiday>> GetVisibleHolidaysAsync(
		GetCalendarViewRequestDto input,
		CalendarVisibilityContext visibility)
	{
		var now = CurrentInstant();
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

		var holidays = await AsyncExecuter.ToListAsync(holidayQuery);
		if (input.Status.HasValue)
		{
			holidays = holidays.Where(h => (int)h.GetStatus(now) == input.Status.Value).ToList();
		}

		return ApplyVisibility(holidays, visibility, h => h.AudienceIds);
	}

	private static List<T> ApplyVisibility<T>(
		List<T> items,
		CalendarVisibilityContext visibility,
		Func<T, IReadOnlyCollection<Guid>> audienceSelector)
	{
		if (visibility.IsAdmin)
		{
			return items;
		}

		if (visibility.UserGroupIds.Count == 0)
		{
			return items.Where(x => audienceSelector(x).Count == 0).ToList();
		}

		return items
			.Where(x => audienceSelector(x).Count == 0 ||
						audienceSelector(x).Any(visibility.UserGroupIds.Contains))
			.ToList();
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

	private sealed record CalendarVisibilityContext(bool IsAdmin, List<Guid> UserGroupIds);
}
