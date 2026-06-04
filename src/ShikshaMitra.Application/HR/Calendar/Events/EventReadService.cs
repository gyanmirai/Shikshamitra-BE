using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp;

namespace ShikshaMitra.Application.HR.Calendar.Events;

[RemoteService(false)]
public class EventReadService(
	IEventRepository eventRepository,
	IRepository<EventType, Guid> eventTypeRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<EventDto>> GetListAsync(GetEventListRequestDto input)
	{
		var search = input.Search;
		var query = (await eventRepository.GetQueryableAsync())
			.WhereIf(!search.IsNullOrWhiteSpace(), x => x.Title.Contains(search!))
			.WhereIf(input.AcademicYearId.HasValue, x => x.AcademicYearId == input.AcademicYearId)
			.WhereIf(input.TypeId.HasValue, x => x.EventTypeId == input.TypeId)
			.WhereIf(input.Mode.HasValue, x => (int)x.Mode == input.Mode)
			.WhereIf(input.FromDate.HasValue, x => x.MeetingDate >= input.FromDate)
			.WhereIf(input.ToDate.HasValue, x => x.MeetingDate <= input.ToDate);

		var totalCount = await asyncExecuter.CountAsync(query);
		var sortedQuery = ApplySorting(query, input.Sorting);
		var items = await asyncExecuter.ToListAsync(sortedQuery.Skip(input.SkipCount).Take(input.MaxResultCount));
		var eventDtos = ObjectMapper.Map<List<Event>, List<EventDto>>(items);
		ApplyStatuses(items, eventDtos);

		await PopulateEventTypesAsync(eventDtos);

		return new PagedResultDto<EventDto>(totalCount, eventDtos);
	}

	public async Task<List<EventDto>> GetAllEventsAsync()
	{
		var events = await eventRepository.GetAllAsync();
		var eventDtos = ObjectMapper.Map<List<Event>, List<EventDto>>(events);
		ApplyStatuses(events, eventDtos);
		return eventDtos;
	}

	public async Task<List<EventDto>> GetByTypeAsync(Guid typeId)
	{
		var events = await eventRepository.GetByTypeAsync(typeId);
		var eventDtos = ObjectMapper.Map<List<Event>, List<EventDto>>(events);
		ApplyStatuses(events, eventDtos);
		return eventDtos;
	}

	public async Task<PagedResultDto<EventDto>> GetByStatusAsync(GetEventListRequestDto input)
	{
		var allEvents = await eventRepository.GetAllAsync();
		var search = input.Search;
		var now = CurrentInstant();
		var filteredEvents = allEvents
			.WhereIf(input.Status.HasValue, x => (int)x.GetStatus(now) == input.Status)
			.ToList();

		var totalCount = filteredEvents.Count;
		var sortedAndPaged = filteredEvents
			.WhereIf(!search.IsNullOrWhiteSpace(), x => x.Title.Contains(search!))
			.WhereIf(input.TypeId.HasValue, x => x.EventTypeId == input.TypeId)
			.WhereIf(input.Mode.HasValue, x => (int)x.Mode == input.Mode)
			.WhereIf(input.FromDate.HasValue, x => x.MeetingDate >= input.FromDate)
			.WhereIf(input.ToDate.HasValue, x => x.MeetingDate <= input.ToDate)
			.OrderBy(x => x.Title)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		var eventDtos = ObjectMapper.Map<List<Event>, List<EventDto>>(sortedAndPaged);
		ApplyStatuses(sortedAndPaged, eventDtos, now);
		return new PagedResultDto<EventDto>(totalCount, eventDtos);
	}

	public async Task<EventDto> GetAsync(Guid id)
	{
		var entity = await eventRepository.GetAsync(id);
		var eventDto = ObjectMapper.Map<Event, EventDto>(entity);
		eventDto.Status = (int)entity.GetStatus(CurrentInstant());
		await PopulateEventTypeAsync(eventDto);
		return eventDto;
	}

	public async Task PopulateEventTypeAsync(EventDto eventDto)
	{
		var eventType = await eventTypeRepository.GetAsync(eventDto.EventTypeId);
		eventDto.EventType = ObjectMapper.Map<EventType, EventTypeDto>(eventType);
	}

	private async Task PopulateEventTypesAsync(List<EventDto> eventDtos)
	{
		var eventTypeIds = eventDtos.Select(e => e.EventTypeId).Distinct().ToList();
		var eventTypes = await asyncExecuter.ToListAsync(
			(await eventTypeRepository.GetQueryableAsync()).Where(et => eventTypeIds.Contains(et.Id)));
		var eventTypeMap = eventTypes.ToDictionary(et => et.Id, et => ObjectMapper.Map<EventType, EventTypeDto>(et));

		foreach (var eventDto in eventDtos)
		{
			if (eventTypeMap.TryGetValue(eventDto.EventTypeId, out var etDto))
			{
				eventDto.EventType = etDto;
			}
		}
	}

	private static IOrderedQueryable<Event> ApplySorting(IQueryable<Event> query, string? sorting) =>
		sorting switch
		{
			"meetingDate" => query.OrderBy(x => x.MeetingDate),
			"title" => query.OrderBy(x => x.Title),
			"creationTime" => query.OrderByDescending(x => x.CreationTime),
			_ => query.OrderBy(x => x.Title)
		};

	private void ApplyStatuses(IReadOnlyList<Event> events, IReadOnlyList<EventDto> eventDtos, DateTimeOffset? now = null)
	{
		var effectiveNow = now ?? CurrentInstant();
		for (var i = 0; i < events.Count && i < eventDtos.Count; i++)
		{
			eventDtos[i].Status = (int)events[i].GetStatus(effectiveNow);
		}
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
