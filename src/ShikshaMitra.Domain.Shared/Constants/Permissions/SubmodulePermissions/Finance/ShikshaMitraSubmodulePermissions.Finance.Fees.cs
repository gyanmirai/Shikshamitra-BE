using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Finance
	{
		public static class Fees_FeeCollections
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.FeeCollections";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.FeeCollections";

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

		public static class Fees_Charges
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.Charges";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.Charges";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";

			public static readonly string[] All = { View, Add, Update, Delete, Approve, Reject };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" }
			};
		}

		public static class Fees_Refund
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.Refund";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.Refund";

			public const string View    = $"{SubmoduleName}.View";
			public const string Add     = $"{SubmoduleName}.Add";
			public const string Delete  = $"{SubmoduleName}.Delete";
			public const string Approve = $"{SubmoduleName}.Approve";

			public static readonly string[] All = { View, Add, Delete, Approve };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View,    $"{SubmoduleDisplayName}.View" },
				{ Add,     $"{SubmoduleDisplayName}.Add" },
				{ Delete,  $"{SubmoduleDisplayName}.Delete" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
			};
		}

		public static class Fees_InvoiceReceipt
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.InvoiceReceipt";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.InvoiceReceipt";

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

		public static class Fees_Concession
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.Concession";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.Concession";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";
			public const string Reverse = $"{SubmoduleName}.Reverse";

			public static readonly string[] All = { View, Add, Delete, Approve, Reject, Reverse };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" },
				{ Reverse, $"{SubmoduleDisplayName}.Reverse" }
			};
		}

		public static class Fees_Adjustment
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.Adjustment";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.Adjustment";

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

		public static class Fees_StructureAssign
		{
			private const string SubmoduleName = $"{ModuleName}.Fees.StructureAssign";
			private const string SubmoduleDisplayName = $"{DisplayName}.Fees.StructureAssign";

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

		public static class Fees_Settings_FeeCategory
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.FeeCategory";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.FeeCategory";

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

		public static class Fees_Settings_FeeHead
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.FeeHead";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.FeeHead";

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

		public static class Fees_Settings_ReceiptBook
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.ReceiptBook";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.ReceiptBook";

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

		public static class Fees_Settings_PaymentTerms
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.PaymentTerms";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.PaymentTerms";

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

		public static class Fees_Settings_FeeStructure
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.FeeStructure";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.FeeStructure";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Fees_Settings_ConcessionType
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.ConcessionType";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.ConcessionType";

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

		public static class Fees_Settings_ChargeCategory
		{
			private const string SubmoduleName = $"{ModuleName}.Settings.ChargeCategory";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings.ChargeCategory";

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
