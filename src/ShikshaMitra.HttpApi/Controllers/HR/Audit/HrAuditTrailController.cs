using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Audit;

[Area("app")]
[RemoteService(Name = "Default")]
[Authorize]
public class HrAuditTrailController(IHrAuditTrailAppService appService) : AbpControllerBase
{
	[HttpGet("api/hr/audit-events")]
	public Task<PagedResultDto<HrAuditEventDto>> GetListAsync([FromQuery] HrAuditEventListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("api/hr/{entityType}/{entityId:guid}/audit-events")]
	public Task<PagedResultDto<HrAuditEventDto>> GetEntityEventsAsync(
		string entityType,
		Guid entityId,
		[FromQuery] HrAuditEventListRequestDto input) =>
		appService.GetEntityEventsAsync(entityType, entityId, input);
}
