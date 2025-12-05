namespace ShikshaMitra.Permissions;

public static class ShikshaMitraPermissions
{
	public const string GroupName = "ShikshaMitra";

	public static class Students
	{
		public const string Default = GroupName + ".Students";

		public static class Attendance
		{
			public const string Default = Students.Default + ".Attendance";
			public const string View = Default + ".View";
			public const string Create = Default + ".Create";
			public const string Edit = Default + ".Edit";
			public const string Delete = Default + ".Delete";

			public static class Settings
			{
				public const string Default = Attendance.Default + ".Settings";
				public const string Create = Default + ".Create";
				public const string Edit = Default + ".Edit";
				public const string Delete = Default + ".Delete";
			}
		}
	}
}
