using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Application.HR.Calendar.Events;

[RemoteService(false)]
public class EventMutationWorkflow(
	IEventRepository eventRepository,
	IRepository<EventType, Guid> eventTypeRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<EventDto> CreateAsync(CreateUpdateEventDto input)
	{
		var eventType = await GetEventTypeAsync(input.EventTypeId);
		var entity = CreateEvent(input, eventType);

		await eventRepository.InsertAsync(entity);
		var eventDto = ObjectMapper.Map<Event, EventDto>(entity);
		eventDto.Status = (int)entity.GetStatus(CurrentInstant());
		eventDto.EventType = ObjectMapper.Map<EventType, EventTypeDto>(eventType);

		return eventDto;
	}

	public async Task<EventDto> UpdateAsync(Guid id, CreateUpdateEventDto input)
	{
		var entity = await eventRepository.GetAsync(id);
		var eventType = await GetEventTypeAsync(input.EventTypeId);

		UpdateEvent(entity, input, eventType);
		entity.EventTypeId = input.EventTypeId;

		await eventRepository.UpdateAsync(entity);
		var eventDto = ObjectMapper.Map<Event, EventDto>(entity);
		eventDto.Status = (int)entity.GetStatus(CurrentInstant());
		eventDto.EventType = ObjectMapper.Map<EventType, EventTypeDto>(eventType);

		return eventDto;
	}

	private async Task<EventType> GetEventTypeAsync(Guid eventTypeId) =>
		await eventTypeRepository.FindAsync(x => x.Id == eventTypeId)
			?? throw new BusinessException(HRErrorCodes.EventTypeNotFound);

	private Event CreateEvent(CreateUpdateEventDto input, EventType eventType)
	{
		var isMeetingEvent = eventType.Name.Equals(EventTypeNames.MeetingEvent, StringComparison.OrdinalIgnoreCase);

		if (isMeetingEvent)
		{
			EnsureMeetingDateExists(input.MeetingDate);

			return Event.CreateMeetingEvent(
				CurrentTenant.Id,
				input.AcademicYearId,
				input.Title,
				input.EventTypeId,
				input.MeetingDate!.Value,
				input.StartTime,
				input.EndTime,
				input.Description,
				input.Poster,
				input.Mode,
				input.Venue,
				input.IsRecurring,
				input.RecurrenceJson,
				input.AudienceIds);
		}

		EnsureMeetingDateAbsent(input.MeetingDate);

		return Event.CreateEvent(
			CurrentTenant.Id,
			input.AcademicYearId,
			input.Title,
			input.EventTypeId,
			input.StartTime,
			input.EndTime,
			input.Description,
			input.Poster,
			input.Mode,
			input.Venue,
			input.IsRecurring,
			input.RecurrenceJson,
			input.AudienceIds);
	}

	private static void UpdateEvent(Event entity, CreateUpdateEventDto input, EventType eventType)
	{
		var isMeetingEvent = eventType.Name.Equals(EventTypeNames.MeetingEvent, StringComparison.OrdinalIgnoreCase);

		if (isMeetingEvent)
		{
			EnsureMeetingDateExists(input.MeetingDate);

			entity.UpdateMeetingEvent(
				input.Title,
				input.MeetingDate!.Value,
				input.StartTime,
				input.EndTime,
				input.Description,
				input.Poster,
				input.Mode,
				input.Venue,
				input.IsRecurring,
				input.RecurrenceJson,
				input.AudienceIds);
			return;
		}

		EnsureMeetingDateAbsent(input.MeetingDate);

		entity.UpdateEvent(
			input.Title,
			input.StartTime,
			input.EndTime,
			input.Description,
			input.Poster,
			input.Mode,
			input.Venue,
			input.IsRecurring,
			input.RecurrenceJson,
			input.AudienceIds);
	}

	private static void EnsureMeetingDateExists(DateTimeOffset? meetingDate)
	{
		if (!meetingDate.HasValue)
		{
			throw new BusinessException(HRErrorCodes.EventMeetingDateRequired);
		}
	}

	private static void EnsureMeetingDateAbsent(DateTimeOffset? meetingDate)
	{
		if (meetingDate.HasValue)
		{
			throw new BusinessException(HRErrorCodes.EventMeetingDateNotAllowed);
		}
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
