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
			public const string Edit = Prefix + ".Edit";
			public const string Submit = Prefix + ".Submit";
			public const string Assign = Prefix + ".Assign";
			public const string Decide = Prefix + ".Decide";
			public static readonly string[] All = [View, Edit, Submit, Assign, Decide];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission reviews", [Edit] = "Save admission review drafts", [Submit] = "Submit admission recommendations",
				[Assign] = "Assign admission reviewers", [Decide] = "Decide reviewed admissions"
			};
		}
	}
}
