namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public static class SchoolIdentityPermissions
{
	public const string Navigation = "ShikshaMitra.Module.InstituteSetup.SchoolIdentity";
	public const string View = "InstituteSetup.SchoolIdentity.View";
	public const string ManageDefaults = "InstituteSetup.SchoolIdentity.ManageDefaults";
	public const string ManageOverrides = "InstituteSetup.SchoolIdentity.ManageOverrides";
	public const string AccessSignature = "InstituteSetup.SchoolIdentity.AccessSignature";
}

public enum SchoolIdentityAssetKind { Logo, Banner, Signature }
public enum SchoolIdentityAssetMode { Inherit, Custom, Hidden }
