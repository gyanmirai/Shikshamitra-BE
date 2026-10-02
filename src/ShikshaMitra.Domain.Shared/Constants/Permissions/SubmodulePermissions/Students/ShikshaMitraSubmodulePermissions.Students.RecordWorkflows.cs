using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class Records_Directory
		{
			public const string View = Records_StudentList.View;
		}
		public static class Records_Changes
		{
			private const string Name = "Students.Records.Changes";
			public const string View = Name + ".View";
			public const string Create = Name + ".Create";
			public const string Approve = Name + ".Approve";
			public const string Cancel = Name + ".Cancel";
			public static readonly string[] All = [View, Create, Approve, Cancel];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		public static class Records_Transitions
		{
			private const string Name = "Students.Records.Transitions";
			public const string View = Name + ".View";
			public const string Create = Name + ".Create";
			public const string Approve = Name + ".Approve";
			public const string Apply = Name + ".Apply";
			public static readonly string[] All = [View, Create, Approve, Apply];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		public static class Records_Overrides
		{
			private const string Name = "Students.Records.Overrides";
			public const string Request = Name + ".Request";
			public const string Approve = Name + ".Approve";
			public static readonly string[] All = [Request, Approve];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		public static class Records_GuardianAccess
		{
			private const string Name = "Students.Records.GuardianAccess";
			public const string View = Name + ".View";
			public const string Manage = Name + ".Manage";
			public static readonly string[] All = [View, Manage];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		private static Dictionary<string, string> Names(string[] names)
		{
			var result = new Dictionary<string, string>();
			foreach (var name in names) result.Add(name, "Permission:" + name);
			return result;
		}
	}
}
