using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public sealed record RefundLineSpec(Guid DueFeeId, Guid? SourceAllocationId, Guid? SourceReceiptId,
    Guid FeeHeadId, string FeeHeadName, string InstallmentLabel, decimal SettledPaidAmount,
    decimal Amount, decimal CalculatedAmount);

public class FeeRefundManager(IFeeRefundRepository repository, IDueFeeRepository dueFeeRepository,
    ICurrentTenant currentTenant) : DomainService
{
    public async Task<FeeRefundRequest> CreatePendingAsync(Guid studentId, Guid academicYearId,
        Guid assignmentId, Guid policyId, Guid policyVersionId, Guid receiptId, string receiptNumber,
        IReadOnlyCollection<RefundLineSpec> lines, string? method, RefundPaymentDetails? details,
        string? reason, string policySnapshot, string calculationSnapshot, string calculationHash,
        Guid submittedBy, string idempotencyKey, Guid? replacesRequestId)
    {
        await ValidateLinesAsync(studentId, assignmentId, lines, null);
        var request = new FeeRefundRequest(GuidGenerator.Create(), currentTenant.Id, studentId,
            academicYearId, GenerateVoucherNumber(), method, details, reason);
        request.PinPolicy(assignmentId, policyId, policyVersionId, receiptId, receiptNumber,
            policySnapshot, calculationSnapshot, calculationHash);
        foreach (var line in lines)
            request.AddLine(ToEntity(request.Id, line));
        request.UseSimpleWorkflow(submittedBy, idempotencyKey, replacesRequestId);
        return await repository.InsertAsync(request, autoSave: false);
    }

    public async Task<FeeRefundRequest> CreateFromConcessionAsync(Guid studentId, Guid academicYearId,
        Guid assignmentId, Guid concessionRequestId, Guid receiptId, string receiptNumber,
        IReadOnlyCollection<RefundLineSpec> lines, string calculationSnapshot, string calculationHash,
        Guid submittedBy, string? method = null, RefundPaymentDetails? details = null,
        string? reason = null, string? idempotencyKey = null, Guid? replacesRequestId = null)
    {
        await ValidateLinesAsync(studentId, assignmentId, lines, null);
        var request = new FeeRefundRequest(GuidGenerator.Create(), currentTenant.Id, studentId,
            academicYearId, GenerateVoucherNumber(), method, details,
            reason ?? $"Approved concession {concessionRequestId}");
        request.PinConcession(assignmentId, concessionRequestId, receiptId, receiptNumber,
            calculationSnapshot, calculationHash);
        foreach (var line in lines)
            request.AddLine(ToEntity(request.Id, line));
        request.UseSimpleWorkflow(submittedBy, idempotencyKey, replacesRequestId);
        return await repository.InsertAsync(request, autoSave: false);
    }

    public async Task UpdateDraftAsync(FeeRefundRequest request, IReadOnlyCollection<RefundLineSpec> lines,
        string? method, RefundPaymentDetails? details, string? reason,
        string calculationSnapshot, string calculationHash)
    {
        if (request.Status != FeeRefundStatus.Draft)
            throw new BusinessException(FeeRefundErrorCodes.CannotModifyNonPendingRefund);
        if (!request.StudentFeeStructureId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        await ValidateLinesAsync(request.StudentId, request.StudentFeeStructureId.Value, lines, request.Id);
        request.ReplaceLines(lines.Select(x => ToEntity(request.Id, x)).ToList());
        request.UpdatePaymentDetails(method, details);
        request.UpdateReason(reason);
        request.UpdateEvidence(calculationSnapshot, calculationHash);
    }

    public async Task ApproveAsync(FeeRefundRequest request, Guid userId, string? notes)
    {
        var dueIds = request.Lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = (await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id))).ToDictionary(x => x.Id);
        var reserved = await repository.GetActiveReservedAmountsAsync(dueIds, request.Id);
        var allocationIds = request.Lines.Where(x => x.SourceAllocationId.HasValue)
            .Select(x => x.SourceAllocationId!.Value).Distinct().ToList();
        var allocationCommitted = await repository.GetCommittedAllocationAmountsAsync(allocationIds, request.Id);
        var reservations = new List<FeeRefundReservation>();
        var dueRemaining = dues.ToDictionary(x => x.Key,
            x => x.Value.RefundableBalance - reserved.GetValueOrDefault(x.Key));
        foreach (var line in request.Lines)
        {
            if (!dues.TryGetValue(line.DueFeeId, out var due))
                throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
            var available = dueRemaining[line.DueFeeId];
            if (line.CalculatedAmount > available)
                throw new BusinessException(FeeRefundErrorCodes.ReservationConflict)
                    .WithData("DueFeeId", line.DueFeeId).WithData("Requested", line.CalculatedAmount).WithData("Available", available);
            if (line.SourceAllocationId.HasValue)
            {
                var allocationAvailable = Math.Max(0m, line.SettledPaidAmount -
                    allocationCommitted.GetValueOrDefault(line.SourceAllocationId.Value));
                if (line.CalculatedAmount > allocationAvailable)
                    throw new BusinessException(FeeRefundErrorCodes.ReservationConflict)
                        .WithData("SourceAllocationId", line.SourceAllocationId.Value)
                        .WithData("Requested", line.CalculatedAmount).WithData("Available", allocationAvailable);
            }
            reservations.Add(new FeeRefundReservation(GuidGenerator.Create(), request.Id, line.Id,
                line.DueFeeId, line.SourceAllocationId, line.CalculatedAmount));
            dueRemaining[line.DueFeeId] -= line.CalculatedAmount;
        }
        request.Approve(userId, reservations, notes);
    }

    public async Task<FeeRefundSettlement> ProcessAsync(FeeRefundRequest request, Guid userId,
        string idempotencyKey, string method, decimal amount, DateTimeOffset date,
        string? reference, string? paymentDetailsJson)
    {
        var existing = request.Settlements.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (existing?.Status == FeeRefundSettlementStatus.Processed) return existing;
        var settlement = request.StartProcessing(GuidGenerator.Create(), userId, idempotencyKey,
            method, amount, date, reference, paymentDetailsJson);
        if (settlement.Status == FeeRefundSettlementStatus.Processed) return settlement;
        var dueIds = request.Lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = (await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id))).ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
        {
            if (!dues.TryGetValue(line.DueFeeId, out var due)) throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
            due.ApplyRefund(line.ApprovedAmount);
        }
        await dueFeeRepository.UpdateManyAsync(dues.Values, autoSave: false);
        request.CompleteSettlement(settlement.Id, userId);
        return settlement;
    }

    public async Task ConfirmPayoutAsync(FeeRefundRequest request, Guid settlementId, Guid userId,
        string key, string evidenceReference)
    {
        var dueIds = request.Lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = (await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id))).ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
        {
            if (!dues.TryGetValue(line.DueFeeId, out var due))
                throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
            due.ApplyRefund(line.ApprovedAmount);
        }
        request.ConfirmPayout(settlementId, userId, key, evidenceReference);
        await dueFeeRepository.UpdateManyAsync(dues.Values, autoSave: false);
    }

    public async Task ReturnPayoutAsync(FeeRefundRequest request, Guid settlementId, Guid userId,
        string reason)
    {
        var dueIds = request.Lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = (await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id))).ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
        {
            if (!dues.TryGetValue(line.DueFeeId, out var due))
                throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
            due.ReverseRefund(line.ProcessedAmount);
        }
        request.RecordReturnedPayout(settlementId, userId, reason);
        await dueFeeRepository.UpdateManyAsync(dues.Values, autoSave: false);
    }

    public async Task ReverseAsync(FeeRefundRequest request, Guid settlementId, Guid userId, string reason)
    {
        var dueIds = request.Lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = (await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id))).ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
        {
            if (!dues.TryGetValue(line.DueFeeId, out var due)) throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
            due.ReverseRefund(line.ProcessedAmount);
        }
        await dueFeeRepository.UpdateManyAsync(dues.Values, autoSave: false);
        request.Reverse(settlementId, userId, reason);
    }

    private async Task ValidateLinesAsync(Guid studentId, Guid assignmentId,
        IReadOnlyCollection<RefundLineSpec> lines, Guid? requestId)
    {
        if (lines.Count == 0 || lines.Any(x => x.Amount <= 0 || x.CalculatedAmount <= 0 || x.CalculatedAmount > x.Amount) ||
            lines.Any(x => !x.SourceAllocationId.HasValue) ||
            lines.GroupBy(x => x.SourceAllocationId).Any(g => g.Count() > 1))
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        var ids = lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = await dueFeeRepository.GetListAsync(x => ids.Contains(x.Id));
        if (dues.Count != ids.Count) throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        var reserved = await repository.GetActiveReservedAmountsAsync(ids, requestId);
        var dueRemaining = dues.ToDictionary(x => x.Id,
            x => x.RefundableBalance - reserved.GetValueOrDefault(x.Id));
        foreach (var line in lines)
        {
            var due = dues.Single(x => x.Id == line.DueFeeId);
            if (due.StudentId != studentId || due.StudentFeeStructureId != assignmentId)
                throw new BusinessException(FeeRefundErrorCodes.AssignmentMismatch);
            var available = Math.Min(dueRemaining[due.Id], line.SettledPaidAmount);
            if (line.Amount > available)
                throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance)
                    .WithData("DueFeeId", due.Id).WithData("Requested", line.Amount).WithData("Available", available);
            dueRemaining[due.Id] -= line.Amount;
        }
    }

    private FeeRefundLine ToEntity(Guid requestId, RefundLineSpec line) => new(GuidGenerator.Create(), requestId,
        line.DueFeeId, line.SourceAllocationId, line.SourceReceiptId, line.FeeHeadId,
        line.FeeHeadName, line.InstallmentLabel, line.SettledPaidAmount, line.Amount, line.CalculatedAmount);

    private static string GenerateVoucherNumber() => $"REF-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();

    public static RefundPaymentDetails? MapRefundPaymentDetails(string? method, Dictionary<string, object?>? details)
    {
        if (string.IsNullOrWhiteSpace(method)) return null;
        details ??= [];
        string Get(string key) => details.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
        string? GetOpt(string key) => details.TryGetValue(key, out var v) ? v?.ToString() : null;
        DateOnly Date(string key) => DateOnly.TryParse(Get(key), out var d) ? d : default;
        return method switch
        {
            "Cash" => new CashRefundDetails(Date("refundDate"), Get("receivedBy"), GetOpt("remarks")),
            "Cheque" => new ChequeRefundDetails(Date("refundDate"), Get("chequeNumber"), Get("payeeName"), Get("sourceBankAccount"), GetOpt("remarks")),
            "Demand Draft (DD)" => new DemandDraftRefundDetails(Date("issueDate"), Get("ddNumber"), Get("bankName"), Get("branchName"), Get("payeeName"), Get("sourceBankAccount"), GetOpt("remarks")),
            "Bank Transfer (NEFT/RTGS/IMPS)" => new BankTransferRefundDetails(Date("refundDate"), Get("sourceBankAccount"), Get("beneficiaryName"), Get("beneficiaryBankName"), Get("beneficiaryAccountNumber"), Get("ifscCode"), GetOpt("transactionId"), GetOpt("remarks")),
            "UPI" => new UpiRefundDetails(Date("refundDate"), Get("sourceBankAccount"), Get("beneficiaryName"), Get("upiId"), GetOpt("transactionId"), GetOpt("remarks")),
            _ => throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed),
        };
    }
}
