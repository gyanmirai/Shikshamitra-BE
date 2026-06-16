using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.HR.Reports;

public class HrReportRecipientLookupService(
	IRepository<IdentityUser, Guid> userRepository,
	IIdentityUserRepository identityUserRepository,
	IRepository<IdentityRole, Guid> roleRepository,
	IRepository<PermissionGrant, Guid> permissionGrantRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<HrReportRecipientDto>> GetRecipientsAsync(string? filter, int maxResultCount, string? ids = null)
	{
		var take = Math.Clamp(maxResultCount <= 0 ? 20 : maxResultCount, 1, 50);
		var requestedUserIds = ParseRecipientIds(ids);
		var eligibleUserIds = await GetReportViewUserIdsAsync();
		if (eligibleUserIds.Count == 0)
		{
			return [];
		}

		var query = await userRepository.GetQueryableAsync();
		var eligibleUsers = query.Where(user => eligibleUserIds.Contains(user.Id));
		List<IdentityUser> requestedUsers = [];
		if (requestedUserIds.Count > 0)
		{
			requestedUsers = await asyncExecuter.ToListAsync(
				eligibleUsers
					.Where(user => requestedUserIds.Contains(user.Id))
					.OrderBy(user => user.Name)
					.ThenBy(user => user.Surname)
					.ThenBy(user => user.UserName));
		}

		query = eligibleUsers.Where(user => !requestedUserIds.Contains(user.Id));
		if (!string.IsNullOrWhiteSpace(filter))
		{
			var search = filter.Trim().ToLowerInvariant();
			query = query.Where(user =>
				user.UserName.ToLower().Contains(search) ||
				(user.Name != null && user.Name.ToLower().Contains(search)) ||
				(user.Surname != null && user.Surname.ToLower().Contains(search)) ||
				(user.Email != null && user.Email.ToLower().Contains(search)));
		}

		var searchUsers = await asyncExecuter.ToListAsync(
			query
				.OrderBy(user => user.Name)
				.ThenBy(user => user.Surname)
				.ThenBy(user => user.UserName)
				.Take(take));

		return requestedUsers
			.Concat(searchUsers)
			.DistinctBy(user => user.Id)
			.Select(user => new HrReportRecipientDto
			{
				Id = user.Id,
				DisplayName = BuildDisplayName(user),
				Email = user.Email
			})
			.ToList();
	}

	private static List<Guid> ParseRecipientIds(string? ids) =>
		string.IsNullOrWhiteSpace(ids)
			? []
			: ids
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(value => Guid.TryParse(value, out var userId) ? userId : Guid.Empty)
				.Where(userId => userId != Guid.Empty)
				.Distinct()
				.ToList();

	public async Task<bool> HasReportViewPermissionAsync(Guid userId)
	{
		var user = await identityUserRepository.FindAsync(userId, includeDetails: true);
		if (user == null)
		{
			return false;
		}

		var permissionGrants = await GetReportViewPermissionGrantsAsync();
		if (permissionGrants.Any(x =>
			x.ProviderName == "U" &&
			string.Equals(x.ProviderKey, userId.ToString("D"), StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}

		var grantedRoleKeys = permissionGrants
			.Where(x => x.ProviderName == "R")
			.Select(x => x.ProviderKey)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Cast<string>()
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (grantedRoleKeys.Count == 0 || user.Roles.Count == 0)
		{
			return false;
		}

		var roleIds = user.Roles.Select(x => x.RoleId).Distinct().ToList();
		var roles = await roleRepository.GetListAsync(role => roleIds.Contains(role.Id));
		foreach (var roleRef in user.Roles)
		{
			var role = roles.FirstOrDefault(x => x.Id == roleRef.RoleId);
			if (role != null && grantedRoleKeys.Contains(role.Name))
			{
				return true;
			}
		}

		return false;
	}

	private async Task<HashSet<Guid>> GetReportViewUserIdsAsync()
	{
		var permissionGrants = await GetReportViewPermissionGrantsAsync();
		var userIds = new HashSet<Guid>();
		foreach (var providerKey in permissionGrants
			.Where(x => x.ProviderName == "U")
			.Select(x => x.ProviderKey))
		{
			if (Guid.TryParse(providerKey, out var userId) && userId != Guid.Empty)
			{
				userIds.Add(userId);
			}
		}

		var grantedRoleNames = permissionGrants
			.Where(x => x.ProviderName == "R")
			.Select(x => x.ProviderKey)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Cast<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (grantedRoleNames.Count == 0)
		{
			return userIds;
		}

		var roles = await roleRepository.GetListAsync(role => grantedRoleNames.Contains(role.Name));
		var roleIds = roles.Select(x => x.Id).Distinct().ToList();
		if (roleIds.Count == 0)
		{
			return userIds;
		}

		var query = await userRepository.GetQueryableAsync();
		var roleUserIds = await asyncExecuter.ToListAsync(
			query
				.Where(user => user.Roles.Any(role => roleIds.Contains(role.RoleId)))
				.Select(user => user.Id));

		foreach (var roleUserId in roleUserIds)
		{
			userIds.Add(roleUserId);
		}

		return userIds;
	}

	private Task<List<PermissionGrant>> GetReportViewPermissionGrantsAsync() =>
		permissionGrantRepository.GetListAsync(x =>
			x.Name == ShikshaMitraSubmodulePermissions.HR.Reports.View &&
			(x.ProviderName == "U" || x.ProviderName == "R"));

	private static string BuildDisplayName(IdentityUser user)
	{
		var name = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
		return string.IsNullOrWhiteSpace(name) ? user.UserName : name;
	}
}
