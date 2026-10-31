using System;
using System.Text.RegularExpressions;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.Receipts;

public static class FeeReceiptPresentation
{
	public static bool IsProvisional(FeeReceiptKind kind)
		=> kind is FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge;

	public static string GetLabel(FeeReceiptKind kind, FeeReceiptScope scope)
		=> $"{(IsProvisional(kind) ? "Provisional Receipt" : "Receipt")} ({GetScopeLabel(scope)})";

	public static string GetScopeLabel(FeeReceiptScope scope) => scope switch
	{
		FeeReceiptScope.Charge => "Charge",
		FeeReceiptScope.Combined => "Fees and Charges",
		_ => "Fee"
	};

	public static string GetPaymentMethodLabel(PaymentMethodEnum method) => method switch
	{
		PaymentMethodEnum.DemandDraft => "Demand Draft",
		PaymentMethodEnum.BankTransfer => "Bank Transfer",
		PaymentMethodEnum.CreditCard => "Credit Card",
		PaymentMethodEnum.DebitCard => "Debit Card",
		_ => method.ToString()
	};

	public static string GetPdfFileName(FeeReceiptKind kind, FeeReceiptScope scope, string number, DateOnly date)
	{
		var safeNumber = Regex.Replace(number, "[^A-Za-z0-9_-]", "-");
		var scopeName = scope == FeeReceiptScope.Combined ? "Fees-And-Charges" : GetScopeLabel(scope);
		return $"{(IsProvisional(kind) ? "Provisional-Receipt" : "Receipt")}_{scopeName}_{safeNumber}_{date:yyyy-MM-dd}.pdf";
	}
}
