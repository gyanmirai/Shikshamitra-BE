using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionReviews
		{
			private const string Prefix = "Students.Admissions.Reviews";
			public const string View = Prefix + ".View";
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string Edit = Add;
			public const string Submit = Add;
			public const string Assign = Add;
			public const string Decide = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission reviews",
				[Add] = "Add or update admission reviews",
				[Delete] = "Delete admission reviews"
			};
		}
	}
}
