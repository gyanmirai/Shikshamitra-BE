using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/work-calendar-policies")]
[Authorize]
public class WorkCalendarPoliciesController(IWorkCalendarPolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<WorkCalendarPolicyDto>> GetListAsync([FromQuery] PolicyListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<WorkCalendarPolicyDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpGet("{id:guid}/holidays")]
	public Task<List<WorkCalendarHolidayDto>> GetHolidaysAsync(Guid id) =>
		appService.GetHolidaysAsync(id);

	[HttpGet("holiday-import-preview")]
	public Task<List<WorkCalendarHolidayDto>> GetHolidayImportPreviewAsync([FromQuery] Guid academicYearId) =>
		appService.GetHolidayImportPreviewAsync(academicYearId);

	[HttpPost]
	public Task<WorkCalendarPolicyDto> CreateAsync([FromBody] CreateUpdateWorkCalendarPolicyDto input) =>
		appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<WorkCalendarPolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdateWorkCalendarPolicyDto input) =>
		appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/clone")]
	public Task<WorkCalendarPolicyDto> CloneAsync(Guid id, [FromBody] HrPolicyCloneRequestDto input) =>
		appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task DeleteAsync(Guid id) =>
		appService.DeleteAsync(id);
}
