using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Audit;

[RemoteService(false)]
[Authorize]
public class HrAuditTrailAppService(
	HrAuditTrailAccessService accessService,
	HrAuditTrailReader reader)
	: ApplicationService, IHrAuditTrailAppService
{
	public async Task<PagedResultDto<HrAuditEventDto>> GetListAsync(HrAuditEventListRequestDto input)
	{
		var allowedEntityTypes = await accessService.ResolveAllowedEntityTypesAsync(input.EntityType);
		return await reader.GetListAsync(input, allowedEntityTypes);
	}

	public async Task<PagedResultDto<HrAuditEventDto>> GetEntityEventsAsync(string entityType, Guid entityId, HrAuditEventListRequestDto input)
	{
		input.EntityType = entityType;
		input.EntityId = entityId;
		return await GetListAsync(input);
	}
}
