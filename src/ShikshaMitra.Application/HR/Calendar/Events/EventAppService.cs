using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Application.HR.Calendar.Events;

/// <summary>
/// Application service for Event CRUD operations
/// </summary>
[RemoteService(false)]
public class EventAppService : CrudAppService<
    Event,
    EventDto,
    Guid,
    GetEventListRequestDto,
    CreateUpdateEventDto>,
    IEventAppService
{
    private readonly IEventRepository _eventRepository;
    private readonly IRepository<EventType, Guid> _eventTypeRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public EventAppService(
        IEventRepository eventRepository,
        IRepository<EventType, Guid> eventTypeRepository,
        IAsyncQueryableExecuter asyncExecuter) : base(eventRepository)
    {
        _eventRepository = eventRepository;
        _eventTypeRepository = eventTypeRepository;
        _asyncExecuter = asyncExecuter;
    }

    public override async Task<PagedResultDto<EventDto>> GetListAsync(GetEventListRequestDto input)
    {
        var query = (await _eventRepository.GetQueryableAsync())
            .WhereIf(!input.Search.IsNullOrWhiteSpace(), x => x.Title.Contains(input.Search))
            .WhereIf(input.AcademicYearId.HasValue, x => x.AcademicYearId == input.AcademicYearId)
            .WhereIf(input.TypeId.HasValue, x => x.EventTypeId == input.TypeId)
            .WhereIf(input.Mode.HasValue, x => (int) x.Mode == input.Mode)
            .WhereIf(input.FromDate.HasValue, x => x.MeetingDate >= input.FromDate)
            .WhereIf(input.ToDate.HasValue, x => x.MeetingDate <= input.ToDate);

        var totalCount = await _asyncExecuter.CountAsync(query);

        // Dynamic sorting
        var sortedQuery = input.Sorting switch
        {
            "meetingDate" => query.OrderBy(x => x.MeetingDate),
            "title" => query.OrderBy(x => x.Title),
            "creationTime" => query.OrderByDescending(x => x.CreationTime),
            _ => query.OrderBy(x => x.Title) // Default sort by title
        };

        var items = await _asyncExecuter.ToListAsync(
            sortedQuery.Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<EventDto>(
            totalCount,
            ObjectMapper.Map<List<Event>, List<EventDto>>(items));
    }

    public async Task<List<EventDto>> GetAllEventsAsync()
    {
        var events = await _eventRepository.GetAllAsync();
        return ObjectMapper.Map<List<Event>, List<EventDto>>(events);
    }

    public async Task<List<EventDto>> GetByTypeAsync(Guid typeId)
    {
        var events = await _eventRepository.GetByTypeAsync(typeId);
        return ObjectMapper.Map<List<Event>, List<EventDto>>(events);
    }

    public async Task<PagedResultDto<EventDto>> GetByStatusAsync(GetEventListRequestDto input)
    {
        var allEvents = await _eventRepository.GetAllAsync();

        // Filter by status (computed property)
        var filteredEvents = allEvents
            .WhereIf(input.Status.HasValue, x => (int) x.Status == input.Status)
            .ToList();

        var totalCount = filteredEvents.Count;

        // Apply other filters
        var sortedAndPaged = filteredEvents
            .WhereIf(!input.Search.IsNullOrWhiteSpace(), x => x.Title.Contains(input.Search))
            .WhereIf(input.TypeId.HasValue, x => x.EventTypeId == input.TypeId)
            .WhereIf(input.Mode.HasValue, x => (int) x.Mode == input.Mode)
            .WhereIf(input.FromDate.HasValue, x => x.MeetingDate >= input.FromDate)
            .WhereIf(input.ToDate.HasValue, x => x.MeetingDate <= input.ToDate)
            .OrderBy(x => x.Title)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<EventDto>(
            totalCount,
            ObjectMapper.Map<List<Event>, List<EventDto>>(sortedAndPaged));
    }

    public override async Task<EventDto> CreateAsync(CreateUpdateEventDto input)
    {
        // Validate that the referenced EventType exists
        var eventTypeExists = await _eventTypeRepository.AnyAsync(x => x.Id == input.EventTypeId);
        if (!eventTypeExists)
        {
            throw new BusinessException($"Event type with ID '{input.EventTypeId}' does not exist.");
        }

        var entity = Event.Create(
            CurrentTenant.Id,
            input.AcademicYearId,
            input.Title,
            input.EventTypeId,
            input.MeetingDate,
            input.StartTime,
            input.EndTime,
            input.Description,
            input.Poster,
            (EventMode) input.Mode,
            input.Venue,
            input.IsRecurring,
            input.RecurrenceJson,
            input.AudienceIds);

        await _eventRepository.InsertAsync(entity);
        return ObjectMapper.Map<Event, EventDto>(entity);
    }

    public override async Task<EventDto> UpdateAsync(Guid id, CreateUpdateEventDto input)
    {
        var entity = await _eventRepository.GetAsync(id);

        entity.Update(
            input.Title,
            input.MeetingDate,
            input.StartTime,
            input.EndTime,
            input.Description,
            input.Poster,
            (EventMode) input.Mode,
            input.Venue,
            input.IsRecurring,
            input.RecurrenceJson,
            input.AudienceIds);

        await _eventRepository.UpdateAsync(entity);
        return ObjectMapper.Map<Event, EventDto>(entity);
    }
}
