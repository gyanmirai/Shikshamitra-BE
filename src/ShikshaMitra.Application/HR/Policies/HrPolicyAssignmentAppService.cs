using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicyAssignmentAppService(
	HrPolicyAssignmentReader reader,
	HrPolicyAssignmentWorkflow workflow)
	: ApplicationService, IHrPolicyAssignmentAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<PagedResultDto<HrPolicyAssignmentDto>> GetListAsync(PolicyListRequestDto input) =>
		reader.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<HrPolicyAssignmentDto> GetAsync(Guid id) =>
		reader.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public Task<HrPolicyAssignmentDto> CreateAsync(CreateUpdateHrPolicyAssignmentDto input) =>
		workflow.CreateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public Task<HrPolicyAssignmentDto> UpdateAsync(Guid id, CreateUpdateHrPolicyAssignmentDto input) =>
		workflow.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideAsync(UpsertEmployeePolicyOverrideDto input) =>
		workflow.UpsertEmployeeOverrideAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public Task DisableEmployeeOverrideAsync(DisableEmployeePolicyOverrideDto input) =>
		workflow.DisableEmployeeOverrideAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input) =>
		workflow.PreviewImpactAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Assign)]
	public Task DeleteAsync(Guid id) =>
		workflow.DeleteAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<EffectiveEmployeePoliciesDto> GetEffectivePoliciesAsync(Guid employeeId, DateTime date) =>
		reader.GetEffectivePoliciesAsync(employeeId, date);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<EffectiveEmployeePoliciesDto> GetPreviewAsync(PolicyAssignmentPreviewRequestDto input) =>
		reader.GetPreviewAsync(input);
}
