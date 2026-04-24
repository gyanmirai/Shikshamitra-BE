using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.HR.Calendar.CalendarView;

/// <summary>
/// Application service for Calendar View operations
/// Provides unified view of events and holidays with visibility filtering
/// </summary>
[RemoteService(false)]
public class CalendarViewAppService : ApplicationService, ICalendarViewAppService
{
    private readonly IEventRepository _eventRepository;
    private readonly IHolidayRepository _holidayRepository;
    private readonly IRepository<EventType, Guid> _eventTypeRepository;
    private readonly IHolidayTypeRepository _holidayTypeRepository;
    private readonly IGroupAppService _groupAppService;
    private readonly IUserScopeService _userScopeService;

    public CalendarViewAppService(
        IEventRepository eventRepository,
        IHolidayRepository holidayRepository,
        IRepository<EventType, Guid> eventTypeRepository,
        IHolidayTypeRepository holidayTypeRepository,
        IGroupAppService groupAppService,
        IUserScopeService userScopeService)
    {
        _eventRepository = eventRepository;
        _holidayRepository = holidayRepository;
        _eventTypeRepository = eventTypeRepository;
        _holidayTypeRepository = holidayTypeRepository;
        _groupAppService = groupAppService;
        _userScopeService = userScopeService;
    }

    /// <summary>
    /// Gets paginated calendar view data with visibility filtering
    /// </summary>
    public async Task<PagedResultDto<CalendarViewItemDto>> GetViewAsync(GetCalendarViewRequestDto input)
    {
        // Get current user's visibility context
        var isAdmin = false;
        var userGroupIds = new List<Guid>();

        if (CurrentUser.Id.HasValue)
        {
            isAdmin = await _userScopeService.IsAdminUserAsync(CurrentUser.Id.Value);
            if (!isAdmin)
            {
                // Pre-fetch all groups the current user belongs to
                userGroupIds = await _groupAppService.GetUserGroupIdsAsync(CurrentUser.Id.Value);
            }
        }

        // Fetch all event types and holiday types for color mapping
        var eventTypes = await AsyncExecuter.ToListAsync(await _eventTypeRepository.GetQueryableAsync());
        var holidayTypes = await AsyncExecuter.ToListAsync(await _holidayTypeRepository.GetQueryableAsync());
        var eventTypeDict = eventTypes.ToDictionary(et => et.Id);
        var holidayTypeDict = holidayTypes.ToDictionary(ht => ht.Id);

        // Query events
        var eventQuery = await _eventRepository.GetQueryableAsync();
        eventQuery = eventQuery.Where(e => e.AcademicYearId == input.AcademicYearId);

        // Apply event-specific filters
        if (input.EventTypeIds != null && input.EventTypeIds.Count > 0)
        {
            eventQuery = eventQuery.Where(e => input.EventTypeIds.Contains(e.EventTypeId));
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            eventQuery = eventQuery.Where(e => e.Title.Contains(input.Search));
        }

        var events = await AsyncExecuter.ToListAsync(eventQuery);

        // Apply status filter for events
        if (input.Status.HasValue)
        {
            events = events.Where(e => (int) e.Status == input.Status.Value).ToList();
        }

        // Apply visibility filter for events (non-admin only)
        if (!isAdmin && userGroupIds.Count > 0)
        {
            events = events.Where(e =>
                e.AudienceIds.Count == 0 || // Events with no audience are visible to all
                e.AudienceIds.Any(aid => userGroupIds.Contains(aid))
            ).ToList();
        }
        else if (!isAdmin && userGroupIds.Count == 0)
        {
            // User is not in any groups, only show events with no audience restriction
            events = events.Where(e => e.AudienceIds.Count == 0).ToList();
        }

        // Query holidays
        var holidayQuery = await _holidayRepository.GetQueryableAsync();
        holidayQuery = holidayQuery.Where(h => h.AcademicYearId == input.AcademicYearId);

        // Apply holiday-specific filters
        if (input.HolidayTypeIds != null && input.HolidayTypeIds.Count > 0)
        {
            holidayQuery = holidayQuery.Where(h => input.HolidayTypeIds.Contains(h.HolidayTypeId));
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            holidayQuery = holidayQuery.Where(h => h.HolidayName.Contains(input.Search));
        }

        var holidays = await AsyncExecuter.ToListAsync(holidayQuery);

        // Apply status filter for holidays
        if (input.Status.HasValue)
        {
            holidays = holidays.Where(h => (int) h.Status == input.Status.Value).ToList();
        }

        // Apply visibility filter for holidays (non-admin only)
        if (!isAdmin && userGroupIds.Count > 0)
        {
            holidays = holidays.Where(h =>
                h.AudienceIds.Count == 0 || // Holidays with no audience are visible to all
                h.AudienceIds.Any(aid => userGroupIds.Contains(aid))
            ).ToList();
        }
        else if (!isAdmin && userGroupIds.Count == 0)
        {
            // User is not in any groups, only show holidays with no audience restriction
            holidays = holidays.Where(h => h.AudienceIds.Count == 0).ToList();
        }

        // Map to unified DTOs
        var eventItems = events.Select(e => new CalendarViewItemDto
        {
            Id = e.Id,
            Title = e.Title,
            CalendarItemType = CalendarItemType.Event,
            Color = eventTypeDict.TryGetValue(e.EventTypeId, out var et) ? et.Color : string.Empty
        });

        var holidayItems = holidays.Select(h => new CalendarViewItemDto
        {
            Id = h.Id,
            Title = h.HolidayName,
            CalendarItemType = CalendarItemType.Holiday,
            Color = holidayTypeDict.TryGetValue(h.HolidayTypeId, out var ht) ? ht.Color : string.Empty
        });

        // Combine and apply pagination
        var allItems = eventItems.Concat(holidayItems).ToList();

        // Apply sorting (default by title)
        if (!string.IsNullOrWhiteSpace(input.Sorting))
        {
            allItems = input.Sorting.ToLowerInvariant() switch
            {
                "title desc" => allItems.OrderByDescending(i => i.Title).ToList(),
                "title" or "title asc" => allItems.OrderBy(i => i.Title).ToList(),
                _ => allItems.OrderBy(i => i.Title).ToList()
            };
        }
        else
        {
            allItems = allItems.OrderBy(i => i.Title).ToList();
        }

        var totalCount = allItems.Count;
        var pagedItems = allItems
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<CalendarViewItemDto>(totalCount, pagedItems);
    }

