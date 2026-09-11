using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionEligibility
		{
			private const string Prefix = "Students.Admissions.Eligibility";
			public const string View = Prefix + ".View";
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string Evaluate = Add;
			public const string RequestException = Add;
			public const string ReviewException = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission eligibility",
				[Add] = "Add or update admission eligibility",
				[Delete] = "Delete admission eligibility"
			};
		}
	}
}
