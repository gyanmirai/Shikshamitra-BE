using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/finance/proration-policies")]
[Authorize]
public class ProrationPoliciesController(IProrationPolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<ProrationPolicyDto>> GetListAsync([FromQuery] ProrationPolicyListRequestDto input) => appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<ProrationPolicyDto> GetAsync(Guid id) => appService.GetAsync(id);

	[HttpGet("{id:guid}/impact")]
	public Task<ProrationPolicyImpactDto> GetImpactAsync(Guid id) => appService.GetImpactAsync(id);

	[HttpPost]
	public Task<ProrationPolicyDto> CreateAsync([FromBody] CreateUpdateProrationPolicyDto input) => appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<ProrationPolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdateProrationPolicyDto input) => appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/activate")]
	public Task<ProrationPolicyDto> ActivateAsync(Guid id, [FromQuery] string? changeReason = null) => appService.ActivateAsync(id, changeReason);

	[HttpPost("{id:guid}/clone")]
	public Task<ProrationPolicyDto> CloneAsync(Guid id, [FromBody] ProrationPolicyCloneRequestDto input) => appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task ArchiveAsync(Guid id) => appService.ArchiveAsync(id);

	[HttpPost("{id:guid}/unarchive")]
	public Task<ProrationPolicyDto> UnarchiveAsync(Guid id) => appService.UnarchiveAsync(id);

	[HttpGet("{id:guid}/versions")]
	public Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, [FromQuery] FinancePolicyVersionListRequestDto input) => appService.GetVersionsAsync(id, input);

	[HttpPost("{id:guid}/simulate")]
	public Task<ProrationCalculationResultDto> SimulateAsync(Guid id, [FromBody] ProrationPolicySimulateRequestDto input) => appService.SimulateAsync(id, input);

}
