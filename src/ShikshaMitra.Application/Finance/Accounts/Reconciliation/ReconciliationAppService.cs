using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

/// <summary>
/// Application service for the Finance Reconciliation inbox.
/// Thin coordinator: delegates mutations to <see cref="ReconciliationManager"/>;
/// reads go directly to the custom <see cref="IFeePaymentReceiptRepository"/> methods.
/// Mirrors LeaveManagementAppService structure from the HR module.
/// </summary>
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.View)]
public class ReconciliationAppService(
    IFeePaymentReceiptRepository receiptRepository,
    ReconciliationManager reconciliationManager,
    ICurrentUser currentUser,
    ILogger<ReconciliationAppService> logger
) : ApplicationService, IReconciliationAppService
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new(JsonSerializerDefaults.Web);

    // ── List ─────────────────────────────────────────────────────────────────

    public async Task<ReconciliationListResponse> GetListAsync(
        GetReconciliationListInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.AcademicYearId == Guid.Empty)
            throw new UserFriendlyException("AcademicYearId is required.");

        var filter = new ReconciliationListFilter
        {
            AcademicYearId     = input.AcademicYearId,
            Month              = input.Month,
            Year               = input.Year,
            PaymentMethod      = MapPaymentMethodString(input.PaymentMethod),
            VerificationStatus = input.VerificationStatus,
            Search             = input.Search,
            Page               = Math.Max(1, input.Page),
            PageSize           = Math.Clamp(input.PageSize, 1, 100),
        };

        var (totalCount, ids) = await receiptRepository
            .GetReconciliationPagedIdsAsync(filter, cancellationToken);

        var rows = ids.Count > 0
            ? await receiptRepository.GetReconciliationRowsByIdsAsync(ids, cancellationToken)
            : [];

        var tabCounts = await GetTabCountsCoreAsync(
            input.AcademicYearId, input.Month, input.Year, cancellationToken);

        var pageSize = filter.PageSize;
        return new ReconciliationListResponse
        {
            Success      = true,
            Items        = rows.Select(MapRow).ToList(),
            TotalCount   = totalCount,
            CurrentPage  = filter.Page,
            PageSize     = pageSize,
            TotalPages   = (int)Math.Ceiling((double)totalCount / pageSize),
            TabCounts    = tabCounts,
        };
    }

    // ── Tab counts ────────────────────────────────────────────────────────────

    public async Task<ReconciliationTabCountsDto> GetTabCountsAsync(
        Guid academicYearId,
        int? month,
        int? year,
        CancellationToken cancellationToken = default)
    {
        return await GetTabCountsCoreAsync(academicYearId, month, year, cancellationToken);
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add)]
    public async Task<ReconciliationActionResponse> ApproveAsync(
        Guid provisionalReceiptId,
        ApproveReconciliationInput input,
        CancellationToken cancellationToken = default)
    {
        var validator = new ApproveReconciliationRequestValidator();
        var validation = validator.Validate(input);
        if (!validation.IsValid)
            throw new UserFriendlyException(validation.Errors[0].ErrorMessage);

        var provisional = await reconciliationManager.ApproveAsync(
            provisionalReceiptId, input.Remarks, cancellationToken);

        logger.LogInformation(
            "[ReconciliationAppService] Approved: ReceiptId={ReceiptId}, FinalReceiptId={FinalId}, ActedBy={Actor}",
            provisional.Id, provisional.LinkedFinalReceiptId, currentUser.Id);

        var finalReceiptNumber = provisional.LinkedFinalReceiptId.HasValue
            ? (await receiptRepository.FindAsync(provisional.LinkedFinalReceiptId.Value, cancellationToken: cancellationToken))
                ?.ReceiptNumber
            : null;

        return BuildActionResponse(provisional, finalReceiptNumber, input.Remarks);
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add)]
    public async Task<ReconciliationActionResponse> RejectAsync(
        Guid provisionalReceiptId,
        RejectReconciliationInput input,
        CancellationToken cancellationToken = default)
    {
        var validator = new RejectReconciliationRequestValidator();
        var validation = validator.Validate(input);
        if (!validation.IsValid)
            throw new UserFriendlyException(validation.Errors[0].ErrorMessage);

        var provisional = await reconciliationManager.RejectAsync(
            provisionalReceiptId, input.Remarks, cancellationToken);

        logger.LogInformation(
            "[ReconciliationAppService] Rejected: ReceiptId={ReceiptId}, ActedBy={Actor}, Remarks={Remarks}",
            provisional.Id, currentUser.Id, input.Remarks);

        return BuildActionResponse(provisional, null, input.Remarks);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ReconciliationTabCountsDto> GetTabCountsCoreAsync(
        Guid academicYearId, int? month, int? year,
        CancellationToken cancellationToken)
    {
        var counts = await receiptRepository
            .GetReconciliationStatusCountsAsync(academicYearId, month, year, cancellationToken);

        return BuildTabCounts(counts);
    }

    private ReconciliationActionResponse BuildActionResponse(
        FeePaymentReceipt receipt,
        string? finalReceiptNumber,
        string? remarks)
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
        };
    }

    private static Dictionary<string, string?> ParseInstrumentDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "$type") continue;
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : prop.Value.ToString();
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static ReconciliationTabCountsDto BuildTabCounts(
        List<ReconciliationStatusCount> counts)
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
                case PaymentMethodEnum.UPI:
                    if (pending)  dto.UpiPending  += c.Count;
                    if (approved) dto.UpiApproved += c.Count;
                    if (rejected) dto.UpiRejected += c.Count;
                    break;
                case PaymentMethodEnum.OnlinePaymentGateway:
                    if (pending)  dto.OnlineGatewayPending  += c.Count;
                    if (approved) dto.OnlineGatewayApproved += c.Count;
                    if (rejected) dto.OnlineGatewayRejected += c.Count;
                    break;
                case PaymentMethodEnum.WalletPrepaidCard:
                    if (pending)  dto.WalletPending  += c.Count;
                    if (approved) dto.WalletApproved += c.Count;
                    if (rejected) dto.WalletRejected += c.Count;
                    break;
            }

            if (pending) dto.TotalPending += c.Count;
        }
        return dto;
    }

    /// <summary>
    /// Maps the FE payment method string to the domain enum.
    /// Supports both the exact enum name and common aliases from the UI.
    /// </summary>
    internal static PaymentMethodEnum? MapPaymentMethodString(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return null;

        return method.Trim().ToLowerInvariant() switch
        {
            "cheque"         => PaymentMethodEnum.Cheque,
            "demanddraft"    => PaymentMethodEnum.DemandDraft,
            "dd"             => PaymentMethodEnum.DemandDraft,
            "banktransfer"   => PaymentMethodEnum.BankTransfer,
            "creditcard"     => PaymentMethodEnum.CreditCard,
            "upi"            => PaymentMethodEnum.UPI,
            "onlinegateway"  => PaymentMethodEnum.OnlinePaymentGateway,
            "onlinepaymentgateway" => PaymentMethodEnum.OnlinePaymentGateway,
            "wallet"         => PaymentMethodEnum.WalletPrepaidCard,
            "walletprepaidcard" => PaymentMethodEnum.WalletPrepaidCard,
            _                => null,
        };
    }
}
