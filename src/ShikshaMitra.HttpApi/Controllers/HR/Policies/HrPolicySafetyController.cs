using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/policies")]
[Authorize]
public class HrPolicySafetyController(IHrPolicySafetyAppService appService) : AbpControllerBase
{
	[HttpPost("impact-preview")]
	public Task<HrPolicyImpactPreviewDto> PreviewPolicyImpactAsync([FromBody] HrPolicyImpactPreviewRequestDto input) =>
		appService.PreviewPolicyImpactAsync(input);

	[HttpGet("{policyType}/{id:guid}/audit-summary")]
	public Task<HrPolicyAuditSummaryDto> GetAuditSummaryAsync(string policyType, Guid id) =>
		appService.GetAuditSummaryAsync(policyType, id);
}
