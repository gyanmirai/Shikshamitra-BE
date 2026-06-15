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
}
