using System;
using System.Linq;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class EfCoreFinanceExternalRefundEvidenceQuery
{
	private static IQueryable<FinanceExternalRefundEvidenceInput> ReadFacts(ShikshaMitraDbContext db, Guid school,
		IQueryable<Guid> relevant)
	{
		var lines = db.Set<FeeRefundRequest>().Where(request => request.TenantId == school)
			.SelectMany(request => request.Lines, (request, line) => new { Request = request, Line = line });
		return from row in lines
			join link in db.Set<FinanceExternalReceivableSourceLink>().Where(link => link.TenantId == school)
				on row.Request.FinanceExternalSourceLinkId equals (Guid?)link.Id into ownerJoin
			from owner in ownerJoin.DefaultIfEmpty()
			join actual in db.Set<Charge>().Where(charge => charge.TenantId == school)
				on (Guid?)owner.FinanceChargeId equals (Guid?)actual.Id into chargeJoin
			from charge in chargeJoin.DefaultIfEmpty()
			join allocationRow in db.Set<ChargeCollectionRecord>().Where(allocation => allocation.TenantId == school)
				on row.Line.SourceAllocationId equals (Guid?)allocationRow.Id into allocationJoin
			from allocation in allocationJoin.DefaultIfEmpty()
			join headerRow in db.Set<FeeRecordMetadata>().Where(header => header.TenantId == school)
				on (Guid?)allocation.FeeRecordMetadataId equals (Guid?)headerRow.Id into headerJoin
			from header in headerJoin.DefaultIfEmpty()
			join receiptRow in db.Set<FeePaymentReceipt>().Where(receipt => receipt.TenantId == school)
				on row.Line.SourceReceiptId equals (Guid?)receiptRow.Id into receiptJoin
			from receipt in receiptJoin.DefaultIfEmpty()
			where relevant.Contains(owner.FinanceChargeId) || row.Line.ChargeId.HasValue && relevant.Contains(row.Line.ChargeId!.Value)
			select new FinanceExternalRefundEvidenceInput
			{
				TenantId = school, RequestId = row.Request.Id, RequestTenant = row.Request.TenantId,
				RequestYear = row.Request.AcademicYearId, RequestStudent = row.Request.StudentId,
				RequestEmployee = row.Request.EmployeeId, RequestAccount = row.Request.FinanceFeeAccountId,
				RequestCurrency = row.Request.Currency, RequestLinkId = row.Request.FinanceExternalSourceLinkId,
				OriginalReceiptRepresented = row.Request.OriginalReceiptId.HasValue &&
					row.Request.Lines.Any(other => other.SourceReceiptId == row.Request.OriginalReceiptId),
				OwnerLinkId = (Guid?)owner.Id, OwnerTenant = owner.TenantId, OwnerChargeId = (Guid?)owner.FinanceChargeId,
				OwnerYear = (Guid?)owner.AcademicYearId, OwnerKind = owner.SourceKind,
				ActualChargeId = (Guid?)charge.Id, ChargeTenant = charge.TenantId, ChargeYear = (Guid?)charge.AcademicYearId,
				ChargeStudent = charge.StudentId, ChargeEmployee = charge.EmployeeId,
				LineRequestId = row.Line.FeeRefundRequestId, ClaimedChargeId = row.Line.ChargeId,
				DueFeeId = row.Line.DueFeeId, LateFeeId = row.Line.LateFeeDueFeeId,
				Requested = row.Line.RequestedAmount, Calculated = row.Line.CalculatedAmount, Approved = row.Line.ApprovedAmount,
				Processed = row.Line.ProcessedAmount, Reversed = row.Line.ReversedAmount, SettledPaid = row.Line.SettledPaidAmount,
				LineAllocationId = row.Line.SourceAllocationId, AllocationId = (Guid?)allocation.Id,
				AllocationTenant = allocation.TenantId, AllocationChargeId = (Guid?)allocation.ChargeId,
				AllocationStudent = allocation.StudentId, AllocationEmployee = allocation.EmployeeId,
				AllocationHeaderId = (Guid?)allocation.FeeRecordMetadataId, AllocationAmount = (decimal?)allocation.Amount,
				HeaderId = (Guid?)header.Id, HeaderTenant = header.TenantId, HeaderYear = (Guid?)header.AcademicYearId,
				HeaderStudent = header.StudentId, HeaderEmployee = header.EmployeeId, HeaderAccount = header.FinanceFeeAccountId,
				HeaderCurrency = header.Currency, HeaderAmount = (decimal?)header.TotalPaidFee, HeaderStatus = header.Status,
				LineReceiptId = row.Line.SourceReceiptId, ReceiptId = (Guid?)receipt.Id, ReceiptTenant = receipt.TenantId,
				ReceiptYear = receipt.AcademicYearId, ReceiptStudent = receipt.StudentId, ReceiptEmployee = receipt.EmployeeId,
				ReceiptAccount = receipt.FinanceFeeAccountId, ReceiptHeaderId = (Guid?)receipt.FeeCollectionTransactionId,
				ReceiptCurrency = receipt.Currency, ReceiptAmount = (decimal?)receipt.TotalPaidAmount,
				ReceiptKind = receipt.ReceiptKind, ReceiptScope = receipt.ReceiptScope, ReceiptVerification = receipt.VerificationStatus
			};
	}
}
