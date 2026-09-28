using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public partial class ReconciliationAppService
{
    private async Task<ReconciliationTabCountsDto> GetTabCountsCoreAsync(
        Guid academicYearId, int? month, int? year,
        IQueryable<Guid> scopedStudentIds,
        CancellationToken cancellationToken)
    {
        var counts = await receiptRepository
            .GetReconciliationStatusCountsAsync(academicYearId, month, year, scopedStudentIds, cancellationToken);
        return BuildTabCounts(counts);
    }

    private ReconciliationActionResponse BuildActionResponse(
        FeePaymentReceipt receipt, string? finalReceiptNumber, string? remarks)
    {
        var statusInt = receipt.VerificationStatus switch
        {
            FeeReceiptVerificationStatus.Verified => 1,
            FeeReceiptVerificationStatus.Rejected => 2,
            _                                     => 0,
        };
        var statusLabel = receipt.VerificationStatus switch
        {
            FeeReceiptVerificationStatus.Verified => "Approved",
            FeeReceiptVerificationStatus.Rejected => "Rejected",
            _                                     => "Pending",
        };

        return new ReconciliationActionResponse
        {
            Success = true,
            Data = new ReconciliationActionData
            {
                ReceiptId          = receipt.Id,
                VerificationStatus = receipt.VerificationStatus,
                Status             = statusInt,
                StatusLabel        = statusLabel,
                FinalReceiptNumber = finalReceiptNumber,
                ActedBy            = currentUser.Id ?? Guid.Empty,
                ActedByName        = currentUser.Name ?? currentUser.UserName ?? string.Empty,
                ActedOn            = DateTimeOffset.UtcNow,
                Remarks            = remarks,
            },
            Message = statusLabel,
        };
    }

    private static ReconciliationListItemDto MapRow(ReconciliationRow row)
    {
        var statusInt = row.VerificationStatus switch
        {
            FeeReceiptVerificationStatus.Verified => 1,
            FeeReceiptVerificationStatus.Rejected => 2,
            _                                     => 0,
        };
        var statusLabel = row.VerificationStatus switch
        {
            FeeReceiptVerificationStatus.Verified => "Approved",
            FeeReceiptVerificationStatus.Rejected => "Rejected",
            _                                     => "Pending",
        };

        return new ReconciliationListItemDto
        {
            Id                         = row.ReceiptId,
            ProvisionalReceiptId       = row.ReceiptId,
            ReceiptNumber              = row.ReceiptNumber,
            StudentId                  = row.StudentId,
            StudentName                = row.StudentName,
            GrNumber                   = row.GrNumber,
            RollNumber                 = row.RollNumber,
            Board                      = row.Board,
            Medium                     = row.Medium,
            Class                      = row.Class,
            Division                   = row.Division,
            FeeCollectionTransactionId = row.FeeCollectionTransactionId,
            PaymentMethod              = row.PaymentMethod,
            PaymentMethodLabel         = row.PaymentMethod.ToString(),
            Amount                     = row.TotalPaidAmount,
            InstrumentDetails          = ParseInstrumentDetails(row.PaymentDetailsJson),
            Status                     = statusInt,
            StatusLabel                = statusLabel,
            VerificationStatus         = row.VerificationStatus,
            FinalReceiptNumber         = row.LinkedFinalReceiptNumber,
            ReceiptDate                = row.ReceiptDate,
            Remarks                    = row.Remarks,
            FinalReceiptId             = row.LinkedFinalReceiptId,
        };
    }

    private static Dictionary<string, string?> ParseInstrumentDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "$type") continue;
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
            }
            return result;
        }
        catch { return []; }
    }

    private static ReconciliationTabCountsDto BuildTabCounts(List<ReconciliationStatusCount> counts)
    {
        var dto = new ReconciliationTabCountsDto();
        foreach (var c in counts)
        {
            var pending  = c.VerificationStatus == FeeReceiptVerificationStatus.PendingVerification;
            var approved = c.VerificationStatus == FeeReceiptVerificationStatus.Verified;
            var rejected = c.VerificationStatus == FeeReceiptVerificationStatus.Rejected;

            switch (c.PaymentMethod)
            {
                case PaymentMethodEnum.Cheque:
                    if (pending)  dto.ChequePending  += c.Count;
                    if (approved) dto.ChequeApproved += c.Count;
                    if (rejected) dto.ChequeRejected += c.Count;
                    break;
                case PaymentMethodEnum.DemandDraft:
                    if (pending)  dto.DemandDraftPending  += c.Count;
                    if (approved) dto.DemandDraftApproved += c.Count;
                    if (rejected) dto.DemandDraftRejected += c.Count;
                    break;
                case PaymentMethodEnum.BankTransfer:
                    if (pending)  dto.BankTransferPending  += c.Count;
                    if (approved) dto.BankTransferApproved += c.Count;
                    if (rejected) dto.BankTransferRejected += c.Count;
                    break;
                case PaymentMethodEnum.CreditCard:
                    if (pending)  dto.CreditCardPending  += c.Count;
                    if (approved) dto.CreditCardApproved += c.Count;
                    if (rejected) dto.CreditCardRejected += c.Count;
                    break;
                case PaymentMethodEnum.DebitCard:
                    if (pending)  dto.DebitCardPending  += c.Count;
                    if (approved) dto.DebitCardApproved += c.Count;
                    if (rejected) dto.DebitCardRejected += c.Count;
                    break;
                case PaymentMethodEnum.UPI:
                    if (pending)  dto.UpiPending  += c.Count;
                    if (approved) dto.UpiApproved += c.Count;
                    if (rejected) dto.UpiRejected += c.Count;
                    break;
            }

            if (pending) dto.TotalPending += c.Count;
        }
        return dto;
    }
}
