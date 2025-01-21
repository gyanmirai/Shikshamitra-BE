using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Security.OrganizationUnits;
using ShikshaMitra.Security.OrganizationUnits.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Security.OrganizationUnits;

[Route("api/organization-unit")]
public class OrganizationUnitController(IOrganizationUnitAppService organizationUnitAppService) : AbpController, IOrganizationUnitAppService
{
	[HttpGet("tree")]
	public Task<List<TreeOutput>> GetTreeAsync()
	{
		return organizationUnitAppService.GetTreeAsync();
	}

	[HttpPost("create")]
	public Task CreateAsync(CreateOrganizationUnitInput input)
	{
		return organizationUnitAppService.CreateAsync(input);
	}

	[HttpDelete("delete")]
	public Task DeleteAsync(Guid input)
	{
		return organizationUnitAppService.DeleteAsync(input);
	}

	[HttpPut("update")]
	public Task UpdateAsync(UpdateOrganizationUnitInput input)
	{
		return organizationUnitAppService.UpdateAsync(input);
	}

	[HttpPost("add-role-to-organization-unit")]
	public Task AddRoleToOrganizationUnitAsync(AddRoleToOrganizationUnitInput input)
	{
		return organizationUnitAppService.AddRoleToOrganizationUnitAsync(input);
	}

	[HttpPost("remove-role-from-organization-unit")]
	public Task RemoveRoleFromOrganizationUnitAsync(RemoveRoleToOrganizationUnitInput input)
	{
		return organizationUnitAppService.RemoveRoleFromOrganizationUnitAsync(input);
	}

	[HttpPost("add-user-to-organization-unit")]
	public Task AddUserToOrganizationUnitAsync(AddUserToOrganizationUnitInput input)
	{
		return organizationUnitAppService.AddUserToOrganizationUnitAsync(input);
	}

	[HttpPost("remove-user-from-organization-unit")]
	public Task RemoveUserFromOrganizationUnitAsync(RemoveUserToOrganizationUnitInput input)
	{
		return organizationUnitAppService.RemoveUserFromOrganizationUnitAsync(input);
	}

	[HttpPost("getUsers")]
	public Task<PagedResultDto<GetOrganizationUnitUserOutput>> GetUsersAsync(GetOrganizationUnitUserInput input)
	{
		return organizationUnitAppService.GetUsersAsync(input);
	}

	[HttpPost("getRoles")]
	public Task<PagedResultDto<GetOrganizationUnitRoleOutput>> GetRolesAsync(GetOrganizationUnitRoleInput input)
	{
		return organizationUnitAppService.GetRolesAsync(input);
	}

	[HttpPost("getUnAddUsers")]
	public Task<PagedResultDto<GetUnAddUserOutput>> GetUnAddUsersAsync(GetUnAddUserInput input)
	{
		return organizationUnitAppService.GetUnAddUsersAsync(input);
	}

	[HttpPost("getUnAddRoles")]
	public Task<PagedResultDto<GetUnAddRoleOutput>> GetUnAddRolessAsync(GetUnAddRoleInput input)
	{
		return organizationUnitAppService.GetUnAddRolessAsync(input);
	}
}