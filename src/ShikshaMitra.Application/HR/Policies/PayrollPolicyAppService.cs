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
public class PayrollPolicyAppService(
	PayrollPolicyReader reader,
	PayrollPolicyWorkflow workflow) : ApplicationService, IPayrollPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<PagedResultDto<PayrollPolicyDto>> GetListAsync(PolicyListRequestDto input) =>
		reader.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public Task<PayrollPolicyDto> GetAsync(Guid id) =>
		reader.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Add)]
	public Task<PayrollPolicyDto> CreateAsync(CreateUpdatePayrollPolicyDto input) =>
		workflow.CreateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Edit)]
	public Task<PayrollPolicyDto> UpdateAsync(Guid id, CreateUpdatePayrollPolicyDto input) =>
		workflow.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public Task<PayrollPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input) =>
		workflow.CloneAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public Task DeleteAsync(Guid id) =>
		workflow.DeleteAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public Task<PayrollPolicyDto> UnarchiveAsync(Guid id) =>
		workflow.UnarchiveAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.SetDefault)]
	public Task<PayrollPolicyDto> SetDefaultAsync(Guid id) =>
		workflow.SetDefaultAsync(id);
}
