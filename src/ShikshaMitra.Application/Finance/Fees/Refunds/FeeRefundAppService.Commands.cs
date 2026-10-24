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
using Volo.Abp.Domain.Entities;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> CreateAsync(CreateFeeRefundRequestDto input)
    {
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var autoApprove = await userScopeService.IsAdminUserAsync(currentUser.GetId());
        if (autoApprove)
            await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve);
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey))
            throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
        var replay = (await feeRefundRepository.GetListAsync(x => x.RequestIdempotencyKey == input.IdempotencyKey)).SingleOrDefault();
        if (replay != null)
        {
            replay = await RequireAsync(replay.Id);
            await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(replay.StudentId, replay.FinanceFeeAccountId);
            if (!await HasSamePayerAsync(replay, input) || replay.AcademicYearId != input.AcademicYearId ||
                replay.OriginalReceiptId != input.OriginalReceiptId ||
                replay.ReplacesRefundRequestId != input.ReplacesRefundRequestId ||
                replay.RefundMethod != input.RefundMethod || replay.Reason != input.Reason ||
                replay.Lines.Count != input.Lines.Count ||
                input.Lines.Any(x => !replay.Lines.Any(y => y.ChargeId == x.ChargeId && y.DueFeeId == x.DueFeeId &&
                    y.SourceAllocationId == x.SourceAllocationId && y.RequestedAmount == x.Amount)))
                throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
            var replayResult = await MapAsync(replay);
            await uow.CompleteAsync();
            return replayResult;
        }
        FeeRefundRequest? replaced = null;
        if (input.ReplacesRefundRequestId.HasValue)
        {
            replaced = await RequireAsync(input.ReplacesRefundRequestId.Value);
            await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(replaced.StudentId, replaced.FinanceFeeAccountId);
            if (replaced.Status != FeeRefundStatus.Failed || !await HasSamePayerAsync(replaced, input) ||
                replaced.AcademicYearId != input.AcademicYearId || replaced.OriginalReceiptId != input.OriginalReceiptId ||
                (await feeRefundRepository.GetListAsync(x => x.ReplacesRefundRequestId == replaced.Id)).Count != 0)
                throw new BusinessException(FeeRefundErrorCodes.DuplicateRefundRequest);
        }
        if (replaced?.ConcessionRequestId is Guid concessionId)
        {
            if (!replaced.StudentFeeStructureId.HasValue || !replaced.OriginalReceiptId.HasValue ||
                input.Lines.Count != replaced.Lines.Count ||
                input.Lines.Any(x => !replaced.Lines.Any(y => y.ChargeId == x.ChargeId && y.DueFeeId == x.DueFeeId &&
                    y.SourceAllocationId == x.SourceAllocationId && y.ApprovedAmount == x.Amount)))
                throw new BusinessException(FeeRefundErrorCodes.ReservationConflict);
            var specs = replaced.Lines.Select(x => new RefundLineSpec(x.DueFeeId,
                x.SourceAllocationId, x.SourceReceiptId, x.FeeHeadId, x.FeeHeadName,
                x.InstallmentLabel, x.SettledPaidAmount, x.ApprovedAmount, x.ApprovedAmount)).ToList();
            var concessionReplacement = await feeRefundManager.CreateFromConcessionAsync(
                replaced.StudentId, replaced.AcademicYearId, replaced.StudentFeeStructureId.Value,
                concessionId, replaced.OriginalReceiptId.Value, replaced.OriginalReceiptNumber ?? string.Empty,
                specs, replaced.CalculationSnapshotJson ?? "{}", replaced.CalculationHash ?? string.Empty,
                currentUser.GetId(), input.RefundMethod,
                FeeRefundManager.MapRefundPaymentDetails(input.RefundMethod, input.PaymentDetails),
                input.Reason, input.IdempotencyKey, replaced.Id, replaced.FinanceFeeAccountId);
            await WriteAuditAsync(concessionReplacement, FinanceAuditActions.Create,
                $"Linked refund replacement {concessionReplacement.VoucherNumber} submitted.", input.Reason);
            if (autoApprove)
                await ApproveOnAdministratorSubmissionAsync(concessionReplacement, replaced);
            await uow.CompleteAsync();
            return await MapAsync(concessionReplacement);
        }
        var prepared = await PrepareAsync(input);
        if (replaced != null && (prepared.Lines.Sum(x => x.CalculatedAmount) != replaced.ApprovedAmount ||
            prepared.Lines.Count != replaced.Lines.Count ||
            prepared.Lines.Any(x => !replaced.Lines.Any(y => y.SourceAllocationId == x.SourceAllocationId &&
                y.ChargeId == x.ChargeId && y.DueFeeId == x.DueFeeId && y.ApprovedAmount == x.CalculatedAmount))))
            throw new BusinessException(FeeRefundErrorCodes.ReservationConflict);
        var request = await feeRefundManager.CreatePendingAsync(prepared.StudentId, input.AcademicYearId,
            prepared.AssignmentId, prepared.PolicyId, prepared.PolicyVersionId, prepared.Receipt.Id,
            prepared.Receipt.ReceiptNumber, prepared.Lines, input.RefundMethod,
            FeeRefundManager.MapRefundPaymentDetails(input.RefundMethod, input.PaymentDetails), input.Reason,
            prepared.PolicyJson, prepared.CalculationJson, prepared.Hash,
            currentUser.GetId(), input.IdempotencyKey, input.ReplacesRefundRequestId, prepared.FinanceFeeAccountId);
        await WriteAuditAsync(request, FinanceAuditActions.Create,
            $"Refund request {request.VoucherNumber} submitted for review.", input.Reason);
        if (autoApprove)
            await ApproveOnAdministratorSubmissionAsync(request);
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Edit)]
    public async Task<FeeRefundRequestDto> UpdateAsync(Guid id, UpdateFeeRefundRequestDto input)
    {
        var request = await RequireAsync(id);
        if (request.ConcessionRequestId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.CannotModifyNonPendingRefund);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        var create = new CreateFeeRefundRequestDto
        {
            StudentId = request.StudentId, FinanceFeeAccountId = request.FinanceFeeAccountId, AcademicYearId = request.AcademicYearId,
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
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        await WriteAuditAsync(request, FinanceAuditActions.Delete,
            $"Refund request {request.VoucherNumber} deleted.");
        await feeRefundRepository.DeleteAsync(request, autoSave: true);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Submit)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> SubmitAsync(Guid id)
    {
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        var autoApprove = request.CreatorId == currentUser.GetId() &&
            await userScopeService.IsAdminUserAsync(currentUser.GetId());
        if (autoApprove)
            await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve);
        request.Submit(currentUser.GetId());
        await feeRefundRepository.UpdateAsync(request, autoSave: true);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Refund request {request.VoucherNumber} submitted for review.");
        if (autoApprove)
            await ApproveOnAdministratorSubmissionAsync(request);
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    private async Task ApproveOnAdministratorSubmissionAsync(FeeRefundRequest request,
        FeeRefundRequest? replaced = null)
    {
        var method = request.RefundMethod ?? "Cash";
        EnsureSettlementMethodAllowed(request, method);
        var existingReservationIds = request.Reservations.Select(x => x.Id).ToHashSet();
        await feeRefundManager.ApproveAsync(request, currentUser.GetId(),
            "Automatically approved on administrator submission.");
        await feeRefundRepository.StageNewReservationsAsync(
            request.Reservations.Where(x => !existingReservationIds.Contains(x.Id)));
        await WriteAuditAsync(request, FinanceAuditActions.Approve,
            $"Refund request {request.VoucherNumber} automatically approved and payment allocations reserved.",
            "Administrator submission; automatic approval.",
            new { request.ExpenseCategoryId, request.PayingBankAccountId, request.RefundMethod });
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.StartReview)]
    public async Task<FeeRefundRequestDto> StartReviewAsync(Guid id)
    {
        var request = await RequireAsync(id);
        if (request.WorkflowVersion == 1)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        request.MarkUnderReview(currentUser.GetId());
        await feeRefundRepository.UpdateAsync(request, autoSave: true);
        await WriteAuditAsync(request, FinanceAuditActions.Update,
            $"Review started for refund request {request.VoucherNumber}.");
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> ApproveAsync(Guid id, FeeRefundActionDto input)
    {
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        await EnsureMakerCheckerAsync(request);
        var method = input.Method ?? request.RefundMethod ?? "Cash";
        EnsureSettlementMethodAllowed(request, method);
        var existingReservationIds = request.Reservations.Select(x => x.Id).ToHashSet();
        await feeRefundManager.ApproveAsync(request, currentUser.GetId(), input.Notes);
        await feeRefundRepository.StageNewReservationsAsync(
            request.Reservations.Where(x => !existingReservationIds.Contains(x.Id)));
        await WriteAuditAsync(request, FinanceAuditActions.Approve,
            $"Refund request {request.VoucherNumber} approved and its payment allocations reserved.", input.Notes,
            new { request.ExpenseCategoryId, request.PayingBankAccountId, request.RefundMethod });
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject)]
    [UnitOfWork(IsDisabled = true)]
    public async Task<FeeRefundRequestDto> RejectAsync(Guid id, RejectFeeRefundDto input)
    {
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        await EnsureMakerCheckerAsync(request);
        request.Reject(currentUser.GetId(), input.Notes);
        await WriteAuditAsync(request, FinanceAuditActions.Reject,
            $"Refund request {request.VoucherNumber} rejected.", input.Notes);
        await uow.CompleteAsync();
        return await MapAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Cancel)]
    public async Task<FeeRefundRequestDto> CancelAsync(Guid id, FeeRefundActionDto input)
    {
        var request = await RequireAsync(id);
        if (request.WorkflowVersion == 1)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
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
        if (request.WorkflowVersion == 1)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
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
                RefundRequestId = request.Id, StudentId = request.StudentId, FinanceFeeAccountId = request.FinanceFeeAccountId,
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
        if (request.WorkflowVersion == 1)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
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
        ?? throw new EntityNotFoundException(typeof(FeeRefundRequest), id);

    private async Task<FeeRefundRequestDto> MapAsync(FeeRefundRequest request)
    {
        var map = await BuildStudentMapAsync([request.StudentId]);
        var accounts = await BuildAccountMapAsync([request.FinanceFeeAccountId]);
        var years = await BuildYearMapAsync([request.AcademicYearId]);
        var dto = MapToDto(request, map, accounts, years);
        await FilterAvailableActionsAsync(request, dto);
        return dto;
    }

    private async Task<bool> HasSamePayerAsync(FeeRefundRequest request, CreateFeeRefundRequestDto input)
    {
        if (input.FinanceFeeAccountId.HasValue)
        {
            if (request.FinanceFeeAccountId != input.FinanceFeeAccountId) return false;
            if (!input.StudentId.HasValue) return true;
            var account = await accountRepository.GetAsync(input.FinanceFeeAccountId.Value);
            return account.StudentId == input.StudentId;
        }
        return input.StudentId.HasValue && request.StudentId == input.StudentId && !request.FinanceFeeAccountId.HasValue;
    }

    private async Task EnsureMakerCheckerAsync(FeeRefundRequest request)
    {
        if (request.WorkflowVersion == 1 &&
            (request.CreatorId == CurrentUser.Id || request.SubmittedByUserId == CurrentUser.Id))
            throw new BusinessException(FeeRefundErrorCodes.MakerCheckerViolation);
        if (request.CreatorId != CurrentUser.Id) return;
        if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.OverrideMakerChecker))
            throw new BusinessException(FeeRefundErrorCodes.MakerCheckerViolation);
    }

    private static void EnsureSettlementMethodAllowed(FeeRefundRequest request, string method)
    {
        if (request.ConcessionRequestId.HasValue)
        {
            if (!TryMapPayoutMethod(method, out _))
                throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
            return;
        }
        if (request.PolicySnapshotJson == null) throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        var policy = JsonSerializer.Deserialize<Domain.Finance.Policies.RefundPolicySnapshot>(request.PolicySnapshotJson)
            ?? throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        if (!TryMapPayoutMethod(method, out var payout) || !policy.AllowedPayoutMethods.Contains(payout))
            throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
    }

    private async Task WriteAuditAsync(FeeRefundRequest request, string action, string summary,
        string? reason = null, object? context = null)
    {
        var student = await AsyncExecuter.FirstOrDefaultAsync((await studentRepository.GetQueryableAsync())
            .Where(x => x.Id == request.StudentId)
            .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.GrNo, x.RollNo }));
        var studentName = student == null ? null : string.Join(" ",
            new[] { student.FirstName, student.MiddleName, student.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (studentName == null && request.FinanceFeeAccountId.HasValue)
            studentName = (await accountRepository.GetAsync(request.FinanceFeeAccountId.Value)).ApplicantName;
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
