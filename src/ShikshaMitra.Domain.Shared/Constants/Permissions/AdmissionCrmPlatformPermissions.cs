namespace ShikshaMitra.Constants.Permissions;

public static class AdmissionCrmPlatformPermissions
{
	public const string Default = "ShikshaMitra.Platform.Enrollment";
	public const string StartSupportSession = Default + ".StartSupportSession";
	public const string ManagePresets = Default + ".ManagePresets";
	public const string ApplyPresets = Default + ".ApplyPresets";
	public const string ManageTenantAccess = Default + ".ManageTenantAccess";
	public const string ViewAudit = Default + ".ViewAudit";

	public static readonly string[] All =
	[
		Default,
		StartSupportSession,
		ManagePresets,
		ApplyPresets,
		ManageTenantAccess,
		ViewAudit
	];
}