    /// <summary>
    /// Gets detailed information about a specific calendar item
    /// </summary>
    public async Task<CalendarItemDetailsDto> GetItemDetailsAsync(Guid itemId)
    {
        // Try to find as event first
        var eventEntity = await _eventRepository.FindAsync(itemId);
        if (eventEntity != null)
        {
            return await MapEventToDetailsAsync(eventEntity);
        }

        // Try to find as holiday
        var holiday = await _holidayRepository.FindAsync(itemId);
        if (holiday != null)
        {
            return await MapHolidayToDetailsAsync(holiday);
        }

        throw new BusinessException(HRErrorCodes.CalendarItemNotFound)
			.WithData("itemId", itemId);
    }

    private async Task<CalendarItemDetailsDto> MapEventToDetailsAsync(Event eventEntity)
    {
        var eventType = await _eventTypeRepository.FindAsync(eventEntity.EventTypeId);

        // Calculate audience count
        var audienceCount = 0;
        foreach (var audienceId in eventEntity.AudienceIds)
        {
            try
            {
                var count = await _groupAppService.GetGroupMemberCountAsync(audienceId);
                audienceCount += count;
            }
            catch
            {
                // Group might not exist, skip
            }
        }

        return new CalendarItemDetailsDto
        {
            Id = eventEntity.Id,
            CalendarItemType = CalendarItemType.Event,
            Status = (int) eventEntity.Status,
            Title = eventEntity.Title,
            Date = eventEntity.MeetingDate,
            Time = new CalendarItemTimeDto
            {
                StartTime = eventEntity.StartTime,
                EndTime = eventEntity.EndTime
            },
            AudienceCount = audienceCount,
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
        var holidayType = await _holidayTypeRepository.FindAsync(holiday.HolidayTypeId);

        // Calculate audience count
        var audienceCount = 0;
        foreach (var audienceId in holiday.AudienceIds)
        {
            try
            {
                var count = await _groupAppService.GetGroupMemberCountAsync(audienceId);
                audienceCount += count;
            }
            catch
            {
                // Group might not exist, skip
            }
        }

        return new CalendarItemDetailsDto
        {
            Id = holiday.Id,
            CalendarItemType = CalendarItemType.Holiday,
            Status = (int) holiday.Status,
            Title = holiday.HolidayName,
            Date = holiday.StartDate,
            Time = null, // Holidays don't have specific times
            AudienceCount = audienceCount,
            HolidayType = holidayType != null ? new HolidayTypeDto
            {
                Id = holidayType.Id,
                Name = holidayType.Name,
                Color = holidayType.Color,
                Description = holidayType.Description
            } : null,
            Mode = null, // Holidays don't have mode
            Venue = null, // Holidays don't have venue
            Description = holiday.Description
        };
    }
}
