using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Security.OrganizationUnits;
using ShikshaMitra.Security.OrganizationUnits.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Security;

[Microsoft.AspNetCore.Components.Route("OrganizationUnits")]
public class OrganizationUnitController : AbpController, IOrganizationUnitAppService
{
	private readonly IOrganizationUnitAppService _organizationUnitAppService;

	public OrganizationUnitController(IOrganizationUnitAppService organizationUnitAppService)
	{
		_organizationUnitAppService = organizationUnitAppService;
	}

	[HttpPost("tree")]
	public Task<List<TreeOutput>> GetTreeAsync()
	{
		return _organizationUnitAppService.GetTreeAsync();
	}

	[HttpPost("create")]
	public Task CreateAsync(CreateOrganizationUnitInput input)
	{
		return _organizationUnitAppService.CreateAsync(input);
	}

	[HttpPost("delete")]
	public Task DeleteAsync(Guid input)
	{
		return _organizationUnitAppService.DeleteAsync(input);
	}

	[HttpPost("update")]
	public Task UpdateAsync(UpdateOrganizationUnitInput input)
	{
		return _organizationUnitAppService.UpdateAsync(input);
	}

	[HttpPost("addRoleToOrganizationUnitAsync")]
	public Task AddRoleToOrganizationUnitAsync(AddRoleToOrganizationUnitInput input)
	{
		return _organizationUnitAppService.AddRoleToOrganizationUnitAsync(input);
	}

	[HttpPost("removeRoleFromOrganizationUnitAsync")]
	public Task RemoveRoleFromOrganizationUnitAsync(RemoveRoleToOrganizationUnitInput input)
	{
		return _organizationUnitAppService.RemoveRoleFromOrganizationUnitAsync(input);
	}

	[HttpPost("addUserToOrganizationUnit")]
	public Task AddUserToOrganizationUnitAsync(AddUserToOrganizationUnitInput input)
	{
		return _organizationUnitAppService.AddUserToOrganizationUnitAsync(input);
	}

	[HttpPost("removeUserFromOrganizationUnit")]
	public Task RemoveUserFromOrganizationUnitAsync(RemoveUserToOrganizationUnitInput input)
	{
		return _organizationUnitAppService.RemoveUserFromOrganizationUnitAsync(input);
	}

	[HttpPost("getUsers")]
	public Task<PagedResultDto<GetOrganizationUnitUserOutput>> GetUsersAsync(GetOrganizationUnitUserInput input)
	{
		return _organizationUnitAppService.GetUsersAsync(input);
	}

	[HttpPost("getRoles")]
	public Task<PagedResultDto<GetOrganizationUnitRoleOutput>> GetRolesAsync(GetOrganizationUnitRoleInput input)
	{
		return _organizationUnitAppService.GetRolesAsync(input);
	}

	[HttpPost("getUnAddUsers")]
	public Task<PagedResultDto<GetUnAddUserOutput>> GetUnAddUsersAsync(GetUnAddUserInput input)
	{
		return _organizationUnitAppService.GetUnAddUsersAsync(input);
	}

	[HttpPost("getUnAddRoles")]
	public Task<PagedResultDto<GetUnAddRoleOutput>> GetUnAddRolessAsync(GetUnAddRoleInput input)
	{
		return _organizationUnitAppService.GetUnAddRolessAsync(input);
	}
}