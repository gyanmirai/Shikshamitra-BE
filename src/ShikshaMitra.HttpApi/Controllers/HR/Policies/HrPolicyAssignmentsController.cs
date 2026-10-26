using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/policy-assignments")]
[Authorize]
public class HrPolicyAssignmentsController(IHrPolicyAssignmentAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<PagedResultDto<HrPolicyAssignmentDto>> GetListAsync([FromQuery] PolicyListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<HrPolicyAssignmentDto> GetAsync(Guid id) =>
		appService.GetAsync(id);

	[HttpPost]
	public Task<HrPolicyAssignmentDto> CreateAsync([FromBody] CreateUpdateHrPolicyAssignmentDto input) =>
		appService.CreateAsync(input);

	[HttpPost("direct-employee-override")]
	public Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideAsync([FromBody] UpsertEmployeePolicyOverrideDto input) =>
		appService.UpsertEmployeeOverrideAsync(input);

	[HttpPost("direct-employee-override/disable")]
	public Task DisableEmployeeOverrideAsync([FromBody] DisableEmployeePolicyOverrideDto input) =>
		appService.DisableEmployeeOverrideAsync(input);

	[HttpPost("impact-preview")]
	public Task<HrPolicyImpactPreviewDto> PreviewImpactAsync([FromBody] HrPolicyAssignmentImpactPreviewRequestDto input) =>
		appService.PreviewImpactAsync(input);

	[HttpPost("resolution-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public Task<PolicyAssignmentResolutionPreviewDto> PreviewResolutionAsync([FromBody] PolicyAssignmentResolutionPreviewRequestDto input) =>
		appService.PreviewResolutionAsync(input);

	[HttpPut("{id:guid}")]
	public Task<HrPolicyAssignmentDto> UpdateAsync(Guid id, [FromBody] CreateUpdateHrPolicyAssignmentDto input) =>
		appService.UpdateAsync(id, input);

	[HttpDelete("{id:guid}")]
	public Task DeleteAsync(Guid id) =>
		appService.DeleteAsync(id);
}