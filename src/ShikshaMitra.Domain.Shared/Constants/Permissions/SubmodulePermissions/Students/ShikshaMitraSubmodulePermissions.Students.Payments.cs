using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionPayments
		{
			private const string Prefix = "Students.Admissions.Payments";
			public const string View = Prefix + ".View";
			public const string Record = Prefix + ".Record";
			public const string Verify = Prefix + ".Verify";
			public const string Waive = Prefix + ".Waive";
			public const string Reverse = Prefix + ".Reverse";
			public static readonly string[] All = [View, Record, Verify, Waive, Reverse];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission payments",
				[Record] = "Record admission payments",
				[Verify] = "Verify admission payments",
				[Waive] = "Waive admission payments",
				[Reverse] = "Reverse admission payments"
			};
		}
	}
}
