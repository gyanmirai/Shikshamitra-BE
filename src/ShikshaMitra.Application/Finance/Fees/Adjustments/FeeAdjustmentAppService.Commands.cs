using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Events;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public partial class FeeAdjustmentAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<FeeAdjustmentDetailDto> CreateAsync(Guid academicYearId, CreateFeeAdjustmentDto input)
    {
        await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);

        using var uow = unitOfWorkManager.Begin(isTransactional: true);

        var lines = input.SourceLines.Select(l => (l.DueFeeId, l.Amount)).ToList();
        var request = await feeAdjustmentManager.CreateAsync(
            academicYearId, input.StudentId, input.AdjustmentType, input.Reason, input.Remarks, lines);

        await feeAdjustmentRepository.InsertAsync(request, autoSave: false);

        var student = await studentRepository.GetAsync(input.StudentId);
        await financeAuditWriter.WriteAsync(
            academicYearId, FinanceAuditEntityTypes.FeeAdjustment, request.Id, request.VoucherNumber,
            FinanceAuditActions.Adjustment, $"Fee adjustment request {request.VoucherNumber} created.",
            BuildStudentContext(student, request.VoucherNumber), input.Reason);

        await uow.CompleteAsync();
        return await MapDetailAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<FeeAdjustmentDetailDto> UpdateAsync(Guid id, Guid academicYearId, UpdateFeeAdjustmentDto input)
    {
        var existing = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(existing.StudentId);
        existing.EnsurePending();
        await feeAdjustmentRepository.DeleteAsync(existing, autoSave: true);
        return await CreateAsync(academicYearId, input);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        request.EnsurePending();
        await feeAdjustmentRepository.DeleteAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<FeeAdjustmentApplyContextDto> GetApplyContextAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        request.EnsureCanApply();

        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == request.StudentId && df.OutstandingAmount > 0.001);
        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id)) : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var pendingFees = dueFees
            .OrderBy(df => df.Year).ThenBy(df => df.Month)
            .Select(df => new FeeAdjustmentPendingFeeDto
            {
                InstallmentId = df.Id, DueFeeId = df.Id,
                InstallmentLabel = FormatMonthLabel(df.Month, df.Year),
                MonthOrder = (int)df.Month,
                FeeHeadId = df.FeeHeadId,
                FeeHeadName = feeHeadMap.GetValueOrDefault(df.FeeHeadId, string.Empty),
                TotalFees = df.OriginalAmount, PaidAmount = df.PaidAmount,
                PendingAmount = df.OutstandingAmount, DueAmount = df.OutstandingAmount,
                MaxApplicableAmount = Math.Min(df.OutstandingAmount, request.RemainingAmount),
            }).ToList();

        return new FeeAdjustmentApplyContextDto
        {
            AdjustmentId = request.Id, StudentId = request.StudentId,
            ApprovedAmount = request.RequestedAmount, RemainingAmount = request.RemainingAmount,
            TotalPendingFees = pendingFees.Sum(p => p.PendingAmount), PendingFees = pendingFees,
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<ApplyFeeAdjustmentResultDto> ApplyAsync(
        Guid id, ApplyFeeAdjustmentDto input, string? idempotencyKey)
    {
        var approverId = currentUser.Id ?? throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply);

        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && request.HasIdempotencyKey(idempotencyKey))
            return await MapApplyResultAsync(request, approverId, 0);

        if (!input.AllowPartialApply)
        {
            var batchTotal = input.Lines.Sum(l => l.AppliedAmount);
            if (Math.Abs(batchTotal - request.RemainingAmount) > 0.01)
                throw new BusinessException(FeeAdjustmentErrorCodes.ApplyExceedsRemaining)
                    .WithData("Message", "Full apply required when partial apply is disabled.");
        }

        using var uow = unitOfWorkManager.Begin(isTransactional: true);
        var targetLines = input.Lines.Select(l => (l.DueFeeId, l.AppliedAmount)).ToList();
        var batchBefore = request.AppliedAmount;

        await feeAdjustmentManager.ApplyAsync(request, approverId, input.ApprovalNotes, targetLines);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.SetIdempotencyKey(idempotencyKey);

        await feeAdjustmentRepository.UpdateAsync(request, autoSave: true);

        var student = await studentRepository.GetAsync(request.StudentId);
        await financeAuditWriter.WriteAsync(
            request.AcademicYearId, FinanceAuditEntityTypes.FeeAdjustment, request.Id, request.VoucherNumber,
            FinanceAuditActions.Approve, $"Applied {request.AppliedAmount - batchBefore:F2} from adjustment {request.VoucherNumber}.",
            BuildStudentContext(student, request.VoucherNumber), input.ApprovalNotes, context: input.Lines);

        await uow.CompleteAsync();

        await eventBus.PublishAsync(new FeeAdjustmentAppliedEto
        {
            AdjustmentRequestId   = request.Id,
            VoucherNumber         = request.VoucherNumber,
            StudentId             = request.StudentId,
            AcademicYearId        = request.AcademicYearId,
            TotalAdjustedAmount   = request.AppliedAmount - batchBefore,
            TenantId              = CurrentTenant.Id,
            AppliedAt             = Clock.Now,
        });

        return await MapApplyResultAsync(request, approverId, request.AppliedAmount - batchBefore);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<RejectFeeAdjustmentResultDto> RejectAsync(Guid id, RejectFeeAdjustmentDto input)
    {
        var reviewerId = currentUser.Id ?? throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply);

        using var uow = unitOfWorkManager.Begin(isTransactional: true);
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        feeAdjustmentManager.Reject(request, reviewerId, input.Message);
        await feeAdjustmentRepository.UpdateAsync(request, autoSave: true);

        var student = await studentRepository.GetAsync(request.StudentId);
        await financeAuditWriter.WriteAsync(
            request.AcademicYearId, FinanceAuditEntityTypes.FeeAdjustment, request.Id, request.VoucherNumber,
            FinanceAuditActions.Reject, $"Adjustment {request.VoucherNumber} rejected.",
            BuildStudentContext(student, request.VoucherNumber), input.Message);

        await uow.CompleteAsync();
        return new RejectFeeAdjustmentResultDto
        {
            AdjustmentId = request.Id, Status = request.Status,
            StatusName = FormatStatus(request.Status),
            RejectedAt = request.ProcessedAt ?? Clock.Now,
            RejectionReason = request.RejectionReason ?? input.Message,
        };
    }
}
