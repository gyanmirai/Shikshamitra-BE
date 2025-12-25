using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;

namespace ShikshaMitra.Security.Permissions;

/// <summary>
/// Custom permission value provider that grants all permissions to admin users.
/// This implements the admin bypass pattern where admin role users automatically
/// have access to all resources regardless of explicit permission assignments.
/// </summary>
public class AdminPermissionValueProvider : PermissionValueProvider
{
	public const string ProviderName = "Admin";

	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly UserScopeOptions _options;

	public override string Name => ProviderName;

	public AdminPermissionValueProvider(
		IPermissionStore permissionStore,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		IOptions<UserScopeOptions> options) 
		: base(permissionStore)
	{
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_options = options.Value;
	}

	/// <summary>
	/// Checks if the current user has admin privileges and grants the permission if true.
	/// This provider has the highest priority to ensure admin users bypass all permission checks.
	/// </summary>
	public override async Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
	{
		// If no user ID, cannot be admin
		var userIdClaim = context.Principal?.FindFirst(AbpClaimTypes.UserId);
		if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
		{
			return PermissionGrantResult.Undefined;
		}

		// Check if user has admin role
		var isAdmin = await IsAdminUserAsync(userId);
		if (isAdmin)
		{
			// Admin users are granted ALL permissions
			return PermissionGrantResult.Granted;
		}

		// Not an admin, let other providers handle it
		return PermissionGrantResult.Undefined;
	}

	/// <summary>
	/// Checks if the specified user has any of the configured admin roles.
	/// </summary>
	private async Task<bool> IsAdminUserAsync(Guid userId)
	{
		try
		{
			var user = await _userRepository.GetAsync(userId, includeDetails: true);
			var roleIds = user.Roles.Select(r => r.RoleId).ToList();

			if (!roleIds.Any())
			{
				return false;
			}

			// Get role names from role repository - only fetch roles matching the user's role IDs
			var allRoles = await _roleRepository.GetListAsync();
			var userRoleNames = allRoles
				.Where(r => roleIds.Contains(r.Id))
				.Select(r => r.Name)
				.ToList();

			// Check if user has any of the configured admin roles
			return _options.AdminRoles.Any(adminRole =>
				userRoleNames.Contains(adminRole, StringComparer.OrdinalIgnoreCase));
		}
		catch (Exception)
		{
			// If we can't determine admin status, don't grant permission
			return false;
		}
	}

	/// <summary>
	/// Not applicable for admin bypass - all permissions are always granted to admins.
	/// </summary>
	public override async Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
	{
		var result = new MultiplePermissionGrantResult(context.Permissions.Select(x => x.Name).ToArray());

		// If no user ID, cannot be admin
		var userIdClaim = context.Principal?.FindFirst(AbpClaimTypes.UserId);
		if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
		{
			return result;
		}

		// Check if user has admin role
		var isAdmin = await IsAdminUserAsync(userId);
		if (isAdmin)
		{
			// Admin users are granted ALL permissions
			foreach (var permission in context.Permissions)
			{
				result.Result[permission.Name] = PermissionGrantResult.Granted;
			}
		}

		return result;
	}
}
