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
			public const string Add = Name + ".Add";
			public const string Delete = Name + ".Delete";
			public const string Create = Add;
			public const string Approve = Add;
			public const string Cancel = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		public static class Records_Transitions
		{
			private const string Name = "Students.Records.Transitions";
			public const string View = Name + ".View";
			public const string Add = Name + ".Add";
			public const string Delete = Name + ".Delete";
			public const string Create = Add;
			public const string Approve = Add;
			public const string Apply = Add;
			public const string Cancel = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		public static class Records_Overrides
		{
			private const string Name = "Students.Records.Overrides";
			public const string View = Name + ".View";
			public const string Add = Name + ".Add";
			public const string Request = Add;
			public const string Approve = Add;
			public static readonly string[] All = [View, Add];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
		public static class Records_GuardianAccess
		{
			private const string Name = "Students.Records.GuardianAccess";
			public const string View = Name + ".View";
			public const string Add = Name + ".Add";
			public const string Manage = Add;
			public static readonly string[] All = [View, Add];
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
