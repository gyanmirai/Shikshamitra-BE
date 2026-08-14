using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class Admissions
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions";

			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Publish = $"{SubmoduleName}.Publish";
			public const string Close = $"{SubmoduleName}.Close";
			public const string Archive = $"{SubmoduleName}.Archive";

			public static readonly string[] All = { View, Create, Edit, Publish, Close, Archive };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" },
				{ Close, $"{SubmoduleDisplayName}.Close" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" }
			};
		}

		public static class AdmissionOfferings
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Offerings";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Offerings";

			public const string View = $"{SubmoduleName}.View";
			public const string Manage = $"{SubmoduleName}.Manage";

			public static readonly string[] All = { View, Manage };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Manage, $"{SubmoduleDisplayName}.Manage" }
			};
		}

		public static class AdmissionInquiries
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Inquiries";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Inquiries";
			public const string View = $"{SubmoduleName}.View";
			public const string Manage = $"{SubmoduleName}.Manage";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Assign = $"{SubmoduleName}.Assign";
			public static readonly string[] All = { View, Manage, Create, Edit, Assign };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Manage, $"{SubmoduleDisplayName}.Manage" }, { Create, $"{SubmoduleDisplayName}.Create" }, { Edit, $"{SubmoduleDisplayName}.Edit" }, { Assign, $"{SubmoduleDisplayName}.Assign" }
			};
		}

		public static class AdmissionWorkflows
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Workflows";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Workflows";
			public const string View = $"{SubmoduleName}.View";
			public const string Manage = $"{SubmoduleName}.Manage";
			public const string Publish = $"{SubmoduleName}.Publish";
			public static readonly string[] All = { View, Manage, Publish };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Manage, $"{SubmoduleDisplayName}.Manage" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" }
			};
		}

		public static class AdmissionForms
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Forms";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Forms";
			public const string View = $"{SubmoduleName}.View"; public const string Manage = $"{SubmoduleName}.Manage"; public const string Publish = $"{SubmoduleName}.Publish";
			public static readonly string[] All = { View, Manage, Publish };
			public static readonly Dictionary<string, string> DisplayNames = new() { { View, $"{SubmoduleDisplayName}.View" }, { Manage, $"{SubmoduleDisplayName}.Manage" }, { Publish, $"{SubmoduleDisplayName}.Publish" } };
		}

		public static class AdmissionApplications
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Applications";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Applications";
			public const string View = $"{SubmoduleName}.View";
			public const string Start = $"{SubmoduleName}.Start";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Submit = $"{SubmoduleName}.Submit";
			public static readonly string[] All = { View, Start, Edit, Submit };
			public static readonly Dictionary<string, string> DisplayNames = new() { { View, $"{SubmoduleDisplayName}.View" }, { Start, $"{SubmoduleDisplayName}.Start" }, { Edit, $"{SubmoduleDisplayName}.Edit" }, { Submit, $"{SubmoduleDisplayName}.Submit" } };
		}

		public static class AdmissionApplicants
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Applicants";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Applicants";
			public const string View = $"{SubmoduleName}.View";
			public const string NotesView = $"{SubmoduleName}.Notes.View";
			public const string NotesCreate = $"{SubmoduleName}.Notes.Create";
			public const string NotesEdit = $"{SubmoduleName}.Notes.Edit";
			public static readonly string[] All = { View, NotesView, NotesCreate, NotesEdit };
			public static readonly Dictionary<string, string> DisplayNames = new() { { View, $"{SubmoduleDisplayName}.View" }, { NotesView, $"{SubmoduleDisplayName}.Notes.View" }, { NotesCreate, $"{SubmoduleDisplayName}.Notes.Create" }, { NotesEdit, $"{SubmoduleDisplayName}.Notes.Edit" } };
		}
	}
}
