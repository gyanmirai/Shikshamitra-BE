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

	/// <summary>
	/// Group management permissions for audience targeting in holidays/events.
	/// </summary>
	public static class Group
	{
		public const string Default = GroupName + ".Group";
		public const string List = Default + ".List";
		public const string View = Default + ".View";
		public const string Add = Default + ".Add";
		[System.Obsolete("Use Add instead")]
		public const string Create = Default + ".Create";
		public const string Edit = Default + ".Edit";
		public const string Delete = Default + ".Delete";
	}
}
