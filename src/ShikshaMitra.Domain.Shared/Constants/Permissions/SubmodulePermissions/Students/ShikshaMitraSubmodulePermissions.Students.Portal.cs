using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>Self-service photo access; never grants staff record management.</summary>
		public static class Portal_ProfilePhoto
		{
			private const string Name = "Students.Portal.ProfilePhoto";
			public const string View = Name + ".View";
			public const string Add = Name + ".Add";
			public static readonly string[] All = [View, Add];
			public static readonly Dictionary<string, string> DisplayNames = Names(All);
		}
	}
}
