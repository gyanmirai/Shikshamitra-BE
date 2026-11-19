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
			public const string Lock = $"{SubmoduleName}.Lock";
			public const string ViewAudit = $"{SubmoduleName}.ViewAudit";
			public const string Adjustments = $"{SubmoduleName}.Adjustments";

			public static readonly string[] All = { View, Add, Delete, Lock, ViewAudit, Adjustments };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Lock, $"{SubmoduleDisplayName}.Lock" },
				{ ViewAudit, $"{SubmoduleDisplayName}.ViewAudit" },
				{ Adjustments, $"{SubmoduleDisplayName}.Adjustments" }
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

		public static class PayrollManagement_SettingsPayrollPolicies
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.PayrollPolicies";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.PayrollPolicies";

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

		public static class PayrollManagement_SettingsSalaryTemplates
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.SalaryTemplates";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.SalaryTemplates";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Publish = $"{SubmoduleName}.Publish";
			public const string SetDefault = $"{SubmoduleName}.SetDefault";
			public const string Archive = $"{SubmoduleName}.Archive";

			public static readonly string[] All = { View, Add, Edit, Publish, SetDefault, Archive };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" },
				{ SetDefault, $"{SubmoduleDisplayName}.SetDefault" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" }
			};
		}

		public static class PayrollManagement_CompensationRevisions
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.CompensationRevisions";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.CompensationRevisions";

			public const string View = $"{SubmoduleName}.View";
			public const string Manage = $"{SubmoduleName}.Manage";
			public const string ViewAudit = $"{SubmoduleName}.ViewAudit";

			public static readonly string[] All = { View, Manage, ViewAudit };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Manage, $"{SubmoduleDisplayName}.Manage" },
				{ ViewAudit, $"{SubmoduleDisplayName}.ViewAudit" }
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

		public static class PayrollManagement_SettingsStatutorySchemes
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Settings.StatutorySchemes";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Settings.StatutorySchemes";

			public const string View = $"{SubmoduleName}.View";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Activate = $"{SubmoduleName}.Activate";
			public const string ManageEnrollment = $"{SubmoduleName}.ManageEnrollment";
			public const string ConfigureCalculation = $"{SubmoduleName}.ConfigureCalculation";
			public const string ApproveOverride = $"{SubmoduleName}.ApproveOverride";

			public static readonly string[] All = { View, Edit, Activate, ManageEnrollment, ApproveOverride, ConfigureCalculation };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Activate, $"{SubmoduleDisplayName}.Activate" },
				{ ManageEnrollment, $"{SubmoduleDisplayName}.ManageEnrollment" },
				{ ApproveOverride, $"{SubmoduleDisplayName}.ApproveOverride" },
				{ ConfigureCalculation, $"{SubmoduleDisplayName}.ConfigureCalculation" }
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

		public static class PayrollManagement_Overview
		{
			private const string SubmoduleName = $"{ModuleName}.PayrollManagement.Overview";
			private const string SubmoduleDisplayName = $"{DisplayName}.PayrollManagement.Overview";

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
