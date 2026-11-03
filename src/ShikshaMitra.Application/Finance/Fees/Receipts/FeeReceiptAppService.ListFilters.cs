using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Receipts;

public partial class FeeReceiptAppService
{
	private static readonly IReadOnlyDictionary<string, FinanceTableColumn> ListFilterColumns =
		new Dictionary<string, FinanceTableColumn>
		{
			["receiptNumber"] = new(FinanceTableValueType.Text),
			["studentName"] = new(FinanceTableValueType.Text),
			["receiptKind"] = new(FinanceTableValueType.Text),
			["paymentMethodLabel"] = new(FinanceTableValueType.Text),
			["verificationStatus"] = new(FinanceTableValueType.Text),
			["rollNo"] = new(FinanceTableValueType.Text),
			["totalPaidAmount"] = new(FinanceTableValueType.Number),
			["receiptDate"] = new(FinanceTableValueType.Date)
		};

	// These expressions run only over the complete authorized DTO projection, after repository scope/search.
	private static readonly IReadOnlyDictionary<string, LambdaExpression> ListFilterFields =
		new Dictionary<string, LambdaExpression>
		{
			["receiptNumber"] = (Expression<Func<FeeReceiptListItemDto, string>>)(r => r.ReceiptNumber),
			["studentName"] = (Expression<Func<FeeReceiptListItemDto, string>>)(r =>
				r.GrNo.HasValue && r.GrNo.Value != 0 ? r.StudentName + " (" + r.GrNo.Value + ")" : r.StudentName),
			["receiptKind"] = (Expression<Func<FeeReceiptListItemDto, string>>)(r => r.ReceiptKindLabel),
			["paymentMethodLabel"] = (Expression<Func<FeeReceiptListItemDto, string>>)(r =>
				string.IsNullOrEmpty(r.PaymentMethodLabel) ? "Not recorded" : r.PaymentMethodLabel),
			["verificationStatus"] = (Expression<Func<FeeReceiptListItemDto, string>>)(r => VerificationDisplay(r)),
			["rollNo"] = (Expression<Func<FeeReceiptListItemDto, string>>)(r => r.RollNo ?? "—"),
			["totalPaidAmount"] = (Expression<Func<FeeReceiptListItemDto, double>>)(r => r.TotalPaidAmount),
			["receiptDate"] = (Expression<Func<FeeReceiptListItemDto, DateTime>>)(r =>
				r.ReceiptDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
		};

	private static FinanceTableQuery ParseListFilters(string? filters)
	{
		try
		{
			return FinanceTableQuery.Parse(filters, null, null, null, ListFilterColumns);
		}
		catch (ArgumentNullException ex) when (ex.ParamName == "key")
		{
			// A JSON null column reaches the shared parser's dictionary before its validation.
			throw new AbpValidationException("Invalid finance table query.",
				[new ValidationResult("Choose a supported column, operator and value.", ["Filters"])]);
		}
	}

	private static string VerificationDisplay(FeeReceiptListItemDto receipt) =>
		!string.IsNullOrEmpty(receipt.LinkedFinalReceiptNumber) ? "Receipt: " + receipt.LinkedFinalReceiptNumber :
		receipt.VerificationStatus switch
		{
			FeeReceiptVerificationStatus.PendingVerification => "Pending",
			FeeReceiptVerificationStatus.Verified => "Verified",
			FeeReceiptVerificationStatus.Rejected => "Rejected",
			_ => "Not required"
		};
}
