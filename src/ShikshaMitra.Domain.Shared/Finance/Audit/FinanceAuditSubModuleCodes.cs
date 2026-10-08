namespace ShikshaMitra.Finance.Audit;

public static class FinanceAuditSubModuleCodes
{
	public const string PaymentTerms = "payment_terms";
	public const string Collections = "collections";
	public const string Charges = "charges";
	public const string Refunds = "refunds";
	public const string InvoiceReceipts = "invoice_receipts";
	public const string Concessions = "concessions";
	public const string Reconciliation = "reconciliation";
	public const string IncomeTracking = "income_tracking";
	public const string ExpenseTracking = "expense_tracking";
	public const string Adjustments = "adjustments";

	public static string FromEntityType(string entityType) =>
		(entityType ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_") switch
		{
			FinanceAuditEntityTypes.PaymentTerm => PaymentTerms,
			FinanceAuditEntityTypes.FeePayment or FinanceAuditEntityTypes.DueFee => Collections,
			FinanceAuditEntityTypes.FeeCharge => Charges,
			FinanceAuditEntityTypes.FeeRefund => Refunds,
			FinanceAuditEntityTypes.FeeReceipt => InvoiceReceipts,
			FinanceAuditEntityTypes.ConcessionRequest => Concessions,
			FinanceAuditEntityTypes.FeeAdjustment => Adjustments,
			FinanceAuditEntityTypes.BankReconciliation => Reconciliation,
			FinanceAuditEntityTypes.IncomeEntry => IncomeTracking,
			FinanceAuditEntityTypes.ExpenseEntry => ExpenseTracking,
			_ => Collections
		};

	public static string ToDisplayName(string subModuleCode) =>
		(subModuleCode ?? string.Empty).Trim().ToLowerInvariant() switch
		{
			PaymentTerms => "Payment Terms",
			Collections => "Collections",
			Charges => "Charges",
			Refunds => "Refunds",
			InvoiceReceipts => "Invoice & Receipts",
			Concessions => "Concessions",
			Reconciliation => "Reconciliation",
			IncomeTracking => "Income Tracking",
			ExpenseTracking => "Expense Tracking",
			Adjustments => "Adjustments",
			_ => "Finance"
		};
}
