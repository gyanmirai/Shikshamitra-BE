using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class CollectFeeWorkflow
{
    private async Task PopulateSavedReceiptAmountsAsync(CollectFeeOutput output, Guid transactionId)
    {
        if ((!output.FeeReceiptId.HasValue || output.FeeReceiptAmount.HasValue) &&
            (!output.ChargeReceiptId.HasValue || output.ChargeReceiptAmount.HasValue)) return;

        // Older saved responses have no document totals. Read the issued evidence;
        // historical fee receipts can include late fees even when a charge sibling exists.
        var receipts = await LazyServiceProvider.LazyGetRequiredService<IFeePaymentReceiptRepository>()
            .GetListAsync(r => r.FeeCollectionTransactionId == transactionId &&
                (r.Id == output.FeeReceiptId || r.Id == output.ChargeReceiptId));
        output.FeeReceiptAmount = receipts.FirstOrDefault(r => r.Id == output.FeeReceiptId) is { } fee
            ? (decimal)fee.TotalPaidAmount : null;
        output.ChargeReceiptAmount = receipts.FirstOrDefault(r => r.Id == output.ChargeReceiptId) is { } charge
            ? (decimal)charge.TotalPaidAmount : null;
    }
}
