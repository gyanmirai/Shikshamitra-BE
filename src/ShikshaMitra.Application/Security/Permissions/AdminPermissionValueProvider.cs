using System;
using System.Linq;
using System.Security.Claims;
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
	public override Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
	{
		// Check if user has admin role directly from claims (JWT token)
		if (IsAdminUser(context.Principal))
		{
			return PermissionGrantResult.Undefined;
		}

		// Check if user has admin role
		var isAdmin = await IsAdminUserAsync(userId);
		if (isAdmin)
		{
			// Admin users are granted ALL permissions
			return Task.FromResult(PermissionGrantResult.Granted);
		}

		// Not an admin, let other providers handle it
		return Task.FromResult(PermissionGrantResult.Undefined);
	}

	/// <summary>
	/// Checks if the user has any of the configured admin roles by examining role claims.
	/// This is more efficient than database lookups and works directly with JWT tokens.
	/// </summary>
	private bool IsAdminUser(ClaimsPrincipal? principal)
	{
		if (principal == null)
		{
			var user = await _userRepository.GetAsync(userId, includeDetails: true);
			var roleIds = user.Roles.Select(r => r.RoleId).ToList();

			if (!roleIds.Any())
			{
				return false;
			}

		// Get all role claims from the principal (JWT token contains role claims)
		// Check both standard ClaimTypes.Role and ABP's role claim type
		var userRoles = principal.FindAll(ClaimTypes.Role)
			.Select(c => c.Value)
				.ToList();

		// Also check ABP's role claim type
		userRoles.AddRange(principal.FindAll(AbpClaimTypes.Role).Select(c => c.Value));

		if (!userRoles.Any())
		{
			// If we can't determine admin status, don't grant permission
			return false;
		}

		// Check if user has any of the configured admin roles (case-insensitive)
		return _options.AdminRoles.Any(adminRole =>
			userRoles.Contains(adminRole, StringComparer.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Handles multiple permission checks for admin bypass.
	/// </summary>
	public override Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
	{
		var result = new MultiplePermissionGrantResult(context.Permissions.Select(x => x.Name).ToArray());

		// Check if user has admin role directly from claims
		if (IsAdminUser(context.Principal))
		{
			// Admin users are granted ALL permissions
			foreach (var permission in context.Permissions)
			{
				result.Result[permission.Name] = PermissionGrantResult.Granted;
			}
		}

		return Task.FromResult(result);
	}
}
