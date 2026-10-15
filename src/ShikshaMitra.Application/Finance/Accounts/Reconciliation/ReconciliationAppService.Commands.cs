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

        await eventBus.PublishAsync(new FeeReconciliationApprovedEto
        {
            ProvisionalReceiptId = provisionalReceiptId,
            FinalReceiptId       = finalReceipt.Id,
            FinalReceiptNumber   = finalReceipt.ReceiptNumber,
            StudentId            = finalReceipt.StudentId,
            FinanceFeeAccountId  = finalReceipt.FinanceFeeAccountId,
            AcademicYearId       = finalReceipt.AcademicYearId,
            ReceiptKind          = finalReceipt.ReceiptKind,
            TotalPaidAmount      = finalReceipt.TotalPaidAmount,
            TenantId             = CurrentTenant.Id,
            ApprovedAt           = DateTime.UtcNow,
        });

        var verifiedProvisional = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken);
        return BuildActionResponse(verifiedProvisional, finalReceipt.ReceiptNumber, input.Remarks);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add)]
    public async Task<ReconciliationActionResponse> RejectAsync(
        Guid provisionalReceiptId,
        RejectReconciliationInput input,
        CancellationToken cancellationToken = default)
    {
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

        await eventBus.PublishAsync(new FeeReconciliationRejectedEto
        {
            ProvisionalReceiptId     = rejected.Id,
            ProvisionalReceiptNumber = rejected.ReceiptNumber,
            StudentId                = rejected.StudentId,
            FinanceFeeAccountId      = rejected.FinanceFeeAccountId,
            AcademicYearId           = rejected.AcademicYearId,
            ReceiptKind              = rejected.ReceiptKind,
            Remarks                  = input.Remarks,
            TenantId                 = CurrentTenant.Id,
            RejectedAt               = DateTime.UtcNow,
        });

        return BuildActionResponse(rejected, null, input.Remarks);
    }
}
