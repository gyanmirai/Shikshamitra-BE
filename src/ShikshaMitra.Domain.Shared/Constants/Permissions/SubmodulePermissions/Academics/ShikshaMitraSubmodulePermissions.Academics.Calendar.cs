using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		/// <summary>
		/// Academic Calendar permissions
		/// </summary>
		public static class Academics_AcademicCalendar
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar";

			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Publish = $"{SubmoduleName}.Publish";
			public const string Archive = $"{SubmoduleName}.Archive";
			public const string ManageSettings = $"{SubmoduleName}.ManageSettings";
			public const string EmergencyClosure = $"{SubmoduleName}.EmergencyClosure";

			public static readonly string[] All = { View, Create, Update, Delete, Publish, Archive, ManageSettings, EmergencyClosure };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" },
				{ ManageSettings, $"{SubmoduleDisplayName}.ManageSettings" },
				{ EmergencyClosure, $"{SubmoduleDisplayName}.EmergencyClosure" }
			};
		}
	}
}
