using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Audit;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Audit;

[Area("app")]
[RemoteService(Name = "Default")]
[Authorize]
[Route("api/finance")]
public class FinanceAuditTrailController(IFinanceAuditTrailAppService appService) : AbpControllerBase
{
	[HttpGet("audit-events")]
	public Task<PagedResultDto<FinanceAuditEventDto>> GetListAsync([FromQuery] FinanceAuditEventListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("students/audit-lookup")]
	public Task<ListResultDto<FinanceAuditStudentLookupDto>> LookupStudentsForAuditAsync(
		[FromQuery] FinanceAuditStudentLookupRequestDto input) =>
		appService.LookupStudentsForAuditAsync(input);

	[HttpGet("audit-events/{id:guid}")]
	public Task<FinanceAuditEventDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpGet("audit-events/by-correlation/{correlationId}")]
	public Task<FinanceAuditCorrelationDto> GetByCorrelationAsync(
		string correlationId,
		[FromQuery] PagedAndSortedResultRequestDto input) =>
		appService.GetByCorrelationAsync(correlationId, input);

	[HttpGet("students/{studentId:guid}/audit-events")]
	public Task<PagedResultDto<FinanceAuditEventDto>> GetStudentEventsAsync(
		Guid studentId,
		[FromQuery] FinanceAuditEventListRequestDto input) =>
		appService.GetStudentEventsAsync(studentId, input);

	[HttpGet("{entityType}/{entityId:guid}/audit-events")]
	public Task<PagedResultDto<FinanceAuditEventDto>> GetEntityEventsAsync(
		string entityType,
		Guid entityId,
		[FromQuery] FinanceAuditEventListRequestDto input) =>
		appService.GetEntityEventsAsync(entityType, entityId, input);
}
