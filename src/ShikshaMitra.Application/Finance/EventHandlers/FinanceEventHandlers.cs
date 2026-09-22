using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Finance.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.Finance.EventHandlers;

/// <summary>
/// Stub handlers for Finance domain events.
/// Extend these to send notifications, update read models, or trigger downstream workflows.
/// </summary>
public class FeeCollectedEventHandler(ILogger<FeeCollectedEventHandler> logger)
    : IDistributedEventHandler<FeeCollectedEto>, ITransientDependency
{
    public Task HandleEventAsync(FeeCollectedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Fee collected: StudentId={StudentId}, TxId={TxId}, Amount={Amount}, Status={Status}",
            eventData.StudentId, eventData.FeeCollectionTransactionId,
            eventData.TotalPaidFee, eventData.Status);
        return Task.CompletedTask;
    }
}

public class FeeReconciliationApprovedEventHandler(ILogger<FeeReconciliationApprovedEventHandler> logger)
    : IDistributedEventHandler<FeeReconciliationApprovedEto>, ITransientDependency
{
    public Task HandleEventAsync(FeeReconciliationApprovedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Reconciliation approved: StudentId={StudentId}, FinalReceipt={Receipt}, Kind={Kind}",
            eventData.StudentId, eventData.FinalReceiptNumber, eventData.ReceiptKind);
        return Task.CompletedTask;
    }
}

public class FeeReconciliationRejectedEventHandler(ILogger<FeeReconciliationRejectedEventHandler> logger)
    : IDistributedEventHandler<FeeReconciliationRejectedEto>, ITransientDependency
{
    public Task HandleEventAsync(FeeReconciliationRejectedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Reconciliation rejected: StudentId={StudentId}, Receipt={Receipt}, Kind={Kind}",
            eventData.StudentId, eventData.ProvisionalReceiptNumber, eventData.ReceiptKind);
        return Task.CompletedTask;
    }
}

public class ConcessionApprovedEventHandler(ILogger<ConcessionApprovedEventHandler> logger)
    : IDistributedEventHandler<ConcessionApprovedEto>, ITransientDependency
{
    public Task HandleEventAsync(ConcessionApprovedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Concession approved: StudentId={StudentId}, Amount={Amount}",
            eventData.StudentId, eventData.TotalConcessionAmount);
        return Task.CompletedTask;
    }
}

public class ChargeApprovedEventHandler(ILogger<ChargeApprovedEventHandler> logger)
    : IDistributedEventHandler<ChargeApprovedEto>, ITransientDependency
{
    public Task HandleEventAsync(ChargeApprovedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Charge approved: StudentId={StudentId}, ChargeId={ChargeId}, Amount={Amount}",
            eventData.StudentId, eventData.ChargeId, eventData.Amount);
        return Task.CompletedTask;
    }
}

public class FeeAdjustmentAppliedEventHandler(ILogger<FeeAdjustmentAppliedEventHandler> logger)
    : IDistributedEventHandler<FeeAdjustmentAppliedEto>, ITransientDependency
{
    public Task HandleEventAsync(FeeAdjustmentAppliedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Adjustment applied: StudentId={StudentId}, Voucher={Voucher}, Amount={Amount}",
            eventData.StudentId, eventData.VoucherNumber, eventData.TotalAdjustedAmount);
        return Task.CompletedTask;
    }
}

public class FeeRefundApprovedEventHandler(ILogger<FeeRefundApprovedEventHandler> logger)
    : IDistributedEventHandler<FeeRefundApprovedEto>, ITransientDependency
{
    public Task HandleEventAsync(FeeRefundApprovedEto eventData)
    {
        logger.LogInformation(
            "[Finance] Refund processed: StudentId={StudentId}, RequestId={RequestId}, Amount={Amount}",
            eventData.StudentId, eventData.RefundRequestId, eventData.TotalRefundAmount);
        return Task.CompletedTask;
    }
}
