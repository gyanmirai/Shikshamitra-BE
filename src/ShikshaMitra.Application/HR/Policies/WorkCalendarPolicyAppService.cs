using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class WorkCalendarPolicyAppService(
	WorkCalendarPolicyReader reader,
	WorkCalendarPolicyWorkflow workflow)
	: ApplicationService, IWorkCalendarPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<WorkCalendarPolicyDto>> GetListAsync(PolicyListRequestDto input)
	{
		return await reader.GetListAsync(input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<WorkCalendarPolicyDto> GetAsync(Guid id)
	{
		return await reader.GetAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<List<WorkCalendarHolidayDto>> GetHolidaysAsync(Guid id)
	{
		return await reader.GetHolidaysAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<List<WorkCalendarHolidayDto>> GetHolidayImportPreviewAsync(Guid academicYearId)
	{
		return await reader.GetHolidayImportPreviewAsync(academicYearId);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Create)]
	public async Task<WorkCalendarPolicyDto> CreateAsync(CreateUpdateWorkCalendarPolicyDto input)
	{
		return await workflow.CreateAsync(input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Edit)]
	public async Task<WorkCalendarPolicyDto> UpdateAsync(Guid id, CreateUpdateWorkCalendarPolicyDto input)
	{
		return await workflow.UpdateAsync(id, input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public async Task<WorkCalendarPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input)
	{
		return await workflow.CloneAsync(id, input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public async Task DeleteAsync(Guid id)
	{
		await workflow.DeleteAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public async Task<WorkCalendarPolicyDto> UnarchiveAsync(Guid id)
	{
		return await workflow.UnarchiveAsync(id);
	}
}
