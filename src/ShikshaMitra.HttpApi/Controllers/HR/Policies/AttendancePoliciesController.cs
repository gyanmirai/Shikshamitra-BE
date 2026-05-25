using System;
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
[Route("api/hr/attendance/policies")]
[Authorize]
public class AttendancePoliciesController(IAttendancePolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<AttendancePolicyDto>> GetListAsync([FromQuery] PolicyListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<AttendancePolicyDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpPost]
	public Task<AttendancePolicyDto> CreateAsync([FromBody] CreateUpdateAttendancePolicyDto input) =>
		appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<AttendancePolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdateAttendancePolicyDto input) =>
		appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/clone")]
	public Task<AttendancePolicyDto> CloneAsync(Guid id, [FromBody] HrPolicyCloneRequestDto input) =>
		appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task DeleteAsync(Guid id) =>
		appService.DeleteAsync(id);

	[HttpPost("{id:guid}/unarchive")]
	public Task<AttendancePolicyDto> UnarchiveAsync(Guid id) =>
		appService.UnarchiveAsync(id);

	[HttpPost("{id:guid}/set-default")]
	public Task<AttendancePolicyDto> SetDefaultAsync(Guid id) =>
		appService.SetDefaultAsync(id);
}
