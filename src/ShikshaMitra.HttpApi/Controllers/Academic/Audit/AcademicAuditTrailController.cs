using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Audit;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Audit;

[Area("academic")]
[RemoteService(Name = "Default")]
[Authorize]
[Route("api/academic")]
public class AcademicAuditTrailController(IAcademicAuditTrailAppService appService) : AbpControllerBase
{
	[HttpGet("audit-events")]
	public Task<PagedResultDto<AcademicAuditEventDto>> GetListAsync([FromQuery] AcademicAuditEventListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("audit-events/{id:guid}")]
	public Task<AcademicAuditEventDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpGet("{entityType}/{entityId:guid}/audit-events")]
	public Task<PagedResultDto<AcademicAuditEventDto>> GetEntityEventsAsync(
		string entityType,
		Guid entityId,
		[FromQuery] AcademicAuditEventListRequestDto input) =>
		appService.GetEntityEventsAsync(entityType, entityId, input);
}
