using ShikshaMitra.Security.OrganizationUnits.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Security.OrganizationUnits;

public interface IOrganizationUnitAppService : IApplicationService
{
	Task<List<TreeOutput>> GetTreeAsync();

	Task CreateAsync(CreateOrganizationUnitInput input);


	Task DeleteAsync(Guid input);


	Task UpdateAsync(UpdateOrganizationUnitInput input);


	Task AddRoleToOrganizationUnitAsync(AddRoleToOrganizationUnitInput input);


	Task RemoveRoleFromOrganizationUnitAsync(RemoveRoleToOrganizationUnitInput input);


	Task AddUserToOrganizationUnitAsync(AddUserToOrganizationUnitInput input);


	Task RemoveUserFromOrganizationUnitAsync(RemoveUserToOrganizationUnitInput input);


	Task<PagedResultDto<GetOrganizationUnitUserOutput>> GetUsersAsync(GetOrganizationUnitUserInput input);


	Task<PagedResultDto<GetOrganizationUnitRoleOutput>> GetRolesAsync(GetOrganizationUnitRoleInput input);


	Task<PagedResultDto<GetUnAddUserOutput>> GetUnAddUsersAsync(GetUnAddUserInput input);


	Task<PagedResultDto<GetUnAddRoleOutput>> GetUnAddRolessAsync(GetUnAddRoleInput input);
}