using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class AttendancePolicyAppService(
	IRepository<AttendancePolicy, Guid> repository,
	AttendancePolicyMutationService mutationService)
	: ApplicationService, IAttendancePolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<AttendancePolicyDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Name.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsDefault)
			.ThenBy(x => x.Name)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		return new PagedResultDto<AttendancePolicyDto>(total, rows.Select(HrPolicyMapping.ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<AttendancePolicyDto> GetAsync(Guid id) =>
		HrPolicyMapping.ToDto(await repository.GetAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Add)]
	public Task<AttendancePolicyDto> CreateAsync(CreateUpdateAttendancePolicyDto input) =>
		mutationService.CreateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Edit)]
	public Task<AttendancePolicyDto> UpdateAsync(Guid id, CreateUpdateAttendancePolicyDto input) =>
		mutationService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public Task<AttendancePolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input) =>
		mutationService.CloneAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public Task DeleteAsync(Guid id) =>
		mutationService.DeleteAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public Task<AttendancePolicyDto> UnarchiveAsync(Guid id) =>
		mutationService.UnarchiveAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.SetDefault)]
	public Task<AttendancePolicyDto> SetDefaultAsync(Guid id) =>
		mutationService.SetDefaultAsync(id);
}
