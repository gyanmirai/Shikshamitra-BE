using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Events;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> RecordPayoutAsync(Guid id, RecordRefundPayoutDto input)
    {
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey))
            throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        EnsureSettlementMethodAllowed(request, input.Method);
        var reference = input.Method == "Cash" && string.IsNullOrWhiteSpace(input.ExternalReference)
            ? $"CASH-{request.VoucherNumber}"
            : input.ExternalReference?.Trim();
        var priorPayout = request.Settlements.FirstOrDefault(x => x.IdempotencyKey == input.IdempotencyKey);
        if (priorPayout != null)
        {
            if (priorPayout.Method != input.Method || priorPayout.Amount != input.Amount ||
                (priorPayout.SettlementDate - input.PaymentDate).Duration() > TimeSpan.FromMilliseconds(1) ||
                priorPayout.ExternalReference != reference ||
                priorPayout.BankAccountId != input.BankAccountId)
                throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
            return await MapAsync(request);
        }
        if ((request.RefundMethod != null && request.RefundMethod != input.Method) ||
            input.Amount != request.ApprovedAmount ||
            input.PaymentDate == default || string.IsNullOrWhiteSpace(input.EvidenceReference) ||
            string.IsNullOrWhiteSpace(reference) ||
            (input.Method != "Cash" && !input.BankAccountId.HasValue))
            throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
        if (request.ExpenseCategoryId.HasValue && request.PayingBankAccountId != input.BankAccountId)
            throw new BusinessException("Finance:Routing:BankMismatch");
        if (input.BankAccountId.HasValue)
        {
            var bank = await bankAccountRepository.FindAsync(input.BankAccountId.Value);
            if (bank == null || !bank.IsActive || bank.AcademicYearId != request.AcademicYearId ||
                bank.TenantId != CurrentTenant.Id)
                throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
        }
        if (await feeRefundRepository.IsPayoutReferenceUsedAsync(input.Method,
            reference!, request.Id))
            throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
        var payout = request.RecordPayout(GuidGenerator.Create(), currentUser.GetId(), input.IdempotencyKey,
            input.Method, input.Amount, input.PaymentDate, reference, null,
            input.BankAccountId, input.EvidenceReference);
        await feeRefundRepository.StageNewSettlementAsync(payout);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Refund payout recorded for {request.VoucherNumber}; awaiting verification.",
            context: new { PayoutId = payout.Id, input.Method, ExternalReference = reference });
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.VerifyPayout)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> ConfirmPayoutAsync(Guid id, Guid payoutId, ResolveRefundPayoutDto input)
    {
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
            string.IsNullOrWhiteSpace(input.EvidenceReference))
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        var payout = request.Settlements.SingleOrDefault(x => x.Id == payoutId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        if (payout.VerificationKey == input.IdempotencyKey && request.Status == FeeRefundStatus.Processed)
        {
            if (payout.EvidenceReference != input.EvidenceReference)
                throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
            return await MapAsync(request);
        }
        if (payout.InitiatedByUserId == currentUser.GetId() && payout.Method != "Cash")
            throw new BusinessException(FeeRefundErrorCodes.MakerCheckerViolation);
        await feeRefundManager.ConfirmPayoutAsync(request, payoutId, currentUser.GetId(),
            input.IdempotencyKey, input.EvidenceReference);
        var amount = request.Lines.Sum(x => x.ProcessedAmount - x.ReversedAmount);
        if (amount != request.ProcessedAmount || amount != payout.Amount ||
            request.Lines.Any(x => x.SourceAllocationId == null || x.SourceReceiptId != request.OriginalReceiptId))
            throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
        await refundAccountingRepository.InsertAsync(new FeeRefundAccountingEntry(
            GuidGenerator.Create(), CurrentTenant.Id, request.Id, payout.Id,
            request.OriginalReceiptId, payout.BankAccountId, payout.Method,
            payout.ExternalReference, amount, false, null, FeeHeadBreakdown(request),
            input.EvidenceReference, currentUser.GetId()), autoSave: false);
        if (request.ExpenseCategoryId is { } categoryId)
            await automaticTransactions.RecordAsync(request.AcademicYearId, categoryId,
                request.ExpenseCategoryName!, payout.BankAccountId, request.PayingBankLabel,
                request.VoucherNumber, amount, PayoutMethodValue(payout.Method),
                ShikshaMitra.Finance.Accounts.Transactions.TransactionType.Expense,
                $"refund:{payout.Id:N}", request.Id, request.StudentId, paymentDate: payout.SettlementDate.UtcDateTime,
                financeFeeAccountId: request.FinanceFeeAccountId);
        await WriteAuditAsync(request, FinanceAuditActions.Refund,
            $"Refund {request.VoucherNumber} confirmed paid.", context: new { PayoutId = payoutId });
        await eventBus.PublishAsync(new FeeRefundApprovedEto
        {
            RefundRequestId = request.Id, StudentId = request.StudentId, FinanceFeeAccountId = request.FinanceFeeAccountId,
            AcademicYearId = request.AcademicYearId, TotalRefundAmount = Convert.ToDouble(request.ProcessedAmount),
            TenantId = CurrentTenant.Id, ApprovedAt = Clock.Now
        });
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.FailPayout)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> FailPayoutAsync(Guid id, Guid payoutId, ResolveRefundPayoutDto input)
    {
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
            string.IsNullOrWhiteSpace(input.EvidenceReference))
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        var payout = request.Settlements.SingleOrDefault(x => x.Id == payoutId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        if (payout.VerificationKey == input.IdempotencyKey && request.Status == FeeRefundStatus.Failed)
        {
            if (payout.EvidenceReference != input.EvidenceReference ||
                payout.FailureReason != input.Reason)
                throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
            return await MapAsync(request);
        }
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        if (payout.InitiatedByUserId == currentUser.GetId())
            throw new BusinessException(FeeRefundErrorCodes.MakerCheckerViolation);
        request.FailPayout(payoutId, currentUser.GetId(), input.IdempotencyKey,
            input.Reason, input.EvidenceReference);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Refund payout failed for {request.VoucherNumber}.", input.Reason,
            new { PayoutId = payoutId });
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> ReturnPayoutAsync(Guid id, ReturnRefundPayoutDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) ||
            string.IsNullOrWhiteSpace(input.EvidenceReference))
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        var original = (await refundAccountingRepository.GetListAsync(x =>
            x.RefundRequestId == id && !x.IsCorrection)).SingleOrDefault();
        if (original == null || original.SettlementId != input.SettlementId ||
            request.ProcessedAmount - request.ReversedAmount != original.Amount)
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        await feeRefundManager.ReturnPayoutAsync(request, input.SettlementId, currentUser.GetId(), input.Reason);
        if (request.ProcessedAmount - request.ReversedAmount != 0 ||
            request.Lines.Sum(x => x.ProcessedAmount - x.ReversedAmount) != 0)
            throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
        await refundAccountingRepository.InsertAsync(new FeeRefundAccountingEntry(
            GuidGenerator.Create(), CurrentTenant.Id, request.Id, input.SettlementId,
            original.OriginalReceiptId, original.BankAccountId, original.Method,
            original.ExternalReference, original.Amount, true, original.Id,
            original.FeeHeadAmountsJson, input.EvidenceReference, currentUser.GetId()), autoSave: false);
        await automaticTransactions.ReverseAsync(request.Id);
        await WriteAuditAsync(request, FinanceAuditActions.Reverse,
            $"Refund payout returned for {request.VoucherNumber}.", input.Reason,
            new { input.SettlementId, input.EvidenceReference });
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    private static int PayoutMethodValue(string method) => method switch
    { "Cash" => 0, "Cheque" => 1, "DemandDraft" or "Demand Draft (DD)" => 2, "BankTransfer" or "Bank Transfer (NEFT/RTGS/IMPS)" => 3, "Upi" or "UPI" => 6,
      _ => throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed) };
    private static string FeeHeadBreakdown(FeeRefundRequest request) => JsonSerializer.Serialize(
        request.Lines.Select(x => new
        {
            x.FeeHeadId,
            x.DueFeeId,
            x.SourceAllocationId,
            x.SourceReceiptId,
            Amount = x.ProcessedAmount - x.ReversedAmount
        }).OrderBy(x => x.FeeHeadId).ThenBy(x => x.SourceAllocationId).ToArray());
}
