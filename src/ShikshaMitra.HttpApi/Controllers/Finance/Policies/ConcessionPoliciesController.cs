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
[Route("api/finance/concession-policies")]
[Authorize]
public class ConcessionPoliciesController(IConcessionPolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<ConcessionPolicyDto>> GetListAsync([FromQuery] ConcessionPolicyListRequestDto input) => appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<ConcessionPolicyDto> GetAsync(Guid id) => appService.GetAsync(id);

	[HttpGet("{id:guid}/impact")]
	public Task<ConcessionPolicyImpactDto> GetImpactAsync(Guid id) => appService.GetImpactAsync(id);

	[HttpPost]
	public Task<ConcessionPolicyDto> CreateAsync([FromBody] CreateUpdateConcessionPolicyDto input) => appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<ConcessionPolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdateConcessionPolicyDto input) => appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/clone")]
	public Task<ConcessionPolicyDto> CloneAsync(Guid id, [FromBody] ConcessionPolicyCloneRequestDto input) => appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task ArchiveAsync(Guid id) => appService.ArchiveAsync(id);

	[HttpPost("{id:guid}/unarchive")]
	public Task<ConcessionPolicyDto> UnarchiveAsync(Guid id) => appService.UnarchiveAsync(id);

	[HttpGet("{id:guid}/versions")]
	public Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, [FromQuery] FinancePolicyVersionListRequestDto input) => appService.GetVersionsAsync(id, input);

	[HttpPost("{id:guid}/simulate")]
	public Task<ConcessionPolicySimulateResponseDto> SimulateAsync(Guid id, [FromBody] ConcessionPolicySimulateRequestDto input) => appService.SimulateAsync(id, input);
}
