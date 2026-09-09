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
[Route("api/finance/refund-policies")]
[Authorize]
public class RefundPoliciesController(IRefundPolicyAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<RefundPolicyDto>> GetListAsync([FromQuery] RefundPolicyListRequestDto input) => appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<RefundPolicyDto> GetAsync(Guid id) => appService.GetAsync(id);

	[HttpGet("{id:guid}/impact")]
	public Task<RefundPolicyImpactDto> GetImpactAsync(Guid id) => appService.GetImpactAsync(id);

	[HttpPost]
	public Task<RefundPolicyDto> CreateAsync([FromBody] CreateUpdateRefundPolicyDto input) => appService.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<RefundPolicyDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRefundPolicyDto input) => appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/clone")]
	public Task<RefundPolicyDto> CloneAsync(Guid id, [FromBody] RefundPolicyCloneRequestDto input) => appService.CloneAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task ArchiveAsync(Guid id) => appService.ArchiveAsync(id);

	[HttpPost("{id:guid}/unarchive")]
	public Task<RefundPolicyDto> UnarchiveAsync(Guid id) => appService.UnarchiveAsync(id);

	[HttpGet("{id:guid}/versions")]
	public Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, [FromQuery] FinancePolicyVersionListRequestDto input) => appService.GetVersionsAsync(id, input);

	[HttpPost("{id:guid}/simulate")]
	public Task<RefundPolicySimulateResponseDto> SimulateAsync(Guid id, [FromBody] RefundPolicySimulateRequestDto input) => appService.SimulateAsync(id, input);
}
