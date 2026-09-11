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
			public const string Evaluate = Prefix + ".Evaluate";
			public const string RequestException = Prefix + ".RequestException";
			public const string ReviewException = Prefix + ".ReviewException";
			public static readonly string[] All = [View, Evaluate, RequestException, ReviewException];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission eligibility", [Evaluate] = "Evaluate admission eligibility",
				[RequestException] = "Request an eligibility exception", [ReviewException] = "Review eligibility exceptions"
			};
		}
	}
}
