using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		public static class PayrollManagement_Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Dashboard";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class PayrollManagement_RunPayroll
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.RunPayroll";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.RunPayroll";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class PayrollManagement_GenerateSalarySlip
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.GenerateSalarySlip";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.GenerateSalarySlip";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class PayrollManagement_SettingsEarningComponents
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.EarningComponents";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.EarningComponents";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class PayrollManagement_DeductionComponents
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.DeductionComponents";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.DeductionComponents";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
