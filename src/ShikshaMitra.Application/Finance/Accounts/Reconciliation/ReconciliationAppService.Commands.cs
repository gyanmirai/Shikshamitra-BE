using System.Data;
using Volo.Abp.Uow;
using ShikshaMitra.Finance.Fees.Receipts;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Events;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public partial class ReconciliationAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add)]
    public async Task<ReconciliationActionResponse> ApproveAsync(
        Guid provisionalReceiptId,
        ApproveReconciliationInput input,
        CancellationToken cancellationToken = default)
    {
        using var verificationUow = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>().Begin(
            new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var validator  = new ApproveReconciliationRequestValidator();
        var validation = validator.Validate(input);
        if (!validation.IsValid)
            throw new Volo.Abp.UserFriendlyException(validation.Errors[0].ErrorMessage);

        var provisionalForScope = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(provisionalForScope.StudentId, provisionalForScope.FinanceFeeAccountId, cancellationToken);

        var finalReceipt = await reconciliationManager.ApproveAsync(
            provisionalReceiptId, input.Remarks, cancellationToken);

        logger.LogInformation(
            "[ReconciliationAppService] Approved: ProvisionalId={ProvisionalId}, FinalReceiptId={FinalId}, ActedBy={Actor}",
            provisionalReceiptId, finalReceipt.Id, currentUser.Id);

        await verificationUow.SaveChangesAsync(cancellationToken);
        var finals = await receiptRepository.GetListAsync(r => r.FeeCollectionTransactionId == finalReceipt.FeeCollectionTransactionId &&
            r.LinkedProvisionalReceiptId != null, cancellationToken: cancellationToken);
        foreach (var document in finals)
            await eventBus.PublishAsync(new FeeReconciliationApprovedEto
            {
                ProvisionalReceiptId = document.LinkedProvisionalReceiptId!.Value,
                FinalReceiptId = document.Id, FinalReceiptNumber = document.ReceiptNumber,
                StudentId = document.StudentId, FinanceFeeAccountId = document.FinanceFeeAccountId,
                AcademicYearId = document.AcademicYearId, ReceiptKind = document.ReceiptKind,
                TotalPaidAmount = document.TotalPaidAmount, TenantId = CurrentTenant.Id, ApprovedAt = DateTime.UtcNow
            });

        var verifiedProvisional = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken);
        var response = BuildActionResponse(verifiedProvisional, finalReceipt.ReceiptNumber, input.Remarks);
        await verificationUow.CompleteAsync(cancellationToken);
        return response;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add)]
    public async Task<ReconciliationActionResponse> RejectAsync(
        Guid provisionalReceiptId,
        RejectReconciliationInput input,
        CancellationToken cancellationToken = default)
    {
        using var verificationUow = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>().Begin(
            new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
        var validator  = new RejectReconciliationRequestValidator();
        var validation = validator.Validate(input);
        if (!validation.IsValid)
            throw new Volo.Abp.UserFriendlyException(validation.Errors[0].ErrorMessage);

        var provisionalForScope = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(provisionalForScope.StudentId, provisionalForScope.FinanceFeeAccountId, cancellationToken);

        var rejected = await reconciliationManager.RejectAsync(
            provisionalReceiptId, input.Remarks, cancellationToken);

        logger.LogInformation(
            "[ReconciliationAppService] Rejected: ReceiptId={ReceiptId}, ActedBy={Actor}, Remarks={Remarks}",
            rejected.Id, currentUser.Id, input.Remarks);

        var rejectedDocuments = await receiptRepository.GetListAsync(r => r.FeeCollectionTransactionId == rejected.FeeCollectionTransactionId &&
            r.LinkedProvisionalReceiptId == null, cancellationToken: cancellationToken);
        foreach (var document in rejectedDocuments)
            await eventBus.PublishAsync(new FeeReconciliationRejectedEto
            {
                ProvisionalReceiptId = document.Id, ProvisionalReceiptNumber = document.ReceiptNumber,
                StudentId = document.StudentId, FinanceFeeAccountId = document.FinanceFeeAccountId,
                AcademicYearId = document.AcademicYearId, ReceiptKind = document.ReceiptKind,
                Remarks = input.Remarks, TenantId = CurrentTenant.Id, RejectedAt = DateTime.UtcNow
            });
        await verificationUow.CompleteAsync(cancellationToken);

        return BuildActionResponse(rejected, null, input.Remarks);
    }
}
