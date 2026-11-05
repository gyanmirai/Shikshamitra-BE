using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class FeeAdjustmentManager(
    IFeeAdjustmentRepository feeAdjustmentRepository,
    IFeeRefundRepository feeRefundRepository,
    IDueFeeRepository dueFeeRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
    IRepository<FeeRecordMetadata, Guid> collectionRepository,
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    IRepository<Student, Guid> studentRepository,
    ICurrentTenant currentTenant) : DomainService
{
    public async Task<FeeAdjustmentRequest> CreateAsync(
        Guid academicYearId,
        Guid studentId,
        string reason,
        IReadOnlyList<(Guid DueFeeId, double Amount)> sourceLines)
    {
        if (sourceLines == null || sourceLines.Count == 0 ||
            sourceLines.Select(line => line.DueFeeId).Distinct().Count() != sourceLines.Count ||
            sourceLines.Any(line => !double.IsFinite(line.Amount) || line.Amount <= 0d ||
                line.Amount > 1_000_000_000_000d ||
                decimal.Round((decimal)line.Amount, 2) != (decimal)line.Amount))
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
        }

        var student = await studentRepository.FindAsync(studentId)
            ?? throw new BusinessException(FeeAdjustmentErrorCodes.StudentNotFound)
                .WithData("StudentId", studentId);

        if (!student.StandardId.HasValue || !student.DivisionId.HasValue)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.StudentNotFound)
                .WithData("StudentId", studentId)
                .WithData("Reason", "Student class/division not set");
        }

        var dueFeeIds = sourceLines.Select(l => l.DueFeeId).Distinct().ToList();
        var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));

        if (dueFees.Count != dueFeeIds.Count)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.DueFeeNotFound);
        }

        var dueFeeMap = dueFees.ToDictionary(df => df.Id);
        var settledPaid = await GetSettledPaidAmountsAsync(dueFeeIds, studentId, academicYearId);
        var sourceHeadIds = dueFees.Select(due => due.FeeHeadId).Distinct().ToList();
        var sourceHeads = await feeHeadRepository.GetListAsync(head => sourceHeadIds.Contains(head.Id));
        if (sourceHeads.Count != sourceHeadIds.Count ||
            sourceHeads.Any(head => head.AcademicYearId != academicYearId))
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
        var refundReservations = await feeRefundRepository.GetActiveReservedAmountsAsync(dueFeeIds);
        var unpaidSources = dueFees.All(due => due.OutstandingAmount > 0m);
        if (dueFees.Any(due => due.OutstandingAmount > 0m) && !unpaidSources)
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid)
                .WithData("Reason", "Choose either unpaid or paid-credit source installments, not both.");
        if (unpaidSources)
        {
            var yearHeadIds = (await feeHeadRepository.GetListAsync(head => head.AcademicYearId == academicYearId))
                .Select(head => head.Id).ToList();
            var destinations = await dueFeeRepository.GetListAsync(due =>
                due.StudentId == studentId && yearHeadIds.Contains(due.FeeHeadId) &&
                !dueFeeIds.Contains(due.Id) && due.OutstandingAmount > 0m);
            if (destinations.Count == 0)
                throw new BusinessException(FeeAdjustmentErrorCodes.NoUnpaidDestination)
                    .WithData("Reason", "A different pending or overdue fee line is required as a destination.");
        }

        foreach (var (dueFeeId, amount) in sourceLines)
        {
            var dueFee = dueFeeMap[dueFeeId];
            if (dueFee.StudentId != studentId)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid)
                    .WithData("DueFeeId", dueFeeId);
            }

            var available = unpaidSources ? dueFee.OutstandingAmount : Math.Max(0m,
                Math.Min(dueFee.AdjustableBalance, Math.Max(0m,
                    settledPaid.GetValueOrDefault(dueFeeId) - dueFee.RefundedAmount +
                    dueFee.NetAmount - dueFee.EffectiveObligationAmount -
                    dueFee.ReservedPaidCreditAmount - dueFee.TransferredPaidCreditAmount)) -
                refundReservations.GetValueOrDefault(dueFeeId));
			if ((decimal)amount > available + 0.001m)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.SourceExceedsAdjustableBalance)
                    .WithData("DueFeeId", dueFeeId)
                    .WithData("Requested", amount)
                    .WithData("Available", available);
            }
            if (!unpaidSources) dueFee.ReservePaidCredit((decimal)amount);
        }

        if (!unpaidSources) await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);

        var requestedAmount = sourceLines.Sum(l => l.Amount);
        var voucherNumber = await GenerateVoucherNumberAsync();
        var requestId = GuidGenerator.Create();

        var request = new FeeAdjustmentRequest(
            requestId,
            currentTenant.Id,
            studentId,
            academicYearId,
            student.AcademicBoardId ?? Guid.Empty,
            student.AcademicMediumId,
            student.StandardId.Value,
            student.DivisionId.Value,
            reason,
            voucherNumber,
            requestedAmount);

        var sourceEntities = sourceLines.Select(l =>
        {
            var df = dueFeeMap[l.DueFeeId];
            return new FeeAdjustmentSourceLine(
                GuidGenerator.Create(),
                requestId,
                l.DueFeeId,
                df.FeeHeadId,
                df.Month,
                df.Year,
                l.Amount);
        }).ToList();

        request.SetSourceLines(sourceEntities);
        if (unpaidSources) request.MarkUnpaidTransfer();
        else request.MarkPaidCreditReserved();

        return request;
    }

    public async Task ApplyAsync(
        FeeAdjustmentRequest request,
        Guid approverId,
        string? approvalNotes,
        IReadOnlyList<(Guid DueFeeId, double Amount)> targetLines)
    {
        if (request.Kind == FeeAdjustmentKind.UnpaidTransfer)
            await ApplyUnpaidTransferAsync(request, approverId, approvalNotes, targetLines);
        else
            await ApplyCoreAsync(request, approverId, approvalNotes, targetLines);
    }

    private async Task ApplyUnpaidTransferAsync(
        FeeAdjustmentRequest request,
        Guid approverId,
        string? approvalNotes,
        IReadOnlyList<(Guid DueFeeId, double Amount)> targetLines)
    {
        request.EnsureCanApply();
        request.EnsureApproverNotCreator(approverId);
        if (targetLines == null || targetLines.Count == 0 ||
            targetLines.Select(line => line.DueFeeId).Distinct().Count() != targetLines.Count ||
            targetLines.Any(line => !double.IsFinite(line.Amount) || line.Amount <= 0d ||
                line.Amount > 1_000_000_000_000d ||
                decimal.Round((decimal)line.Amount, 2) != (decimal)line.Amount))
            throw new BusinessException(FeeAdjustmentErrorCodes.NoAllocationLines);

        var batchTotal = targetLines.Sum(line => (decimal)line.Amount);
        if (batchTotal > (decimal)request.RemainingAmount)
            throw new BusinessException(FeeAdjustmentErrorCodes.ApplyExceedsRemaining);
        var sourceIds = request.SourceLines.Select(line => line.DueFeeId).ToHashSet();
        var ids = sourceIds.Concat(targetLines.Select(line => line.DueFeeId)).Distinct().ToList();
        if (ids.Count != sourceIds.Count + targetLines.Count)
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
        var dues = await dueFeeRepository.GetListAsync(due => ids.Contains(due.Id));
        if (dues.Count != ids.Count || dues.Any(due => due.StudentId != request.StudentId))
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
        var headIds = dues.Select(due => due.FeeHeadId).Distinct().ToList();
        var heads = await feeHeadRepository.GetListAsync(head => headIds.Contains(head.Id));
        if (heads.Count != headIds.Count || heads.Any(head => head.AcademicYearId != request.AcademicYearId))
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
        var byId = dues.ToDictionary(due => due.Id);
        if (targetLines.Any(line => byId[line.DueFeeId].OutstandingAmount <= 0m))
            throw new BusinessException(FeeAdjustmentErrorCodes.TargetExceedsOutstanding);

        var priorApplied = (decimal)request.AppliedAmount;
        var remainingToMove = batchTotal;
        foreach (var source in request.SourceLines.OrderBy(line => line.Id))
        {
            var alreadyMoved = Math.Min(priorApplied, (decimal)source.Amount);
            priorApplied -= alreadyMoved;
            var move = Math.Min((decimal)source.Amount - alreadyMoved, remainingToMove);
            if (move > 0m)
            {
                byId[source.DueFeeId].TransferUnpaidOut(move);
                remainingToMove -= move;
            }
        }
        if (remainingToMove != 0m)
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceExceedsAdjustableBalance);
        foreach (var target in targetLines)
            byId[target.DueFeeId].TransferUnpaidIn((decimal)target.Amount);
        request.RecordApplyBatch(approverId, approvalNotes,
            targetLines.Select(line =>
            {
                var due = byId[line.DueFeeId];
                return (line.DueFeeId, due.FeeHeadId, due.Month, due.Year, line.Amount);
            }));
        await dueFeeRepository.UpdateManyAsync(dues, autoSave: false);
    }

    private async Task ApplyCoreAsync(
        FeeAdjustmentRequest request,
        Guid approverId,
        string? approvalNotes,
        IReadOnlyList<(Guid DueFeeId, double Amount)> targetLines)
    {
        request.EnsureCanApply();
        if (!request.HasPaidCreditReservation)
            throw new BusinessException(FeeAdjustmentErrorCodes.PaidCreditTransferUnavailable);

        if (targetLines == null || targetLines.Count == 0)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.NoAllocationLines);
        }

        var dueFeeIds = targetLines.Select(l => l.DueFeeId).Distinct().ToList();
        var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));

        if (dueFees.Count != dueFeeIds.Count)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.DueFeeNotFound);
        }

        var dueFeeMap = dueFees.ToDictionary(df => df.Id);
        var targetHeadIds = dueFees.Select(due => due.FeeHeadId).Distinct().ToList();
        var targetHeads = await feeHeadRepository.GetListAsync(head => targetHeadIds.Contains(head.Id));
        if (targetHeads.Count != targetHeadIds.Count ||
            targetHeads.Any(head => head.AcademicYearId != request.AcademicYearId))
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
        var sourceDueIds = request.SourceLines.Select(line => line.DueFeeId).Distinct().ToList();
        if (targetLines.Any(line => sourceDueIds.Contains(line.DueFeeId)))
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);

        foreach (var (dueFeeId, amount) in targetLines)
        {
            if (amount <= 0)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.NoAllocationLines);
            }

            var dueFee = dueFeeMap[dueFeeId];
            if (dueFee.StudentId != request.StudentId)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid)
                    .WithData("DueFeeId", dueFeeId);
            }

			if ((decimal)amount > dueFee.OutstandingAmount + 0.001m)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.TargetExceedsOutstanding)
                    .WithData("DueFeeId", dueFeeId)
                    .WithData("Outstanding", dueFee.OutstandingAmount)
                    .WithData("Requested", amount);
            }
        }

        var allocations = targetLines.Select(l =>
        {
            var df = dueFeeMap[l.DueFeeId];
            return (l.DueFeeId, df.FeeHeadId, df.Month, df.Year, l.Amount);
        }).ToList();

        var batchAmount = (decimal)targetLines.Sum(line => line.Amount);
        var sourceDues = (await dueFeeRepository.GetListAsync(df => sourceDueIds.Contains(df.Id)))
            .ToDictionary(due => due.Id);
        var settledPaid = await GetSettledPaidAmountsAsync(sourceDueIds, request.StudentId, request.AcademicYearId);
        if (sourceDues.Values.Any(due => settledPaid.GetValueOrDefault(due.Id) - due.RefundedAmount +
            due.NetAmount - due.EffectiveObligationAmount < due.ReservedPaidCreditAmount + due.TransferredPaidCreditAmount))
            throw new BusinessException(FeeAdjustmentErrorCodes.SourceExceedsAdjustableBalance);
        var priorApplied = (decimal)request.AppliedAmount;
        var remainingToConsume = batchAmount;
        foreach (var sourceLine in request.SourceLines.OrderBy(line => line.Id))
        {
            var sourceAmount = (decimal)sourceLine.Amount;
            var alreadyConsumed = Math.Min(priorApplied, sourceAmount);
            priorApplied -= alreadyConsumed;
            var consume = Math.Min(sourceAmount - alreadyConsumed, remainingToConsume);
            if (consume > 0m)
            {
                sourceDues[sourceLine.DueFeeId].ConsumeReservedPaidCredit(consume);
                remainingToConsume -= consume;
            }
        }
        if (remainingToConsume != 0m)
            throw new BusinessException(FeeAdjustmentErrorCodes.ApplyExceedsRemaining);

        request.RecordApplyBatch(approverId, approvalNotes, allocations);

        foreach (var (dueFeeId, amount) in targetLines)
        {
			dueFeeMap[dueFeeId].ApplyAdjustmentCredit((decimal)amount);
        }

        await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);
        await dueFeeRepository.UpdateManyAsync(sourceDues.Values, autoSave: false);
    }

    public void Reject(FeeAdjustmentRequest request, Guid reviewerId, string message) =>
        request.Reject(reviewerId, message);

    public async Task ReleasePendingReservationAsync(FeeAdjustmentRequest request)
    {
        request.EnsurePending();
        if (!request.HasPaidCreditReservation) return;
        var byDue = request.SourceLines.GroupBy(line => line.DueFeeId)
            .ToDictionary(group => group.Key, group => (decimal)group.Sum(line => line.Amount));
        var dueIds = byDue.Keys.ToList();
        var dues = await dueFeeRepository.GetListAsync(due => dueIds.Contains(due.Id));
        foreach (var due in dues)
            due.ReleaseReservedPaidCredit(byDue[due.Id]);
        await dueFeeRepository.UpdateManyAsync(dues, autoSave: false);
    }

    public async Task<Dictionary<Guid, decimal>> GetSettledPaidAmountsAsync(
        IReadOnlyCollection<Guid> dueIds, Guid studentId, Guid academicYearId)
    {
        if (dueIds.Count == 0) return [];
        var allocations = await allocationRepository.GetListAsync(line => dueIds.Contains(line.DueFeeId));
        var collectionIds = allocations.Select(line => line.FeeRecordMetadataId).Distinct().ToList();
        var collections = await collectionRepository.GetListAsync(collection =>
            collectionIds.Contains(collection.Id) && collection.TenantId == currentTenant.Id &&
            collection.StudentId == studentId && collection.AcademicYearId == academicYearId &&
            (collection.Status == FeeCollectionStatus.Paid ||
                collection.Status == FeeCollectionStatus.Partial));
        var eligibleIds = collections.Select(collection => collection.Id).ToList();
        var receipts = await receiptRepository.GetListAsync(receipt =>
            eligibleIds.Contains(receipt.FeeCollectionTransactionId) &&
            receipt.TenantId == currentTenant.Id && receipt.StudentId == studentId &&
            receipt.AcademicYearId == academicYearId);
        var settledIds = receipts.Where(receipt =>
                RefundReceiptEvidencePolicy.IsSettled(receipt) &&
                RefundReceiptEvidencePolicy.CoversPrincipal(receipt))
            .Select(receipt => receipt.FeeCollectionTransactionId).Distinct().ToHashSet();
        return allocations.Where(line => settledIds.Contains(line.FeeRecordMetadataId))
            .GroupBy(line => line.DueFeeId)
            .ToDictionary(group => group.Key, group => group.Sum(line => (decimal)line.Amount));
    }

    private async Task<string> GenerateVoucherNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await feeAdjustmentRepository.IncrementAndGetVoucherSequenceAsync(year);
        return $"ADJ-{year}-{sequence:D6}";
    }
}
