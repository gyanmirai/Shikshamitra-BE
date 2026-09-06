using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class FeeAdjustmentManager(
    IFeeAdjustmentRepository feeAdjustmentRepository,
    IDueFeeRepository dueFeeRepository,
    IRepository<Student, Guid> studentRepository,
    ICurrentTenant currentTenant) : DomainService
{
    public async Task<FeeAdjustmentRequest> CreateAsync(
        Guid academicYearId,
        Guid studentId,
        string reason,
        IReadOnlyList<(Guid DueFeeId, double Amount)> sourceLines)
    {
        if (sourceLines == null || sourceLines.Count == 0)
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
        var pendingSource = await feeAdjustmentRepository.GetPendingSourceAmountsByDueFeeAsync(studentId);

        foreach (var (dueFeeId, amount) in sourceLines)
        {
            if (amount <= 0)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid)
                    .WithData("DueFeeId", dueFeeId);
            }

            var dueFee = dueFeeMap[dueFeeId];
            if (dueFee.StudentId != studentId)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid)
                    .WithData("DueFeeId", dueFeeId);
            }

            pendingSource.TryGetValue(dueFeeId, out var reserved);
			var available = dueFee.AdjustableBalance - (decimal)reserved;
			if ((decimal)amount > available + 0.001m)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.SourceExceedsAdjustableBalance)
                    .WithData("DueFeeId", dueFeeId)
                    .WithData("Requested", amount)
                    .WithData("Available", available);
            }
        }

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

        return request;
    }

    public async Task ApplyAsync(
        FeeAdjustmentRequest request,
        Guid approverId,
        string? approvalNotes,
        IReadOnlyList<(Guid DueFeeId, double Amount)> targetLines)
    {
        request.EnsureCanApply();

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

        request.RecordApplyBatch(approverId, approvalNotes, allocations);

        foreach (var (dueFeeId, amount) in targetLines)
        {
			dueFeeMap[dueFeeId].ApplyAdjustmentCredit((decimal)amount);
        }

        await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);
    }

    public void Reject(FeeAdjustmentRequest request, Guid reviewerId, string message) =>
        request.Reject(reviewerId, message);

    private async Task<string> GenerateVoucherNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await feeAdjustmentRepository.IncrementAndGetVoucherSequenceAsync(year);
        return $"ADJ-{year}-{sequence:D6}";
    }
}
