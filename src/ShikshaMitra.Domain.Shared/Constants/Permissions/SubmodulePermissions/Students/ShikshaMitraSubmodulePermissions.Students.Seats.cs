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
			public const string Reserve = Prefix + ".Reserve";
			public const string Confirm = Prefix + ".Confirm";
			public const string Renew = Prefix + ".Renew";
			public const string Release = Prefix + ".Release";
			public const string Waitlist = Prefix + ".Waitlist";
			public const string ManageCapacity = Prefix + ".ManageCapacity";
			public const string OverrideCapacity = Prefix + ".OverrideCapacity";
			public static readonly string[] All =
			[
				View, Reserve, Confirm, Renew, Release, Waitlist, ManageCapacity, OverrideCapacity
			];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission seat allocation",
				[Reserve] = "Reserve admission seats",
				[Confirm] = "Confirm admission seats",
				[Renew] = "Renew admission seat reservations",
				[Release] = "Release admission seats",
				[Waitlist] = "Manage admission waitlists",
				[ManageCapacity] = "Manage admission seat capacity",
				[OverrideCapacity] = "Override admission seat capacity"
			};
		}
	}
}
