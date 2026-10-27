using System;
using System.Linq;
using FinanceGrants = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance;

namespace ShikshaMitra.Finance.Mobile;

/// <summary>Mobile operations reuse existing Finance grants; this is not a permission catalogue.</summary>
public static class MobileFinancePermissions
{
	public const string View = FinanceGrants.Fees_FeeCollections.View;
	public const string ViewCharges = FinanceGrants.Fees_Charges.View;
	public const string ViewReceipts = FinanceGrants.Fees_InvoiceReceipt.View;
	public const string ViewConcessions = FinanceGrants.Fees_Concession.View;
	public const string ViewRefunds = FinanceGrants.Fees_Refund.View;
	public const string ViewAdjustments = FinanceGrants.Fees_Adjustment.View;
	public const string DownloadReceipts = ViewReceipts;
	public const string DownloadStatement = View;
	public const string RequestConcession = FinanceGrants.Fees_Concession.Add;
	public const string EditConcession = FinanceGrants.Fees_Concession.Edit;
	public const string SubmitConcession = FinanceGrants.Fees_Concession.Submit;
	public const string CancelConcession = FinanceGrants.Fees_Concession.Cancel;
	public const string UploadConcessionDocument = FinanceGrants.Fees_Concession.UploadDocument;
	public const string ViewConcessionDocument = FinanceGrants.Fees_Concession.ViewDocument;
	public const string RequestRefund = FinanceGrants.Fees_Refund.Add;
	public const string SubmitRefund = FinanceGrants.Fees_Refund.Submit;
	public const string RequestAdjustment = FinanceGrants.Fees_Adjustment.Add;
	public static readonly string[] All = [View, ViewCharges, ViewReceipts, ViewConcessions, ViewRefunds, ViewAdjustments,
		RequestConcession, EditConcession, SubmitConcession, CancelConcession, UploadConcessionDocument,
		ViewConcessionDocument, RequestRefund, SubmitRefund, RequestAdjustment];
	public static bool IsSupported(string permission) => All.Contains(permission, StringComparer.Ordinal);
	public static bool RequiresGuardian(string permission) => permission is RequestConcession or EditConcession or
		SubmitConcession or CancelConcession or UploadConcessionDocument or RequestRefund or SubmitRefund or RequestAdjustment;
}
