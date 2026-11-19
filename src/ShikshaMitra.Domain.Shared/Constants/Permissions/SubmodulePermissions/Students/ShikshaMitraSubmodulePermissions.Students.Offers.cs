using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionOffers
		{
			private const string Prefix = "Students.Admissions.Offers";
			public const string View = Prefix + ".View";
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string Issue = Add;
			public const string Respond = Add;
			public const string Extend = Add;
			public const string Withdraw = Add;
			public const string Reissue = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission offers",
				[Add] = "Add or update admission offers",
				[Delete] = "Delete or withdraw admission offers"
			};
		}
	}
}
