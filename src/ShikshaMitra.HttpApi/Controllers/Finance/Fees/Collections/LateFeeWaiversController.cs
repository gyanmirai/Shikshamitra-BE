using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Fees.Collections;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/finance/late-fee-waivers")]
[Authorize]
public class LateFeeWaiversController(ILateFeeWaiverAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<List<LateFeeWaiverApplicationDto>> GetForDueFeeAsync([FromQuery] Guid dueFeeId) =>
		appService.GetWaiversForDueFeeAsync(dueFeeId);

	[HttpPost]
	public Task<LateFeeWaiverApplicationDto> ApplyAsync([FromBody] ApplyLateFeeWaiverInput input) =>
		appService.ApplyWaiverAsync(input);

	[HttpPost("{id:guid}/approve")]
	public Task<LateFeeWaiverApplicationDto> ApproveAsync(Guid id) =>
		appService.ApproveAsync(id);

	[HttpPost("{id:guid}/reject")]
	public Task<LateFeeWaiverApplicationDto> RejectAsync(Guid id) =>
		appService.RejectAsync(id);
}
