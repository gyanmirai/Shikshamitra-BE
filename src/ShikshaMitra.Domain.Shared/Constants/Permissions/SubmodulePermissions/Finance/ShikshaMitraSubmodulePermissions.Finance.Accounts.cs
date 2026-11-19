using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Finance
	{
		public static class Accounts_Activity
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Activity";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Activity";
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class Accounts_Reconciliation
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Reconciliation";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Reconciliation";

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

		public static class Accounts_ExpenseTracking
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.ExpenseTracking";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.ExpenseTracking";

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

		public static class Accounts_IncomeTracking
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.IncomeTracking";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.IncomeTracking";

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

		public static class AuditTrail
		{
			private const string SubmoduleName = $"{ModuleName}.AuditTrail";
			private const string SubmoduleDisplayName = $"{DisplayName}.AuditTrail";

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

		public static class Adjustments
		{
			private const string SubmoduleName = $"{ModuleName}.Adjustments";
			private const string SubmoduleDisplayName = $"{DisplayName}.Adjustments";

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

		public static class Accounts_Settings_IncomeCategory
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Settings.IncomeCategory";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Settings.IncomeCategory";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string ChangeStatus = $"{SubmoduleName}.ChangeStatus";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, ChangeStatus, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ ChangeStatus, $"{SubmoduleDisplayName}.ChangeStatus" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Accounts_Settings_ExpenseCategory
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Settings.ExpenseCategory";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Settings.ExpenseCategory";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string ChangeStatus = $"{SubmoduleName}.ChangeStatus";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, ChangeStatus, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ ChangeStatus, $"{SubmoduleDisplayName}.ChangeStatus" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Accounts_Settings_BankAccount
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Settings.BankAccount";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Settings.BankAccount";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string ChangeStatus = $"{SubmoduleName}.ChangeStatus";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, ChangeStatus, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ ChangeStatus, $"{SubmoduleDisplayName}.ChangeStatus" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Accounts_Settings_AccountHead
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Settings.AccountHead";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Settings.AccountHead";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string ChangeStatus = $"{SubmoduleName}.ChangeStatus";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, ChangeStatus, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ ChangeStatus, $"{SubmoduleDisplayName}.ChangeStatus" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Accounts_Settings_PostingSetup
		{
			private const string SubmoduleName = $"{ModuleName}.Accounts.Settings.PostingSetup";
			private const string SubmoduleDisplayName = $"{DisplayName}.Accounts.Settings.PostingSetup";
			public const string View = $"{SubmoduleName}.View";
			public const string Edit = $"{SubmoduleName}.Edit";
			public static readonly string[] All = { View, Edit };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" }
			};
		}
	}
}
