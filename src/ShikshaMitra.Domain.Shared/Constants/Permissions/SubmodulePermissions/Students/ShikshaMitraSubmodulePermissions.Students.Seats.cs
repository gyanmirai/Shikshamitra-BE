using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionSeats
		{
			private const string Prefix = "Students.Admissions.Seats";
			public const string View = Prefix + ".View";
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string Reserve = Add;
			public const string Confirm = Add;
			public const string Renew = Add;
			public const string Release = Delete;
			public const string Waitlist = Add;
			public const string ManageCapacity = Add;
			public const string OverrideCapacity = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission seat allocation",
				[Add] = "Add or update admission seat allocation",
				[Delete] = "Delete or release admission seat allocation"
			};
		}
	}
}
