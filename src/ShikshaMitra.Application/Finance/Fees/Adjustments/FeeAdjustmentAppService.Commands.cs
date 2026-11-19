using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Events;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public partial class FeeAdjustmentAppService
{
    private IUnitOfWork BeginBalanceUnitOfWork() => unitOfWorkManager.Begin(
        new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable },
        requiresNew: true);

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeAdjustmentDetailDto> CreateAsync(Guid academicYearId, CreateFeeAdjustmentDto input)
    {
        using var uow = BeginBalanceUnitOfWork();
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(input.StudentId, academicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add);
        var payload = new { academicYearId, input.StudentId, input.Reason,
            Lines = input.SourceLines.OrderBy(line => line.DueFeeId).Select(line =>
                new { line.DueFeeId, line.Amount }).ToList() };
        var replayId = await feeAdjustmentCommandStore.ReplayAsync("create", input.IdempotencyKey, payload);
        if (replayId.HasValue)
        {
            var saved = await feeAdjustmentRepository.GetWithDetailsAsync(replayId.Value);
            await uow.CompleteAsync();
            return await MapDetailAsync(saved);
        }

        var lines = input.SourceLines.Select(l => (l.DueFeeId, l.Amount)).ToList();
        var request = await feeAdjustmentManager.CreateAsync(
            academicYearId, input.StudentId, input.Reason, lines);

        await feeAdjustmentRepository.InsertAsync(request, autoSave: false);
        await feeAdjustmentCommandStore.RecordAsync("create", input.IdempotencyKey, payload,
            request.Id, request.StudentId, academicYearId);

        var student = await studentRepository.GetAsync(input.StudentId);
        await financeAuditWriter.WriteAsync(
            academicYearId, FinanceAuditEntityTypes.FeeAdjustment, request.Id, request.VoucherNumber,
            FinanceAuditActions.Adjustment, $"Fee adjustment request {request.VoucherNumber} created.",
            BuildStudentContext(student, request.VoucherNumber), input.Reason);

        await uow.CompleteAsync();
        return await MapDetailAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeAdjustmentDetailDto> UpdateAsync(Guid id, Guid academicYearId, UpdateFeeAdjustmentDto input)
    {
        using var uow = BeginBalanceUnitOfWork();
        var payload = new { id, academicYearId, input.StudentId, input.Reason,
            Lines = input.SourceLines.OrderBy(line => line.DueFeeId).Select(line =>
                new { line.DueFeeId, line.Amount }).ToList() };
        var replayId = await feeAdjustmentCommandStore.ReplayAsync($"update:{id}", input.IdempotencyKey, payload);
        if (replayId.HasValue)
        {
            var saved = await feeAdjustmentRepository.GetWithDetailsAsync(replayId.Value);
            await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(saved.StudentId, saved.AcademicYearId,
                ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add);
            await uow.CompleteAsync();
            return await MapDetailAsync(saved);
        }
        var existing = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(existing.StudentId, existing.AcademicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add);
        await EnsureOwnerOrAdministratorAsync(existing);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(input.StudentId, academicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add);
        existing.EnsurePending();
        await feeAdjustmentManager.ReleasePendingReservationAsync(existing);
        await feeAdjustmentRepository.DeleteAsync(existing, autoSave: false);
        var lines = input.SourceLines.Select(line => (line.DueFeeId, line.Amount)).ToList();
        var replacement = await feeAdjustmentManager.CreateAsync(
            academicYearId, input.StudentId, input.Reason, lines);
        await feeAdjustmentRepository.InsertAsync(replacement, autoSave: false);
        await feeAdjustmentCommandStore.RecordAsync($"update:{id}", input.IdempotencyKey, payload,
            replacement.Id, replacement.StudentId, academicYearId);
        var student = await studentRepository.GetAsync(input.StudentId);
        await financeAuditWriter.WriteAsync(
            academicYearId, FinanceAuditEntityTypes.FeeAdjustment, replacement.Id, replacement.VoucherNumber,
            FinanceAuditActions.Adjustment, $"Fee adjustment request {existing.VoucherNumber} replaced by {replacement.VoucherNumber}.",
            BuildStudentContext(student, replacement.VoucherNumber), input.Reason);
        await uow.CompleteAsync();
        return await MapDetailAsync(replacement);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete)]
    [UnitOfWork(IsDisabled = true)]
    public async Task DeleteAsync(Guid id)
    {
        using var uow = BeginBalanceUnitOfWork();
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(request.StudentId, request.AcademicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete);
        await EnsureOwnerOrAdministratorAsync(request);
        request.EnsurePending();
        await feeAdjustmentManager.ReleasePendingReservationAsync(request);
        await feeAdjustmentRepository.DeleteAsync(request);
        await uow.CompleteAsync();
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve)]
    public async Task<FeeAdjustmentApplyContextDto> GetApplyContextAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(request.StudentId, request.AcademicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve);
        await EnsureCurrentReviewerCanDecideAsync(request,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve);
        request.EnsureCanApply();
        if (request.Kind == FeeAdjustmentKind.PaidCredit && !request.HasPaidCreditReservation)
            throw new BusinessException(FeeAdjustmentErrorCodes.PaidCreditTransferUnavailable);
        var feeHeads = await feeHeadRepository.GetListAsync(head => head.AcademicYearId == request.AcademicYearId);
        var headIds = feeHeads.Select(head => head.Id).ToList();
        var headNames = feeHeads.ToDictionary(head => head.Id, head => head.Name);
        var sourceIds = request.SourceLines.Select(line => line.DueFeeId).ToList();
        var unpaidTransfer = request.Kind == FeeAdjustmentKind.UnpaidTransfer;
        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == request.StudentId && headIds.Contains(df.FeeHeadId) &&
                df.OutstandingAmount > 0.001m &&
                (!unpaidTransfer || !sourceIds.Contains(df.Id)));
        var pendingFees = dueFees.OrderBy(df => df.Year).ThenBy(df => df.Month)
            .Select(df => new FeeAdjustmentPendingFeeDto
            {
                InstallmentId = df.Id, DueFeeId = df.Id,
                InstallmentLabel = FormatMonthLabel(df.Month, df.Year),
                MonthOrder = (int)df.Month, FeeHeadId = df.FeeHeadId,
                FeeHeadName = headNames.GetValueOrDefault(df.FeeHeadId, string.Empty),
                TotalFees = (double)df.EffectiveObligationAmount, PaidAmount = (double)df.PaidAmount,
                PendingAmount = (double)df.OutstandingAmount, DueAmount = (double)df.OutstandingAmount,
                MaxApplicableAmount = unpaidTransfer
                    ? request.RemainingAmount
                    : Math.Min((double)df.OutstandingAmount, request.RemainingAmount),
            }).ToList();
        return new FeeAdjustmentApplyContextDto
        {
            Kind = request.Kind,
            AdjustmentId = request.Id, StudentId = request.StudentId,
            ApprovedAmount = request.RequestedAmount, RemainingAmount = request.RemainingAmount,
            TotalPendingFees = pendingFees.Sum(fee => fee.PendingAmount), PendingFees = pendingFees,
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<ApplyFeeAdjustmentResultDto> ApplyAsync(
        Guid id, ApplyFeeAdjustmentDto input, string? idempotencyKey)
    {
        using var uow = BeginBalanceUnitOfWork();
        var approverId = currentUser.Id ?? throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply);

        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(request.StudentId, request.AcademicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve);
        await EnsureCurrentReviewerCanDecideAsync(request,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve);

        request.EnsureApproverNotCreator(approverId);
        var key = idempotencyKey ?? string.Empty;
        var payload = new { id, input.AllowPartialApply, input.ApprovalNotes,
            Lines = input.Lines.OrderBy(line => line.DueFeeId).Select(line =>
                new { line.DueFeeId, line.AppliedAmount }).ToList() };
        var replayId = await feeAdjustmentCommandStore.ReplayAsync($"apply:{id}", key, payload);
        if (replayId.HasValue)
        {
            await uow.CompleteAsync();
            return await MapApplyResultAsync(request, approverId, 0);
        }

        if (!input.AllowPartialApply)
        {
            var batchTotal = input.Lines.Sum(l => l.AppliedAmount);
            if (Math.Abs(batchTotal - request.RemainingAmount) > 0.01)
                throw new BusinessException(FeeAdjustmentErrorCodes.ApplyExceedsRemaining)
                    .WithData("Message", "Full apply required when partial apply is disabled.");
        }

        var targetLines = input.Lines.Select(l => (l.DueFeeId, l.AppliedAmount)).ToList();
        var batchBefore = request.AppliedAmount;

        await feeAdjustmentManager.ApplyAsync(request, approverId, input.ApprovalNotes, targetLines);

        await feeAdjustmentRepository.UpdateAsync(request, autoSave: true);
        await feeAdjustmentCommandStore.RecordAsync($"apply:{id}", key, payload,
            request.Id, request.StudentId, request.AcademicYearId);

        var student = await studentRepository.GetAsync(request.StudentId);
        await financeAuditWriter.WriteAsync(
            request.AcademicYearId, FinanceAuditEntityTypes.FeeAdjustment, request.Id, request.VoucherNumber,
            FinanceAuditActions.Approve, $"Applied {request.AppliedAmount - batchBefore:F2} from adjustment {request.VoucherNumber}.",
            BuildStudentContext(student, request.VoucherNumber), input.ApprovalNotes, context: input.Lines);

        await uow.CompleteAsync();

        if (request.Kind == FeeAdjustmentKind.PaidCredit)
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

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<RejectFeeAdjustmentResultDto> RejectAsync(Guid id, RejectFeeAdjustmentDto input)
    {
        var reviewerId = currentUser.Id ?? throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply);

        using var uow = BeginBalanceUnitOfWork();
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(request.StudentId, request.AcademicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject);
        await EnsureCurrentReviewerCanDecideAsync(request,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject);
        await feeAdjustmentManager.ReleasePendingReservationAsync(request);
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
