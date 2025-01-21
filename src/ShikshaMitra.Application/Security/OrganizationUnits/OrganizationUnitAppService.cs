using ShikshaMitra.Security.OrganizationUnits.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace ShikshaMitra.Security.OrganizationUnits;

public class OrganizationUnitAppService(
	OrganizationUnitManager organizationUnitManager,
	IdentityUserManager identityUserManager,
	IOrganizationUnitRepository organizationUnitRepository)
	: ShikshaMitraAppService, IOrganizationUnitAppService
{
	public virtual async Task<List<TreeOutput>> GetTreeAsync()
	{
		var organizationUnits = await organizationUnitRepository.GetListAsync();
		var organizationUnitDtos =
			ObjectMapper.Map<List<OrganizationUnit>, List<OrganizationUnitDto>>(organizationUnits);
		return ConvertToTree(organizationUnitDtos);
	}

	public virtual async Task CreateAsync(CreateOrganizationUnitInput input)
	{
		var entity = new OrganizationUnit
		(
			GuidGenerator.Create(),
			input.DisplayName,
			input.ParentId,
			CurrentTenant.Id
		);
		await organizationUnitManager.CreateAsync(entity);
	}

	public virtual Task DeleteAsync(Guid input)
	{
		return organizationUnitManager.DeleteAsync(input);
	}

	public virtual async Task UpdateAsync(UpdateOrganizationUnitInput input)
	{
		var entity = await organizationUnitRepository.FindAsync(input.Id);
		if (entity != null)
		{
			entity.DisplayName = input.DisplayName;
			await organizationUnitManager.UpdateAsync(entity);
		}
	}

	public virtual async Task AddRoleToOrganizationUnitAsync(AddRoleToOrganizationUnitInput input)
	{
		foreach (var roleId in input.RoleId)
		{
			await organizationUnitManager.AddRoleToOrganizationUnitAsync(roleId, input.OrganizationUnitId);
		}
	}

	public virtual async Task RemoveRoleFromOrganizationUnitAsync(RemoveRoleToOrganizationUnitInput input)
	{
		await organizationUnitManager.RemoveRoleFromOrganizationUnitAsync(input.RoleId, input.OrganizationUnitId);
	}

	public virtual async Task AddUserToOrganizationUnitAsync(AddUserToOrganizationUnitInput input)
	{
		foreach (var userId in input.UserId)
		{
			await identityUserManager.AddToOrganizationUnitAsync(userId, input.OrganizationUnitId);
		}
	}

	public virtual async Task RemoveUserFromOrganizationUnitAsync(RemoveUserToOrganizationUnitInput input)
	{
		await identityUserManager.RemoveFromOrganizationUnitAsync(input.UserId, input.OrganizationUnitId);
	}

	public virtual async Task<PagedResultDto<GetOrganizationUnitUserOutput>> GetUsersAsync(
		GetOrganizationUnitUserInput input)
	{
		var listResult = new List<GetOrganizationUnitUserOutput>();
		var organizationUnit = await organizationUnitRepository.FindAsync(input.OrganizationUnitId);
		if (organizationUnit == null)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.OrganizationUnitNotExist);

		var count = await organizationUnitRepository.GetMembersCountAsync(organizationUnit, filter: input.Filter);
		if (count > 0)
		{
			var list = await organizationUnitRepository.GetMembersAsync
			(
				organizationUnit,
				maxResultCount: input.PageSize,
				skipCount: input.SkipCount,
				filter: input.Filter
			);
			listResult = ObjectMapper.Map<List<IdentityUser>, List<GetOrganizationUnitUserOutput>>(list);
		}

		return new PagedResultDto<GetOrganizationUnitUserOutput>(count, listResult);
	}

	public virtual async Task<PagedResultDto<GetUnAddUserOutput>> GetUnAddUsersAsync(GetUnAddUserInput input)
	{
		var listResult = new List<GetUnAddUserOutput>();
		var organizationUnit = await organizationUnitRepository.FindAsync(input.OrganizationUnitId);
		if (organizationUnit == null)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.OrganizationUnitNotExist);
		var count = await organizationUnitRepository.GetUnaddedUsersCountAsync(organizationUnit, input.Filter);
		if (count > 0)
		{
			var users = await organizationUnitRepository.GetUnaddedUsersAsync
			(
				organizationUnit,
				maxResultCount: input.PageSize,
				skipCount: input.SkipCount,
				filter: input.Filter
			);
			listResult = ObjectMapper.Map<List<IdentityUser>, List<GetUnAddUserOutput>>(users);
		}

		return new PagedResultDto<GetUnAddUserOutput>(count, listResult);
	}

	public virtual async Task<PagedResultDto<GetOrganizationUnitRoleOutput>> GetRolesAsync(
		GetOrganizationUnitRoleInput input)
	{
		var listResult = new List<GetOrganizationUnitRoleOutput>();
		var organizationUnit = await organizationUnitRepository.FindAsync(input.OrganizationUnitId);
		if (organizationUnit == null)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.OrganizationUnitNotExist);

		var count = await organizationUnitRepository.GetRolesCountAsync(organizationUnit);
		if (count > 0)
		{
			var list = await organizationUnitRepository.GetRolesAsync(organizationUnit, maxResultCount: input.PageSize,
				skipCount: input.SkipCount);
			listResult = ObjectMapper.Map<List<IdentityRole>, List<GetOrganizationUnitRoleOutput>>(list);
		}

		return new PagedResultDto<GetOrganizationUnitRoleOutput>(count, listResult);
	}

	public virtual async Task<PagedResultDto<GetUnAddRoleOutput>> GetUnAddRolessAsync(GetUnAddRoleInput input)
	{
		var listResult = new List<GetUnAddRoleOutput>();
		var organizationUnit = await organizationUnitRepository.FindAsync(input.OrganizationUnitId);
		if (organizationUnit == null)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.OrganizationUnitNotExist);
		var count = await organizationUnitRepository.GetUnaddedRolesCountAsync(organizationUnit, input.Filter);
		if (count > 0)
		{
			var roles = await organizationUnitRepository.GetUnaddedRolesAsync
			(
				organizationUnit,
				maxResultCount: input.PageSize,
				skipCount: input.SkipCount,
				filter: input.Filter
			);
			listResult = ObjectMapper.Map<List<IdentityRole>, List<GetUnAddRoleOutput>>(roles);
		}

		return new PagedResultDto<GetUnAddRoleOutput>(count, listResult);
	}


	private List<TreeOutput> ConvertToTree(
		List<OrganizationUnitDto> list,
		Guid? Id = null)
	{
		var childList = Children(list, Id);

		return childList.Select(item =>
				new TreeOutput { Key = item.Id, Title = item.DisplayName, Children = ConvertToTree(list, item.Id) })
			.ToList();
	}

	private List<OrganizationUnitDto> Children(
		List<OrganizationUnitDto> list,
		Guid? Id)
	{
		var childList = list.Where(x => x.ParentId == Id).ToList();
		return childList;
	}
}