namespace ShikshaMitra.Security.Permissions;

public static class ShikshaMitraPermissions
{
	public const string GroupName = "ShikshaMitra";

	/// <summary>
	/// User Scope permissions for access control management.
	/// </summary>
	public static class UserScope
	{
		public const string Default = GroupName + ".UserScope";
		public const string Assign = Default + ".Assign";
		public const string Revoke = Default + ".Revoke";
		public const string View = Default + ".View";
		public const string BulkAssign = Default + ".BulkAssign";
		public const string Manage = Default + ".Manage";
	}
}
