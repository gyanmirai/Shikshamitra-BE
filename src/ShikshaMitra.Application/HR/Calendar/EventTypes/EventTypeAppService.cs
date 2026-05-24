using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Calendar.EventTypes;

/// <summary>
/// Application service for managing event types with CRUD operations
/// </summary>
[RemoteService(false)]
public class EventTypeAppService :
	CrudAppService<EventType, EventTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEventTypeDto>,
	IEventTypeAppService
{
	private readonly IEventTypeRepository eventTypeRepository;

	public EventTypeAppService(IEventTypeRepository eventTypeRepository)
		: base(eventTypeRepository)
	{
		this.eventTypeRepository = eventTypeRepository ?? throw new ArgumentNullException(nameof(eventTypeRepository));
		GetPolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.View;
		GetListPolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.View;
		CreatePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Add;
		UpdatePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Add;
		DeletePolicyName = ShikshaMitraSubmodulePermissions.HR.Calendar.Delete;
	}

	/// <summary>
	/// Get paginated list of event types with optional name search
	/// </summary>
	public async Task<PagedResultDto<EventTypeDto>> GetListByNameAsync(GetEventTypeListRequestDto input)
	{
		// Build query with search filter
		var query = (await eventTypeRepository.GetQueryableAsync()).AsQueryable();

		// Apply search filter if provided
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var searchTerm = input.Search.Trim();
			query = query.Where(e => e.Name.Contains(searchTerm));
		}

		// Apply sorting
		if (!input.Sorting.IsNullOrWhiteSpace())
		{
			query = query.OrderBy(input.Sorting);
		}
		else
		{
			query = query.OrderByDescending(e => e.CreationTime); // Default: newest first
		}

		// Get total count before pagination
		var totalCount = query.Count();

		// Apply pagination
		var items = query
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		// Map to DTOs
		var dtos = ObjectMapper.Map<List<EventType>, List<EventTypeDto>>(items);

		return new PagedResultDto<EventTypeDto>
		{
			TotalCount = totalCount,
			Items = dtos
		};
	}

	/// <summary>
	/// Get all event types for the current tenant (non-paginated)
	/// </summary>
	public async Task<List<EventTypeDto>> GetAllEventTypesAsync()
	{
		var eventTypes = await eventTypeRepository.GetListAsync(includeDetails: false);
		return ObjectMapper.Map<List<EventType>, List<EventTypeDto>>(eventTypes);
	}

	/// <summary>
	/// Override Create to use factory method
	/// </summary>
	public override async Task<EventTypeDto> CreateAsync(CreateUpdateEventTypeDto input)
	{
		// Get current tenant ID (ABP handles this automatically via CurrentTenant)
		var tenantId = CurrentTenant.Id;

		// Create entity using factory method
		var entity = EventType.Create(tenantId, input.Name, input.Color, input.Description);

		// Insert and return
		var result = await Repository.InsertAsync(entity, autoSave: true);
		return ObjectMapper.Map<EventType, EventTypeDto>(result);
	}

	/// <summary>
	/// Override Update to use the entity's Update method
	/// </summary>
	public override async Task<EventTypeDto> UpdateAsync(Guid id, CreateUpdateEventTypeDto input)
	{
		var entity = await Repository.GetAsync(id);
		entity.Update(input.Name, input.Color, input.Description);
		await Repository.UpdateAsync(entity, autoSave: true);
		return ObjectMapper.Map<EventType, EventTypeDto>(entity);
	}
}
