using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Finance module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Finance.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Finance.Fees.cs
/// - ShikshaMitraSubmodulePermissions.Finance.Accounts.cs
/// - ShikshaMitraSubmodulePermissions.Finance.Auditing.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Finance module permissions
	/// </summary>
	public static partial class Finance
	{
		/// <summary>
		/// Permission group name for Finance module.
		/// </summary>
		public const string GroupName = "Finance";

		/// <summary>
		/// Display name for Finance module permission group.
		/// </summary>
		public const string GroupDisplayName = "Permission:FinanceModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			// Fees submodules
			{ "Finance.Fees.FeeCollections", new SubmoduleInfo(Fees_FeeCollections.All, Fees_FeeCollections.DisplayNames) },
			{ "Finance.Fees.Charges", new SubmoduleInfo(Fees_Charges.All, Fees_Charges.DisplayNames) },
			{ "Finance.Fees.Refund", new SubmoduleInfo(Fees_Refund.All, Fees_Refund.DisplayNames) },
			{ "Finance.Fees.InvoiceReceipt", new SubmoduleInfo(Fees_InvoiceReceipt.All, Fees_InvoiceReceipt.DisplayNames) },
			{ "Finance.Fees.Concession", new SubmoduleInfo(Fees_Concession.All, Fees_Concession.DisplayNames) },
			{ "Finance.Fees.Adjustment", new SubmoduleInfo(Fees_Adjustment.All, Fees_Adjustment.DisplayNames) },
			{ "Finance.Fees.StructureAssign", new SubmoduleInfo(Fees_StructureAssign.All, Fees_StructureAssign.DisplayNames) },

			// Fees Settings submodules
			{ "Finance.Settings.FeeCategory", new SubmoduleInfo(Fees_Settings_FeeCategory.All, Fees_Settings_FeeCategory.DisplayNames) },
			{ "Finance.Settings.FeeHead", new SubmoduleInfo(Fees_Settings_FeeHead.All, Fees_Settings_FeeHead.DisplayNames) },
			{ "Finance.Settings.ReceiptBook", new SubmoduleInfo(Fees_Settings_ReceiptBook.All, Fees_Settings_ReceiptBook.DisplayNames) },
			{ "Finance.Settings.PaymentTerms", new SubmoduleInfo(Fees_Settings_PaymentTerms.All, Fees_Settings_PaymentTerms.DisplayNames) },
			{ "Finance.Settings.FeeStructure", new SubmoduleInfo(Fees_Settings_FeeStructure.All, Fees_Settings_FeeStructure.DisplayNames) },
			{ "Finance.Settings.ConcessionType", new SubmoduleInfo(Fees_Settings_ConcessionType.All, Fees_Settings_ConcessionType.DisplayNames) },
			{ "Finance.Settings.ChargeCategory", new SubmoduleInfo(Fees_Settings_ChargeCategory.All, Fees_Settings_ChargeCategory.DisplayNames) },

			// Accounts submodules
			{ "Finance.Accounts.Reconciliation", new SubmoduleInfo(Accounts_Reconciliation.All, Accounts_Reconciliation.DisplayNames) },
			{ "Finance.Accounts.ExpenseTracking", new SubmoduleInfo(Accounts_ExpenseTracking.All, Accounts_ExpenseTracking.DisplayNames) },
			{ "Finance.Accounts.IncomeTracking", new SubmoduleInfo(Accounts_IncomeTracking.All, Accounts_IncomeTracking.DisplayNames) },

			// Accounts Settings submodules
			{ "Finance.Accounts.Settings.IncomeCategory", new SubmoduleInfo(Accounts_Settings_IncomeCategory.All, Accounts_Settings_IncomeCategory.DisplayNames) },
			{ "Finance.Accounts.Settings.ExpenseCategory", new SubmoduleInfo(Accounts_Settings_ExpenseCategory.All, Accounts_Settings_ExpenseCategory.DisplayNames) },
			{ "Finance.Accounts.Settings.BankAccount", new SubmoduleInfo(Accounts_Settings_BankAccount.All, Accounts_Settings_BankAccount.DisplayNames) },
			{ "Finance.Accounts.Settings.AccountHead", new SubmoduleInfo(Accounts_Settings_AccountHead.All, Accounts_Settings_AccountHead.DisplayNames) },

			// Finance root-level submodules
			{ "Finance.AuditTrail", new SubmoduleInfo(AuditTrail.All, AuditTrail.DisplayNames) },
			{ "Finance.Auditing", new SubmoduleInfo(Auditing.All, Auditing.DisplayNames) },
			{ "Finance.Adjustments", new SubmoduleInfo(Adjustments.All, Adjustments.DisplayNames) },

			// Policies submodules
			{ "Finance.Policies.ConcessionPolicy", new SubmoduleInfo(Policies_ConcessionPolicy.All, Policies_ConcessionPolicy.DisplayNames) },
			{ "Finance.Policies.RefundPolicy", new SubmoduleInfo(Policies_RefundPolicy.All, Policies_RefundPolicy.DisplayNames) },
			{ "Finance.Policies.CollectionPolicy", new SubmoduleInfo(Policies_CollectionPolicy.All, Policies_CollectionPolicy.DisplayNames) },
			{ "Finance.Policies.ProrationPolicy", new SubmoduleInfo(Policies_ProrationPolicy.All, Policies_ProrationPolicy.DisplayNames) }
		};
	}
}
