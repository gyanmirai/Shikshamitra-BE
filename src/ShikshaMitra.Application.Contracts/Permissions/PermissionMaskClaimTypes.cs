namespace ShikshaMitra.Permissions;

/// <summary>
/// Constants for permission mask claims in JWTs.
/// Share these values between backend and frontend.
/// </summary>
public static class PermissionMaskClaimTypes
{
	/// <summary>Claim type for module permissions bitmask</summary>
	public const string ModuleMask = "module_mask";

	/// <summary>Claim type for module permissions version</summary>
	public const string ModuleMaskVersion = "module_mask_v";

	/// <summary>Claim type for submodule permissions bitmask</summary>
	public const string SubmoduleMask = "submodule_mask";

	/// <summary>Claim type for submodule permissions version</summary>
	public const string SubmoduleMaskVersion = "submodule_mask_v";

	/// <summary>Current version of module permissions schema</summary>
	public const string CurrentModuleVersion = "1.0";

	/// <summary>Current version of submodule permissions schema</summary>
	public const string CurrentSubmoduleVersion = "1.0";
}
