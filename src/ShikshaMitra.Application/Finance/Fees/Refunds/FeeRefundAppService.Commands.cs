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
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
    public async Task<FeeRefundRequestDto> CreateAsync(CreateFeeRefundRequestDto input)
    {
        var prepared = await PrepareAsync(input);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });
        var request = await feeRefundManager.CreateDraftAsync(input.StudentId, input.AcademicYearId,
            prepared.AssignmentId, prepared.PolicyId, prepared.PolicyVersionId, prepared.Receipt.Id,
            prepared.Receipt.ReceiptNumber, prepared.Lines, input.RefundMethod,
            FeeRefundManager.MapRefundPaymentDetails(input.RefundMethod, input.PaymentDetails), input.Reason,
            prepared.PolicyJson, prepared.CalculationJson, prepared.Hash);
        await WriteAuditAsync(request, FinanceAuditActions.Create,
            $"Refund request {request.VoucherNumber} created as a draft.", input.Reason);
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Edit)]
    public async Task<FeeRefundRequestDto> UpdateAsync(Guid id, UpdateFeeRefundRequestDto input)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        var create = new CreateFeeRefundRequestDto
        {
            StudentId = request.StudentId, AcademicYearId = request.AcademicYearId,
            OriginalReceiptId = request.OriginalReceiptId ?? input.OriginalReceiptId,
            Lines = input.Lines, RefundMethod = input.RefundMethod,
            PaymentDetails = input.PaymentDetails, Reason = input.Reason
        };
        var prepared = await PrepareAsync(create, request.Id);
        if (prepared.AssignmentId != request.StudentFeeStructureId || prepared.PolicyVersionId != request.RefundPolicyVersionId)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });
        await feeRefundManager.UpdateDraftAsync(request, prepared.Lines, input.RefundMethod,
            FeeRefundManager.MapRefundPaymentDetails(input.RefundMethod, input.PaymentDetails), input.Reason,
            prepared.CalculationJson, prepared.Hash);
        await feeRefundRepository.UpdateAsync(request, autoSave: false);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Refund request {request.VoucherNumber} draft updated.", input.Reason);
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var request = await RequireAsync(id);
        if (request.Status != FeeRefundStatus.Draft)
            throw new BusinessException(FeeRefundErrorCodes.CannotModifyNonPendingRefund);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        await WriteAuditAsync(request, FinanceAuditActions.Delete,
            $"Refund request {request.VoucherNumber} deleted.");
        await feeRefundRepository.DeleteAsync(request, autoSave: true);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Submit)]
    public async Task<FeeRefundRequestDto> SubmitAsync(Guid id)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        request.Submit(currentUser.GetId());
        await feeRefundRepository.UpdateAsync(request, autoSave: true);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Refund request {request.VoucherNumber} submitted for review.");
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.StartReview)]
    public async Task<FeeRefundRequestDto> StartReviewAsync(Guid id)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        request.MarkUnderReview(currentUser.GetId());
        await feeRefundRepository.UpdateAsync(request, autoSave: true);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Review started for refund request {request.VoucherNumber}.");
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
    public async Task<FeeRefundRequestDto> ApproveAsync(Guid id, FeeRefundActionDto input)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        await EnsureMakerCheckerAsync(request);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        await feeRefundManager.ApproveAsync(request, currentUser.GetId(), input.Notes);
        await feeRefundRepository.UpdateAsync(request, autoSave: false);
        await WriteAuditAsync(request, FinanceAuditActions.Approve,
            $"Refund request {request.VoucherNumber} approved and its payment allocations reserved.", input.Notes);
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject)]
    public async Task<FeeRefundRequestDto> RejectAsync(Guid id, RejectFeeRefundDto input)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        await EnsureMakerCheckerAsync(request);
        request.Reject(currentUser.GetId(), input.Notes);
        await feeRefundRepository.UpdateAsync(request, autoSave: true);
        await WriteAuditAsync(request, FinanceAuditActions.Reject,
            $"Refund request {request.VoucherNumber} rejected.", input.Notes);
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Cancel)]
    public async Task<FeeRefundRequestDto> CancelAsync(Guid id, FeeRefundActionDto input)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        request.Cancel(currentUser.GetId(), input.Notes);
        await feeRefundRepository.UpdateAsync(request, autoSave: true);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Refund request {request.VoucherNumber} cancelled and active reservations released.", input.Notes);
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<FeeRefundRequestDto> ProcessAsync(Guid id, ProcessFeeRefundDto input)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        await AuthorizationService.CheckAsync(request.Status == FeeRefundStatus.Failed
            ? ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.RetryProcessing
            : ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process);
        EnsureSettlementMethodAllowed(request, input.Method);
        var settlementDetails = input.PaymentDetails ?? MapRefundPaymentDetailsToDict(request.RefundPaymentDetails);
        var wasAlreadyProcessed = request.Status == FeeRefundStatus.Processed;
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        await feeRefundManager.ProcessAsync(request, currentUser.GetId(), input.IdempotencyKey,
            input.Method, input.Amount, input.SettlementDate, input.ExternalReference,
            settlementDetails == null ? null : JsonSerializer.Serialize(settlementDetails));
        await feeRefundRepository.UpdateAsync(request, autoSave: false);
        if (!wasAlreadyProcessed)
        {
            await WriteAuditAsync(request, FinanceAuditActions.Refund,
                $"Refund {request.VoucherNumber} processed for {request.ProcessedAmount:F2} {request.Currency}.",
                context: new { input.Method, input.ExternalReference, input.SettlementDate });
            await eventBus.PublishAsync(new FeeRefundApprovedEto
            {
                RefundRequestId = request.Id, StudentId = request.StudentId,
                AcademicYearId = request.AcademicYearId, TotalRefundAmount = Convert.ToDouble(request.ProcessedAmount),
                TenantId = CurrentTenant.Id, ApprovedAt = Clock.Now
            });
        }
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse)]
    public async Task<FeeRefundRequestDto> ReverseAsync(Guid id, ReverseFeeRefundDto input)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        await feeRefundManager.ReverseAsync(request, input.SettlementId, currentUser.GetId(), input.Reason);
        await feeRefundRepository.UpdateAsync(request, autoSave: false);
        await WriteAuditAsync(request, FinanceAuditActions.Reverse,
            $"Processed refund {request.VoucherNumber} reversed.", input.Reason,
            new { input.SettlementId, request.ReversedAmount });
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<FeeRefundPreviewDto> PreviewAsync(CreateFeeRefundRequestDto input)
    {
        var prepared = await PrepareAsync(input);
        return new FeeRefundPreviewDto
        {
            StudentFeeStructureId = prepared.AssignmentId, RefundPolicyId = prepared.PolicyId,
            RefundPolicyVersionId = prepared.PolicyVersionId, RefundPolicyName = prepared.Policy.PolicyName,
            SettledPaidAmount = prepared.Calculation.SettledPaidAmount,
            PriorProcessedRefund = prepared.Calculation.PriorProcessedRefund,
            ActiveReservation = prepared.Calculation.ActiveReservation,
            RequestedAmount = input.Lines.Sum(x => x.Amount), DeductionAmount = prepared.Calculation.DeductionAmount,
            CapAdjustment = prepared.Calculation.CapAdjustment, MaximumAllowedAmount = prepared.Calculation.FinalRefund,
            IsAllowed = true
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<FeeRefundRequestDto> UpdateStatusAsync(Guid id, UpdateFeeRefundStatusDto input)
    {
        switch (input.Status)
        {
            case FeeRefundStatus.UnderReview:
                await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.StartReview);
                return await StartReviewAsync(id);
            case FeeRefundStatus.Approved:
                await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve);
                return await ApproveAsync(id, new FeeRefundActionDto { Notes = input.Notes });
            case FeeRefundStatus.Rejected:
                await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject);
                return await RejectAsync(id, new RejectFeeRefundDto { Notes = input.Notes ?? string.Empty });
            case FeeRefundStatus.Cancelled:
                await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Cancel);
                return await CancelAsync(id, new FeeRefundActionDto { Notes = input.Notes });
            default: throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition);
        }
    }

    private async Task<FeeRefundRequest> RequireAsync(Guid id) =>
        await feeRefundRepository.FindWithLinesAsync(id)
        ?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound).WithData("Id", id);

    private async Task<FeeRefundRequestDto> MapAsync(FeeRefundRequest request)
    {
        var map = await BuildStudentMapAsync([request.StudentId]);
        return MapToDto(request, map);
    }

    private async Task EnsureMakerCheckerAsync(FeeRefundRequest request)
    {
        if (request.CreatorId != CurrentUser.Id) return;
        if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.OverrideMakerChecker))
            throw new BusinessException(FeeRefundErrorCodes.MakerCheckerViolation);
    }

    private static void EnsureSettlementMethodAllowed(FeeRefundRequest request, string method)
    {
        if (request.PolicySnapshotJson == null) throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        var policy = JsonSerializer.Deserialize<Domain.Finance.Policies.RefundPolicySnapshot>(request.PolicySnapshotJson)
            ?? throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        if (!TryMapPayoutMethod(method, out var payout) || !policy.AllowedPayoutMethods.Contains(payout))
            throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
    }

    private async Task WriteAuditAsync(FeeRefundRequest request, string action, string summary,
        string? reason = null, object? context = null)
    {
        var student = await studentRepository.FindAsync(request.StudentId);
        var studentName = student == null ? null : string.Join(" ",
            new[] { student.FirstName, student.MiddleName, student.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        await financeAuditWriter.WriteAsync(request.AcademicYearId, FinanceAuditEntityTypes.FeeRefund,
            request.Id, request.VoucherNumber, action, summary,
            new FinanceAuditStudentContext
            {
                StudentId = request.StudentId, StudentName = studentName,
                GrNo = student?.GrNo.ToString(), RollNo = student?.RollNo.ToString()
            }, reason, context: context ?? new
            {
                request.Status, request.TotalAmount, request.ApprovedAmount,
                request.ProcessedAmount, request.ReversedAmount,
                request.RefundPolicyVersionId, request.OriginalReceiptId
            });
    }
}
