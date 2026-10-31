using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public partial class ReconciliationAppService
{
	private static readonly IReadOnlyDictionary<string, LambdaExpression> ListFilterFields =
		new Dictionary<string, LambdaExpression>
		{
			["studentName"] = (Expression<Func<ReconciliationListItemDto, string>>)(r =>
				DisplayOrDash(r.StudentName) + (r.GrNumber.HasValue && r.GrNumber.Value != 0 ? " (" + r.GrNumber.Value + ")" : "")),
			["board"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => CombinedDisplay(r.Board, r.Medium)),
			["standardName"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => CombinedDisplay(r.Class, r.Division)),
			["class"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => CombinedDisplay(r.Class, r.Division)),
			["amount"] = (Expression<Func<ReconciliationListItemDto, double>>)(r => r.Amount),
			["status"] = (Expression<Func<ReconciliationListItemDto, int>>)(r => r.Status),
			["remarks"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r =>
				r.PaymentMethod == PaymentMethodEnum.Cheque || r.PaymentMethod == PaymentMethodEnum.UPI ? DisplayOrDash(r.Remarks) : r.Remarks),
			["receiptNumber"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => r.ReceiptNumber),
			["chequeNo"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r => Detail(r, "chequeNumber")),
			["bankName"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r => BankNameDisplay(r)),
			["receivedChequeDate"] = (Expression<Func<ReconciliationListItemDto, DateTimeOffset?>>)(r => DetailDate(r, "chequeDate")),
			["ddNumber"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r => Detail(r, "ddNumber")),
			["ddIssueDate"] = (Expression<Func<ReconciliationListItemDto, DateTimeOffset?>>)(r => DetailDate(r, "ddDate")),
			["paymentDate"] = (Expression<Func<ReconciliationListItemDto, DateTimeOffset?>>)(r => PaymentDateDisplay(r)),
			["transactionId"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r =>
				Detail(r, r.PaymentMethod == PaymentMethodEnum.BankTransfer ? "transactionReferenceNumber" : "transactionId")),
			["transferDate"] = (Expression<Func<ReconciliationListItemDto, DateTimeOffset?>>)(r => DetailDate(r, "transferDate")),
			["accountHolderName"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r => Detail(r, "accountHolderName")),
			["bankLabel"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => DisplayOrDash(r.BankLabel)),
			["cardHolderName"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r => Detail(r, "cardHolderName")),
			["last4Digits"] = (Expression<Func<ReconciliationListItemDto, string?>>)(r => CardDisplay(r)),
			["upiId"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => DisplayOrDash(Detail(r, "upiId"))),
			["upiTransactionId"] = (Expression<Func<ReconciliationListItemDto, string>>)(r => DisplayOrDash(Detail(r, "upiTransactionId"))),
			["receiptDate"] = (Expression<Func<ReconciliationListItemDto, DateTime>>)(r =>
				r.ReceiptDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
		};

	private static FinanceTableQuery ParseListFilters(string? filters, PaymentMethodEnum? method)
	{
		var columns = new Dictionary<string, FinanceTableColumn>
		{
			["studentName"] = new(FinanceTableValueType.Text), ["board"] = new(FinanceTableValueType.Text),
			["standardName"] = new(FinanceTableValueType.Text), ["class"] = new(FinanceTableValueType.Text),
			["amount"] = new(FinanceTableValueType.Number), ["status"] = new(FinanceTableValueType.Enum, Values: [0, 1, 2]),
			["remarks"] = new(FinanceTableValueType.Text)
		};
		void Add(PaymentMethodEnum selected, string[] text, string[] dates)
		{
			if (method.HasValue && method != selected) return;
			foreach (var column in text) columns[column] = new(FinanceTableValueType.Text);
			foreach (var column in dates) columns[column] = new(FinanceTableValueType.Date);
		}
		Add(PaymentMethodEnum.Cheque, ["chequeNo", "bankName"], ["receivedChequeDate"]);
		Add(PaymentMethodEnum.DemandDraft, ["ddNumber", "bankName"], ["ddIssueDate", "paymentDate"]);
		Add(PaymentMethodEnum.BankTransfer, ["transactionId", "accountHolderName", "bankLabel"], ["transferDate"]);
		Add(PaymentMethodEnum.CreditCard, ["cardHolderName", "last4Digits", "transactionId"], ["paymentDate"]);
		Add(PaymentMethodEnum.DebitCard, ["cardHolderName", "last4Digits", "transactionId"], ["paymentDate"]);
		Add(PaymentMethodEnum.UPI, ["receiptNumber", "upiId", "upiTransactionId"], ["receiptDate"]);
		try
		{
			return FinanceTableQuery.Parse(filters, null, null, null, columns);
		}
		catch (ArgumentNullException ex) when (ex.ParamName == "key")
		{
			throw new AbpValidationException("Invalid finance table query.",
				[new ValidationResult("Choose a supported column, operator and value.", ["Filters"])]);
		}
	}

	private static string DisplayOrDash(string? value) => string.IsNullOrEmpty(value) ? "-" : value;
	private static string CombinedDisplay(string? primary, string? secondary) =>
		DisplayOrDash(primary) + (string.IsNullOrEmpty(secondary) ? "" : " - (" + secondary + ")");
	private static string? Detail(ReconciliationListItemDto row, string key) => row.InstrumentDetails.GetValueOrDefault(key);
	private static DateTimeOffset? DetailDate(ReconciliationListItemDto row, string key) => ParseDisplayDate(Detail(row, key));
	private static DateTimeOffset? ParseDisplayDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
		DateTimeStyles.AssumeUniversal, out var date) ? date : null;

	private static string? BankNameDisplay(ReconciliationListItemDto row) => row.PaymentMethod switch
	{
		PaymentMethodEnum.Cheque => Detail(row, "bankName"),
		PaymentMethodEnum.DemandDraft => string.IsNullOrEmpty(Detail(row, "issuingBankName"))
			? Detail(row, "branchName") : Detail(row, "issuingBankName"),
		// Other methods do not display the payer's bankName instrument value.
		_ => null
	};

	private static string? CardDisplay(ReconciliationListItemDto row)
	{
		var last4 = Detail(row, "cardLast4");
		return string.IsNullOrEmpty(last4) ? null : "•••• " + last4[Math.Max(0, last4.Length - 4)..];
	}

	private static DateTimeOffset? PaymentDateDisplay(ReconciliationListItemDto row)
	{
		if (row.PaymentMethod == PaymentMethodEnum.DemandDraft)
		{
			var paymentDate = Detail(row, "paymentDate");
			return paymentDate is null ? row.PaymentDate : ParseDisplayDate(paymentDate);
		}
		if (row.PaymentMethod is PaymentMethodEnum.CreditCard or PaymentMethodEnum.DebitCard)
		{
			var transactionDate = Detail(row, "transactionDate");
			return string.IsNullOrEmpty(transactionDate)
				? new DateTimeOffset(row.ReceiptDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
				: ParseDisplayDate(transactionDate);
		}
		return null;
	}
}
