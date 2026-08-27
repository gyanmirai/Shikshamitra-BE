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
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Activate = $"{SubmoduleName}.Activate";
			public const string Deactivate = $"{SubmoduleName}.Deactivate";
			public const string Clone = $"{SubmoduleName}.Clone";
			public const string BulkCreate = $"{SubmoduleName}.BulkCreate";
			public const string Validate = $"{SubmoduleName}.Validate";
			public const string Manage = $"{SubmoduleName}.Manage";

			public static readonly string[] All = { View, Create, Edit, Delete, Activate, Deactivate, Clone, BulkCreate, Validate, Manage };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Activate, $"{SubmoduleDisplayName}.Activate" },
				{ Deactivate, $"{SubmoduleDisplayName}.Deactivate" },
				{ Clone, $"{SubmoduleDisplayName}.Clone" },
				{ BulkCreate, $"{SubmoduleDisplayName}.BulkCreate" },
				{ Validate, $"{SubmoduleDisplayName}.Validate" },
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
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Clone = $"{SubmoduleName}.Clone";
			public const string CreateVersion = $"{SubmoduleName}.CreateVersion";
			public const string Validate = $"{SubmoduleName}.Validate";
			public const string Manage = $"{SubmoduleName}.Manage";
			public const string Publish = $"{SubmoduleName}.Publish";
			public const string Archive = $"{SubmoduleName}.Archive";
			public const string ViewUsage = $"{SubmoduleName}.ViewUsage";
			public static readonly string[] All =
			{
				View,
				Create,
				Edit,
				Delete,
				Clone,
				CreateVersion,
				Validate,
				Manage,
				Publish,
				Archive,
				ViewUsage
			};
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Clone, $"{SubmoduleDisplayName}.Clone" },
				{ CreateVersion, $"{SubmoduleDisplayName}.CreateVersion" },
				{ Validate, $"{SubmoduleDisplayName}.Validate" },
				{ Manage, $"{SubmoduleDisplayName}.Manage" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" },
				{ ViewUsage, $"{SubmoduleDisplayName}.ViewUsage" }
			};
		}

		public static class AdmissionForms
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Forms";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Forms";
			public const string View = $"{SubmoduleName}.View"; public const string Manage = $"{SubmoduleName}.Manage"; public const string Publish = $"{SubmoduleName}.Publish"; public const string Archive = $"{SubmoduleName}.Archive"; public const string CreateVersion = $"{SubmoduleName}.CreateVersion"; public const string ViewUsage = $"{SubmoduleName}.ViewUsage";
			public static readonly string[] All = { View, Manage, Publish, Archive, CreateVersion, ViewUsage };
			public static readonly Dictionary<string, string> DisplayNames = new() { { View, $"{SubmoduleDisplayName}.View" }, { Manage, $"{SubmoduleDisplayName}.Manage" }, { Publish, $"{SubmoduleDisplayName}.Publish" }, { Archive, $"{SubmoduleDisplayName}.Archive" }, { CreateVersion, $"{SubmoduleDisplayName}.CreateVersion" }, { ViewUsage, $"{SubmoduleDisplayName}.ViewUsage" } };
		}

		public static class AdmissionApplications
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Applications";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Applications";
			public const string View = $"{SubmoduleName}.View";
			public const string Start = $"{SubmoduleName}.Start";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Submit = $"{SubmoduleName}.Submit";
			public const string Reopen = $"{SubmoduleName}.Reopen";
			public const string Advance = $"{SubmoduleName}.Advance";
			public const string SkipStage = $"{SubmoduleName}.SkipStage";
			public const string OverrideWorkflow = $"{SubmoduleName}.OverrideWorkflow";
			public const string Decide = $"{SubmoduleName}.Decide";
			public const string Offer = $"{SubmoduleName}.Offer";
			public const string Enroll = $"{SubmoduleName}.Enroll";
			public static readonly string[] All = { View, Start, Edit, Submit, Reopen, Advance, SkipStage, OverrideWorkflow, Decide, Offer, Enroll };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Start, $"{SubmoduleDisplayName}.Start" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Submit, $"{SubmoduleDisplayName}.Submit" },
				{ Reopen, $"{SubmoduleDisplayName}.Reopen" },
				{ Advance, $"{SubmoduleDisplayName}.Advance" },
				{ SkipStage, $"{SubmoduleDisplayName}.SkipStage" },
				{ OverrideWorkflow, $"{SubmoduleDisplayName}.OverrideWorkflow" }
				, { Decide, $"{SubmoduleDisplayName}.Decide" }
				, { Offer, $"{SubmoduleDisplayName}.Offer" }
				, { Enroll, $"{SubmoduleDisplayName}.Enroll" }
			};
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
