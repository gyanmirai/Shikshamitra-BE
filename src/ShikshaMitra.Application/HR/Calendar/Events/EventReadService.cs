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
		var now = CurrentInstant();
		var query = (await eventRepository.GetQueryableAsync())
			.WhereIf(!search.IsNullOrWhiteSpace(), x => x.Title.Contains(search!))
			.WhereIf(input.AcademicYearId.HasValue, x => x.AcademicYearId == input.AcademicYearId)
			.WhereIf(input.TypeId.HasValue, x => x.EventTypeId == input.TypeId)
			.WhereIf(input.Mode.HasValue, x => (int)x.Mode == input.Mode);

		if (input.FromDate.HasValue)
		{
			query = query.Where(x => (x.MeetingDate ?? x.EndDate ?? x.StartDate) >= input.FromDate);
		}

		if (input.ToDate.HasValue)
		{
			query = query.Where(x => (x.MeetingDate ?? x.StartDate) <= input.ToDate);
		}

		List<Event> items;
		int totalCount;
		if (input.Status.HasValue || string.Equals(input.Sorting, "status", StringComparison.OrdinalIgnoreCase))
		{
			var filtered = await asyncExecuter.ToListAsync(query);
			if (input.Status.HasValue)
			{
				filtered = filtered.Where(x => (int)x.GetStatus(now) == input.Status.Value).ToList();
			}

			totalCount = filtered.Count;
			items = ApplySorting(filtered, input.Sorting, now)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
				.ToList();
		}
		else
		{
			totalCount = await asyncExecuter.CountAsync(query);
			items = await asyncExecuter.ToListAsync(
				ApplySorting(query, input.Sorting)
					.Skip(input.SkipCount)
					.Take(input.MaxResultCount));
		}
		var eventDtos = ObjectMapper.Map<List<Event>, List<EventDto>>(items);
		ApplyStatuses(items, eventDtos, now);

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
		var query = (await eventRepository.GetQueryableAsync()).Where(x => x.EventTypeId == typeId);
		var events = await asyncExecuter.ToListAsync(query);
		var eventDtos = ObjectMapper.Map<List<Event>, List<EventDto>>(events);
		ApplyStatuses(events, eventDtos);
		return eventDtos;
	}

	public Task<PagedResultDto<EventDto>> GetByStatusAsync(GetEventListRequestDto input) =>
		GetListAsync(input);

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

	private static IOrderedQueryable<Event> ApplySorting(
		IQueryable<Event> events,
		string? sorting) =>
		sorting switch
		{
			"meetingDate" or "startDate" => events.OrderBy(x => x.MeetingDate ?? x.StartDate),
			"title" => events.OrderBy(x => x.Title),
			"creationTime" => events.OrderByDescending(x => x.CreationTime),
			_ => events.OrderBy(x => x.Title)
		};

	private static IOrderedEnumerable<Event> ApplySorting(
		IEnumerable<Event> events,
		string? sorting,
		DateTimeOffset now) =>
		sorting switch
		{
			"meetingDate" or "startDate" => events.OrderBy(x => x.MeetingDate ?? x.StartDate),
			"title" => events.OrderBy(x => x.Title),
			"creationTime" => events.OrderByDescending(x => x.CreationTime),
			"status" => events.OrderBy(x => x.GetStatus(now)),
			_ => events.OrderBy(x => x.Title)
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
