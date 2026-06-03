using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Calendar.Events;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.HR.Calendar.Events;

[RemoteService(false)]
public class EventAppService : CrudAppService<
	Event,
	EventDto,
	Guid,
	GetEventListRequestDto,
	CreateUpdateEventDto>, IEventAppService
{
	private readonly EventReadService _eventReadService;
	private readonly EventMutationWorkflow _mutationWorkflow;

	public EventAppService(
		IEventRepository eventRepository,
		EventReadService eventReadService,
		EventMutationWorkflow mutationWorkflow)
		: base(eventRepository)
	{
		_eventReadService = eventReadService;
		_mutationWorkflow = mutationWorkflow;
		GetPolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.View;
		GetListPolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.View;
		CreatePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Add;
		UpdatePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Add;
		DeletePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Delete;
	}

	public override Task<PagedResultDto<EventDto>> GetListAsync(GetEventListRequestDto input) =>
		_eventReadService.GetListAsync(input);

	public Task<List<EventDto>> GetAllEventsAsync() =>
		_eventReadService.GetAllEventsAsync();

	public Task<List<EventDto>> GetByTypeAsync(Guid typeId) =>
		_eventReadService.GetByTypeAsync(typeId);

	public Task<PagedResultDto<EventDto>> GetByStatusAsync(GetEventListRequestDto input) =>
		_eventReadService.GetByStatusAsync(input);

	public override Task<EventDto> GetAsync(Guid id) =>
		_eventReadService.GetAsync(id);

	public override Task<EventDto> CreateAsync(CreateUpdateEventDto input) =>
		_mutationWorkflow.CreateAsync(input);

	public override Task<EventDto> UpdateAsync(Guid id, CreateUpdateEventDto input) =>
		_mutationWorkflow.UpdateAsync(id, input);
}
