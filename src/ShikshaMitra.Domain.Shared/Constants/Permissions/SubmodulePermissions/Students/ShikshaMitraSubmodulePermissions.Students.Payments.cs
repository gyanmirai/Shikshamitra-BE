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
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string Record = Add;
			public const string Verify = Add;
			public const string Waive = Add;
			public const string Reverse = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission payments",
				[Add] = "Add or update admission payments",
				[Delete] = "Delete or reverse admission payments"
			};
		}
	}
}
