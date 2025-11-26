using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Finance module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Finance module permissions
	/// </summary>
	public static class Finance
	{
		/// <summary>
		/// Permission group name for Finance module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "Finance";
		
		/// <summary>
		/// Display name for Finance module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:FinanceModule";

		private const string ModuleName = $"{ShikshaMitraSubmodulePermissions.GroupName}.Finance";
		private const string DisplayName = $"{DisplayNamePrefix}:Finance";

		public const string Default = ModuleName;

		// Fee Management Submodule
		public static class FeeManagement
		{
			private const string SubmoduleName = $"{ModuleName}.FeeManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.FeeManagement";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Approve = $"{SubmoduleName}.Approve";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Approve };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" }
			};
		}

		// Fee Collection Submodule
		public static class FeeCollection
		{
			private const string SubmoduleName = "FeeCollection";
			private const string SubmoduleDisplayName = $"{DisplayName}.FeeCollection";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Collect = $"{SubmoduleName}.Collect";
			public const string Refund = $"{SubmoduleName}.Refund";
			public const string Receipt = $"{SubmoduleName}.Receipt";
			public const string Cancel = $"{SubmoduleName}.Cancel";

			public static readonly string[] All = { Default, View, Collect, Refund, Receipt, Cancel };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Collect, $"{SubmoduleDisplayName}.Collect" },
				{ Refund, $"{SubmoduleDisplayName}.Refund" },
				{ Receipt, $"{SubmoduleDisplayName}.Receipt" },
				{ Cancel, $"{SubmoduleDisplayName}.Cancel" }
			};
		}

		// Expenses Submodule
		public static class Expenses
		{
			private const string SubmoduleName = $"{ModuleName}.Expenses";
			private const string SubmoduleDisplayName = $"{DisplayName}.Expenses";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Approve, Reject };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" }
			};
		}

		// Reports Submodule
		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Export = $"{SubmoduleName}.Export";
			public const string Financial = $"{SubmoduleName}.Financial";
			public const string Revenue = $"{SubmoduleName}.Revenue";

			public static readonly string[] All = { Default, View, Export, Financial, Revenue };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Export, $"{SubmoduleDisplayName}.Export" },
				{ Financial, $"{SubmoduleDisplayName}.Financial" },
				{ Revenue, $"{SubmoduleDisplayName}.Revenue" }
			};
		}

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ FeeManagement.Default, new SubmoduleInfo(FeeManagement.All, FeeManagement.DisplayNames) },
			{ FeeCollection.Default, new SubmoduleInfo(FeeCollection.All, FeeCollection.DisplayNames) },
			{ Expenses.Default, new SubmoduleInfo(Expenses.All, Expenses.DisplayNames) },
			{ Reports.Default, new SubmoduleInfo(Reports.All, Reports.DisplayNames) }
		};
	}
}
