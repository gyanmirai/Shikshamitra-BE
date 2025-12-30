using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Security.OrganizationUnits;
using ShikshaMitra.Security.OrganizationUnits.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Security.OrganizationUnits;

[Route("api/organization-units")]
public class OrganizationUnitController(IOrganizationUnitAppService organizationUnitAppService)
	: AbpController
{
	[HttpGet("tree")]
	public Task<List<TreeOutput>> GetOrganizationUnitTreeAsync()
	{
		return organizationUnitAppService.GetTreeAsync();
	}

	[HttpPost("create")]
	public Task CreateOrganizationUnitAsync(CreateOrganizationUnitInput input)
	{
		return organizationUnitAppService.CreateAsync(input);
	}

	[HttpDelete("delete/{id}")]
	public Task DeleteOrganizationUnitAsync(Guid id)
	{
		return organizationUnitAppService.DeleteAsync(id);
	}

	[HttpPut("update")]
	public Task UpdateOrganizationUnitAsync(UpdateOrganizationUnitInput input)
	{
		return organizationUnitAppService.UpdateAsync(input);
	}

	[HttpPost("roles/add")]
	public Task AddRoleToOrganizationUnitAsync(AddRoleToOrganizationUnitInput input)
	{
		return organizationUnitAppService.AddRoleToOrganizationUnitAsync(input);
	}

	[HttpPost("roles/remove")]
	public Task RemoveRoleFromOrganizationUnitAsync(RemoveRoleToOrganizationUnitInput input)
	{
		return organizationUnitAppService.RemoveRoleFromOrganizationUnitAsync(input);
	}

	[HttpPost("users/add")]
	public Task AddUserToOrganizationUnitAsync(AddUserToOrganizationUnitInput input)
	{
		return organizationUnitAppService.AddUserToOrganizationUnitAsync(input);
	}

	[HttpPost("users/remove")]
	public Task RemoveUserFromOrganizationUnitAsync(RemoveUserToOrganizationUnitInput input)
	{
		return organizationUnitAppService.RemoveUserFromOrganizationUnitAsync(input);
	}

	[HttpGet("users")]
	public Task<PagedResultDto<GetOrganizationUnitUserOutput>> GetOrganizationUnitUsersAsync(GetOrganizationUnitUserInput input)
	{
		return organizationUnitAppService.GetUsersAsync(input);
	}

	[HttpGet("roles")]
	public Task<PagedResultDto<GetOrganizationUnitRoleOutput>> GetOrganizationUnitRolesAsync(GetOrganizationUnitRoleInput input)
	{
		return organizationUnitAppService.GetRolesAsync(input);
	}

	[HttpPost("users/unadded")]
	public Task<PagedResultDto<GetUnAddUserOutput>> GetUnaddedOrganizationUnitUsersAsync(GetUnAddUserInput input)
	{
		return organizationUnitAppService.GetUnAddUsersAsync(input);
	}

	[HttpPost("roles/unadded")]
	public Task<PagedResultDto<GetUnAddRoleOutput>> GetUnaddedOrganizationUnitRolesAsync(GetUnAddRoleInput input)
	{
		return organizationUnitAppService.GetUnAddRolessAsync(input);
	}
}
