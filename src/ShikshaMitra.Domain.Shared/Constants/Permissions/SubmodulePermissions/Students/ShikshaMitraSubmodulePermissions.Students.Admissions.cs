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
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Create = Add;
			public const string Edit = Add;
			public const string Publish = Add;
			public const string Close = Delete;
			public const string Archive = Delete;

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AdmissionOfferings
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Offerings";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Offerings";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Create = Add;
			public const string Edit = Add;
			public const string Activate = Add;
			public const string Deactivate = Delete;
			public const string Clone = Add;
			public const string BulkCreate = Add;
			public const string Validate = Add;
			public const string Manage = Add;

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AdmissionInquiries
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Inquiries";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Inquiries";
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Manage = Add;
			public const string Create = Add;
			public const string Edit = Add;
			public const string Assign = Add;
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AdmissionWorkflows
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Workflows";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Workflows";
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Create = Add;
			public const string Edit = Add;
			public const string Clone = Add;
			public const string CreateVersion = Add;
			public const string Validate = Add;
			public const string Manage = Add;
			public const string Publish = Add;
			public const string Archive = Delete;
			public const string ViewUsage = View;
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AdmissionForms
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Forms";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Forms";
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Manage = Add;
			public const string Publish = Add;
			public const string Archive = Delete;
			public const string CreateVersion = Add;
			public const string ViewUsage = View;
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AdmissionApplications
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Applications";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Applications";
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Start = Add;
			public const string Edit = Add;
			public const string Submit = Add;
			public const string Reopen = Add;
			public const string Advance = Add;
			public const string SkipStage = Add;
			public const string OverrideWorkflow = Add;
			public const string Decide = Add;
			public const string Offer = Add;
			public const string Finalize = Add;
			[System.Obsolete("Use Finalize. Admissions no longer creates operational enrollments.")]
			public const string Enroll = Add;
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class StudentOnboarding
		{
			private const string SubmoduleName = $"{ModuleName}.Onboarding";
			private const string SubmoduleDisplayName = $"{DisplayName}.Onboarding";
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Edit = Add;
			public const string Waive = Add;
			public const string AllocateSeat = Add;
			public const string AllocateIdentifier = Add;
			public const string Activate = Add;
			public const string Cancel = Delete;
			public const string Repair = Add;
			public const string Configure = Add;
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AdmissionApplicants
		{
			private const string SubmoduleName = $"{ModuleName}.Admissions.Applicants";
			private const string SubmoduleDisplayName = $"{DisplayName}.Admissions.Applicants";
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Edit = Add;
			public const string NotesView = View;
			public const string NotesCreate = Add;
			public const string NotesEdit = Add;
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
