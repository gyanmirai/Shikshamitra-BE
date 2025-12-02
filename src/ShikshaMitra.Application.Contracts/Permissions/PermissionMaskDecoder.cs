using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ShikshaMitra.Permissions;

/// <summary>
/// Utility for decoding permission bitmasks from JWT claims.
/// Supports validation and lookup by permission name.
/// </summary>
public static class PermissionMaskDecoder
{
	/// <summary>
	/// Checks if a specific permission is granted based on the bitmask.
	/// </summary>
	/// <param name="mask">Bit-string mask from claims (e.g., "101010")</param>
	/// <param name="permissions">Array of permission names in same order as mask was built</param>
	/// <param name="permissionName">Permission to check</param>
	/// <returns>True if granted, false otherwise</returns>
	public static bool HasPermission(string mask, string[] permissions, string permissionName)
	{
		if (string.IsNullOrEmpty(mask) || permissions == null || string.IsNullOrEmpty(permissionName))
		{
			return false;
		}

		// Validate mask length
		if (mask.Length != permissions.Length)
		{
			// Length mismatch indicates version incompatibility
			return false;
		}

		// Find permission index
		var index = Array.IndexOf(permissions, permissionName);
		if (index < 0 || index >= mask.Length)
		{
			return false;
		}

		// Check bit at index
		return mask[index] == '1';
	}

	/// <summary>
	/// Decodes a bitmask into a set of granted permission names for fast lookups.
	/// </summary>
	/// <param name="mask">Bit-string mask from claims</param>
	/// <param name="permissions">Array of permission names</param>
	/// <returns>HashSet of granted permission names, or empty set if validation fails</returns>
	public static HashSet<string> DecodeToSet(string mask, string[] permissions)
	{
		var granted = new HashSet<string>(StringComparer.Ordinal);

		if (string.IsNullOrEmpty(mask) || permissions == null)
		{
			return granted;
		}

		// Validate mask length
		if (mask.Length != permissions.Length)
		{
			return granted;
		}

		// Collect granted permissions
		for (int i = 0; i < mask.Length; i++)
		{
			if (mask[i] == '1')
			{
				granted.Add(permissions[i]);
			}
		}

		return granted;
	}

	/// <summary>
	/// Extracts mask and version from claims principal.
	/// </summary>
	/// <param name="principal">Claims principal</param>
	/// <param name="maskClaimType">Claim type for mask (e.g., "module_mask")</param>
	/// <param name="versionClaimType">Claim type for version (e.g., "module_mask_v")</param>
	/// <param name="expectedVersion">Expected version for validation</param>
	/// <returns>Tuple of (mask, version, isValid)</returns>
	public static (string? Mask, string? Version, bool IsValid) ExtractMaskFromClaims(
		ClaimsPrincipal principal,
		string maskClaimType,
		string versionClaimType,
		string expectedVersion)
	{
		if (principal == null)
		{
			return (null, null, false);
		}

		var mask = principal.FindFirst(maskClaimType)?.Value;
		var version = principal.FindFirst(versionClaimType)?.Value;

		var isValid = !string.IsNullOrEmpty(mask)
			&& !string.IsNullOrEmpty(version)
			&& version == expectedVersion;

		return (mask, version, isValid);
	}

	/// <summary>
	/// Gets all granted permissions from multiple masks.
	/// </summary>
	/// <param name="principal">Claims principal</param>
	/// <param name="maskConfigs">Configuration for each mask type</param>
	/// <returns>Combined set of all granted permissions</returns>
	public static HashSet<string> GetAllGrantedPermissions(
		ClaimsPrincipal principal,
		params MaskConfig[] maskConfigs)
	{
		var allGranted = new HashSet<string>(StringComparer.Ordinal);

		foreach (var config in maskConfigs)
		{
			var (mask, version, isValid) = ExtractMaskFromClaims(
				principal,
				config.MaskClaimType,
				config.VersionClaimType,
				config.ExpectedVersion);

			if (isValid && mask != null)
			{
				var granted = DecodeToSet(mask, config.Permissions);
				allGranted.UnionWith(granted);
			}
		}

		return allGranted;
	}
}

/// <summary>
/// Configuration for a permission mask type.
/// </summary>
public record MaskConfig(
	string MaskClaimType,
	string VersionClaimType,
	string ExpectedVersion,
	string[] Permissions);
