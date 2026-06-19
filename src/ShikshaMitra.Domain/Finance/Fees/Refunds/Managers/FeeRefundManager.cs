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

/// <summary>
/// Domain service that owns the creation and approval orchestration of <see cref="FeeRefundRequest"/>.
/// Application services must delegate domain logic here — never mutate entities directly from AppService.
/// </summary>
public class FeeRefundManager(
    IFeeRefundRepository feeRefundRepository,
    IDueFeeRepository dueFeeRepository,
	ICurrentTenant currentTenant
) : DomainService
{
    /// <summary>
    /// Creates and persists a new <see cref="FeeRefundRequest"/> in <see cref="FeeRefundStatus.Pending"/>
    /// after validating each line's refundable balance.
    /// </summary>
    public async Task<FeeRefundRequest> CreateAsync(
        Guid studentId,
        Guid academicYearId,
        List<(Guid DueFeeId, double Amount)> lines,
        string? refundMethod,
        RefundPaymentDetails? refundPaymentDetails,
        string? reason)
    {
        if (lines == null || lines.Count == 0)
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);

        // Validate each DueFee line and check refundable balance
        var dueFeeIds = lines.Select(l => l.DueFeeId).ToList();
        var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));

        if (dueFees.Count != dueFeeIds.Count)
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound)
                .WithData("Missing", dueFeeIds.Except(dueFees.Select(df => df.Id)).ToList());

        var dueFeeMap = dueFees.ToDictionary(df => df.Id);
        foreach (var (dueFeeId, amount) in lines)
        {
            var dueFee = dueFeeMap[dueFeeId];
            if (amount > dueFee.RefundableBalance)
                throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance)
                    .WithData("DueFeeId", dueFeeId)
                    .WithData("Requested", amount)
                    .WithData("Available", dueFee.RefundableBalance);
        }

        var voucherNumber = await GenerateVoucherNumberAsync();
        var totalAmount = lines.Sum(l => l.Amount);

        var request = new FeeRefundRequest(
            id: GuidGenerator.Create(),
            tenantId: currentTenant.Id,
            studentId: studentId,
            academicYearId: academicYearId,
            voucherNumber: voucherNumber,
            totalAmount: totalAmount,
            refundMethod: refundMethod,
            refundPaymentDetails: refundPaymentDetails,
            reason: reason);

        foreach (var (dueFeeId, amount) in lines)
        {
            request.AddLine(new FeeRefundLine(
                id: GuidGenerator.Create(),
                feeRefundRequestId: request.Id,
                dueFeeId: dueFeeId,
                amount: amount));
        }

        return await feeRefundRepository.InsertAsync(request, autoSave: false);
    }

    /// <summary>
    /// Validates and applies an in-place update to a <see cref="FeeRefundStatus.Pending"/> refund request.
    /// Replaces all lines, recomputes the total, and updates payment details and reason.
    /// </summary>
    /// <remarks>
    /// Refundable-balance validation re-excludes amounts already held by this same request,
    /// so the requester can freely adjust amounts without being blocked by their own prior reservation.
    /// </remarks>
    public async Task UpdateAsync(
        FeeRefundRequest request,
        List<(Guid DueFeeId, double Amount)> lines,
        string? refundMethod,
        RefundPaymentDetails? refundPaymentDetails,
        string? reason)
    {
        if (request.Status != FeeRefundStatus.Pending)
            throw new BusinessException(FeeRefundErrorCodes.CannotModifyNonPendingRefund)
                .WithData("VoucherNumber", request.VoucherNumber)
                .WithData("CurrentStatus", request.Status);

        if (lines == null || lines.Count == 0)
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);

        var dueFeeIds = lines.Select(l => l.DueFeeId).ToList();
        var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));

        if (dueFees.Count != dueFeeIds.Count)
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound)
                .WithData("Missing", dueFeeIds.Except(dueFees.Select(df => df.Id)).ToList());

        // Build a map of amounts already held by this request so we don't double-count them
        // when re-validating the refundable balance.
        var existingLineAmounts = request.Lines
            .ToDictionary(l => l.DueFeeId, l => l.Amount);

        var dueFeeMap = dueFees.ToDictionary(df => df.Id);
        foreach (var (dueFeeId, amount) in lines)
        {
            var dueFee = dueFeeMap[dueFeeId];

            // Available = stored RefundableBalance + amount already reserved by this request
            var alreadyReserved = existingLineAmounts.TryGetValue(dueFeeId, out var prev) ? prev : 0d;
            var available = dueFee.RefundableBalance + alreadyReserved;

            if (amount > available)
                throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance)
                    .WithData("DueFeeId", dueFeeId)
                    .WithData("Requested", amount)
                    .WithData("Available", available);
        }

        var totalAmount = lines.Sum(l => l.Amount);

        var newLines = lines
            .Select(l => new FeeRefundLine(
                id: request.Lines.FirstOrDefault(existing => existing.DueFeeId == l.DueFeeId)?.Id ?? GuidGenerator.Create(),
                feeRefundRequestId: request.Id,
                dueFeeId: l.DueFeeId,
                amount: l.Amount))
            .ToList();

        request.ReplaceLines(newLines, totalAmount);
        request.UpdatePaymentDetails(refundMethod, refundPaymentDetails);
        request.UpdateReason(reason);
    }

	/// <summary>
	/// Approves a refund request and applies <see cref="DueFee.ApplyRefund"/>
	/// to each referenced DueFee row inside the caller's transaction.
	/// The caller must wrap this in a unit-of-work and call <see cref="IFeeRefundRepository.UpdateAsync"/>.
	/// </summary>
	public async Task ApproveAsync(FeeRefundRequest request, Guid approverUserId, string? notes = null)
    {
		if (request.Status == FeeRefundStatus.Approved)
            throw new BusinessException(FeeRefundErrorCodes.RefundAlreadyProcessed)
                .WithData("VoucherNumber", request.VoucherNumber);

        // Load and mutate DueFee rows
        var dueFeeIds = request.Lines.Select(l => l.DueFeeId).ToList();
        var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));
        var dueFeeMap = dueFees.ToDictionary(df => df.Id);

        foreach (var line in request.Lines)
        {
            if (!dueFeeMap.TryGetValue(line.DueFeeId, out var dueFee))
                throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound)
                    .WithData("DueFeeId", line.DueFeeId);

            dueFee.ApplyRefund(line.Amount);
        }

        await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);

        request.Approve(approverUserId, notes);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GenerateVoucherNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await feeRefundRepository.GetCountAsync();
        return $"REF-{year}-{count + 1:D6}";
    }

    // ── Payment details factory ───────────────────────────────────────────────

    /// <summary>
    /// Maps the raw details dictionary from the API request into the appropriate
    /// strongly-typed <see cref="RefundPaymentDetails"/> value object.
    /// Returns <c>null</c> for Cash — the only required field (received-by) is
    /// captured separately; pass it explicitly via the <paramref name="details"/> dict.
    /// </summary>
    /// <param name="refundMethod">The string method key sent by the frontend (e.g., "Cash", "Cheque").</param>
    /// <param name="details">Flat key/value dictionary matching the frontend <c>paymentDetails</c> object.</param>
    public static RefundPaymentDetails? MapRefundPaymentDetails(
        string? refundMethod,
        Dictionary<string, object?>? details)
    {
        if (string.IsNullOrWhiteSpace(refundMethod))
            return null;

        details ??= [];

        string Get(string key) =>
            details.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

        string? GetOpt(string key) =>
            details.TryGetValue(key, out var v) ? v?.ToString() : null;

        DateOnly ParseDate(string key) =>
            DateOnly.TryParse(Get(key), out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        return refundMethod switch
        {
            "Cash" => new CashRefundDetails(
                refundDate: ParseDate("refundDate"),
                receivedBy: Get("receivedBy"),
                remarks:    GetOpt("remarks")),

            "Cheque" => new ChequeRefundDetails(
                chequeIssueDate:   ParseDate("refundDate"),
                chequeNumber:      Get("chequeNumber"),
                payeeName:         Get("payeeName"),
                sourceBankAccount: Get("sourceBankAccount"),
                remarks:           GetOpt("remarks")),

            "Demand Draft (DD)" => new DemandDraftRefundDetails(
                issueDate:         ParseDate("issueDate"),
                ddNumber:          Get("ddNumber"),
                bankName:          Get("bankName"),
                branchName:        Get("branchName"),
                payeeName:         Get("payeeName"),
                sourceBankAccount: Get("sourceBankAccount"),
                remarks:           GetOpt("remarks")),

            "Bank Transfer (NEFT/RTGS/IMPS)" => new BankTransferRefundDetails(
                transferDate:             ParseDate("refundDate"),
                sourceBankAccount:        Get("sourceBankAccount"),
                beneficiaryName:          Get("beneficiaryName"),
                beneficiaryBankName:      Get("beneficiaryBankName"),
                beneficiaryAccountNumber: Get("beneficiaryAccountNumber"),
                ifscCode:                 Get("ifscCode"),
                transactionId:            GetOpt("transactionId"),
                remarks:                  GetOpt("remarks")),

            "UPI" => new UpiRefundDetails(
                refundDate:        ParseDate("refundDate"),
                sourceBankAccount: Get("sourceBankAccount"),
                beneficiaryName:   Get("beneficiaryName"),
                upiId:             Get("upiId"),
                transactionId:     GetOpt("transactionId"),
                remarks:           GetOpt("remarks")),

            _ => null,
        };
    }
}
