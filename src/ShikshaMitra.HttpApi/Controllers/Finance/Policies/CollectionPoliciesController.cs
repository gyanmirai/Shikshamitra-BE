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
[Route("api/finance/collection-policies")]
[Authorize]
public class CollectionPoliciesController(ICollectionPolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<CollectionPolicyDto>> GetListAsync([FromQuery] CollectionPolicyListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<CollectionPolicyDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpPost]
	public Task<CollectionPolicyDto> CreateAsync([FromBody] CreateUpdateCollectionPolicyDto input) =>
		appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<CollectionPolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdateCollectionPolicyDto input) =>
		appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/clone")]
	public Task<CollectionPolicyDto> CloneAsync(Guid id, [FromBody] CollectionPolicyCloneRequestDto input) =>
		appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task ArchiveAsync(Guid id) =>
		appService.ArchiveAsync(id);

	[HttpPost("{id:guid}/unarchive")]
	public Task<CollectionPolicyDto> UnarchiveAsync(Guid id) =>
		appService.UnarchiveAsync(id);

	[HttpGet("{id:guid}/versions")]
	public Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, [FromQuery] FinancePolicyVersionListRequestDto input) =>
		appService.GetVersionsAsync(id, input);

	[HttpPost("{id:guid}/simulate")]
	public Task<CollectionPolicySimulateResponseDto> SimulateAsync(Guid id, [FromBody] CollectionPolicySimulateRequestDto input) =>
		appService.SimulateAsync(id, input);
}
