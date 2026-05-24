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
[Route("api/hr/payroll/policies")]
[Authorize]
public class PayrollPoliciesController(IPayrollPolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<PayrollPolicyDto>> GetListAsync([FromQuery] PolicyListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<PayrollPolicyDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpPost]
	public Task<PayrollPolicyDto> CreateAsync([FromBody] CreateUpdatePayrollPolicyDto input) =>
		appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<PayrollPolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdatePayrollPolicyDto input) =>
		appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/clone")]
	public Task<PayrollPolicyDto> CloneAsync(Guid id, [FromBody] HrPolicyCloneRequestDto input) =>
		appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task DeleteAsync(Guid id) =>
		appService.DeleteAsync(id);

	[HttpPost("{id:guid}/set-default")]
	public Task<PayrollPolicyDto> SetDefaultAsync(Guid id) =>
		appService.SetDefaultAsync(id);
}
