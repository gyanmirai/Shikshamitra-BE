namespace ShikshaMitra.HostTenants;

public static class HostTenantConsts
{
	public const string IsActiveProperty = "IsActive";
	public const string InstituteNameProperty = "InstituteName";
	public const string ManageStatusPermission = "AbpTenantManagement.Tenants.ManageStatus";
	public const int MaxCodeLength = 64;
	public const int MaxInstituteNameLength = 256;
	public const int MaxAddressLength = 256;
	public const int MaxContactLength = 32;
	public const int MaxReasonLength = 1024;
	public const string StatusReadOnly = "HostTenants:StatusReadOnly";
	public const string CodeReadOnly = "HostTenants:CodeReadOnly";
	public const string ReasonRequired = "HostTenants:ReasonRequired";
	public const string InvalidSorting = "HostTenants:InvalidSorting";
}
