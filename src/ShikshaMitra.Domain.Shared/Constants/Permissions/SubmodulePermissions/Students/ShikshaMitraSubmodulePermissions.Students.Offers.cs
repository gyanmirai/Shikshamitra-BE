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
			public const string Issue = Prefix + ".Issue";
			public const string Respond = Prefix + ".Respond";
			public const string Extend = Prefix + ".Extend";
			public const string Withdraw = Prefix + ".Withdraw";
			public const string Reissue = Prefix + ".Reissue";
			public static readonly string[] All = [View, Issue, Respond, Extend, Withdraw, Reissue];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission offers",
				[Issue] = "Issue admission offers",
				[Respond] = "Record admission offer responses",
				[Extend] = "Extend admission offers",
				[Withdraw] = "Withdraw admission offers",
				[Reissue] = "Reissue admission offers"
			};
		}
	}
}
