using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Finance
	{
		public static class Policies_ConcessionPolicy
		{
			private const string SubmoduleName = $"{ModuleName}.Policies.ConcessionPolicy";
			private const string SubmoduleDisplayName = $"{DisplayName}.Policies.ConcessionPolicy";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Clone = $"{SubmoduleName}.Clone";
			public const string Simulate = $"{SubmoduleName}.Simulate";
			public const string Archive = $"{SubmoduleName}.Archive";
			public const string Unarchive = $"{SubmoduleName}.Unarchive";
			public const string ViewVersions = $"{SubmoduleName}.ViewVersions";
			public const string ManageAttachment = $"{SubmoduleName}.ManageAttachment";

			public static readonly string[] All = { View, Add, Delete, Edit, Clone, Simulate, Archive, Unarchive, ViewVersions, ManageAttachment };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Clone, $"{SubmoduleDisplayName}.Clone" },
				{ Simulate, $"{SubmoduleDisplayName}.Simulate" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" },
				{ Unarchive, $"{SubmoduleDisplayName}.Unarchive" },
				{ ViewVersions, $"{SubmoduleDisplayName}.ViewVersions" },
				{ ManageAttachment, $"{SubmoduleDisplayName}.ManageAttachment" }
			};
		}

		public static class Policies_RefundPolicy
		{
			private const string SubmoduleName = $"{ModuleName}.Policies.RefundPolicy";
			private const string SubmoduleDisplayName = $"{DisplayName}.Policies.RefundPolicy";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Policies_CollectionPolicy
		{
			private const string SubmoduleName = $"{ModuleName}.Policies.CollectionPolicy";
			private const string SubmoduleDisplayName = $"{DisplayName}.Policies.CollectionPolicy";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Archive = $"{SubmoduleName}.Archive";
			public const string EditStudentOverride = $"{SubmoduleName}.EditStudentOverride";
			public const string ApproveWaiver = $"{SubmoduleName}.ApproveWaiver";
			/// <summary>Phase 4a: required in addition to ApproveWaiver when a waiver's requested amount exceeds the resolved policy's ApprovalThresholdAmount — lets schools have a two-tier approver structure (e.g. Accountant approves small waivers, Principal approves large ones).</summary>
			public const string ApproveWaiverAboveThreshold = $"{SubmoduleName}.ApproveWaiverAboveThreshold";
			public const string ManageBulkWaiver = $"{SubmoduleName}.ManageBulkWaiver";
			public const string ManageExceptionWindow = $"{SubmoduleName}.ManageExceptionWindow";
			public const string ViewReports = $"{SubmoduleName}.ViewReports";

			public static readonly string[] All = { View, Add, Delete, Edit, Archive, EditStudentOverride, ApproveWaiver, ApproveWaiverAboveThreshold, ManageBulkWaiver, ManageExceptionWindow, ViewReports };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" },
				{ EditStudentOverride, $"{SubmoduleDisplayName}.EditStudentOverride" },
				{ ApproveWaiver, $"{SubmoduleDisplayName}.ApproveWaiver" },
				{ ApproveWaiverAboveThreshold, $"{SubmoduleDisplayName}.ApproveWaiverAboveThreshold" },
				{ ManageBulkWaiver, $"{SubmoduleDisplayName}.ManageBulkWaiver" },
				{ ManageExceptionWindow, $"{SubmoduleDisplayName}.ManageExceptionWindow" },
				{ ViewReports, $"{SubmoduleDisplayName}.ViewReports" }
			};
		}

		public static class Policies_ProrationPolicy
		{
			private const string SubmoduleName = $"{ModuleName}.Policies.ProrationPolicy";
			private const string SubmoduleDisplayName = $"{DisplayName}.Policies.ProrationPolicy";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
    }
}
