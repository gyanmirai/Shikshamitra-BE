using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		public static class PayrollManagement
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class PayrollManagement_Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Dashboard";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class PayrollManagement_RunPayroll
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.RunPayroll";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.RunPayroll";

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

		public static class PayrollManagement_GenerateSalarySlip
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.GenerateSalarySlip";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.GenerateSalarySlip";

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

		public static class PayrollManagement_SettingsEarningComponents
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.EarningComponents";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.EarningComponents";

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

		public static class PayrollManagement_SettingsDeductionComponents
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.DeductionComponents";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.DeductionComponents";

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

		public static class PayrollManagement_SettingsPfRule
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.PfRule";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.PfRule";

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

		public static class PayrollManagement_SettingsPayrollConfiguration
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.PayrollConfiguration";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.PayrollConfiguration";

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

		public static class PayrollManagement_PayrollStructure
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.PayrollStructure";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.PayrollStructure";

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

		public static class PayrollManagement_SettingsTds
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.Tds";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.Tds";

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

		public static class PayrollManagement_SettingsProfessionalTax
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.ProfessionalTax";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.ProfessionalTax";

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
