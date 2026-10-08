using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

/// <summary>Protects routing for pending and outstanding charges while allowing historical masters to retire.</summary>
public class ChargeRoutingUsageManager(
    IRepository<ChargeRequest, Guid> requests,
    IRepository<Charge, Guid> charges,
    IRepository<ChargeCollectionRecord, Guid> collections,
    IRepository<FeeRecordMetadata, Guid> receipts,
    IRepository<ChargeWaiverApplication, Guid> waivers) : DomainService
{
    public async Task<bool> HasOpenChargesAsync(Guid? categoryId = null)
    {
        var requestQuery = await requests.GetQueryableAsync();
        var chargeQuery = await charges.GetQueryableAsync();
        var linked = await AsyncExecuter.ToListAsync(
            from request in requestQuery
            join charge in chargeQuery on request.Id equals charge.ChargeRequestId
            where (!categoryId.HasValue || request.IncomeCategoryId == categoryId) &&
                  (charge.Status == ChargeStatus.Pending || charge.Status == ChargeStatus.Approved)
            select new { charge.Id, charge.Amount, charge.Status });
        if (linked.Any(x => x.Status == ChargeStatus.Pending)) return true;
        if (linked.Count == 0) return false;
        var ids = linked.Select(x => x.Id).ToArray();
        var collectionQuery = await collections.GetQueryableAsync();
        var receiptQuery = await receipts.GetQueryableAsync();
        var paid = await AsyncExecuter.ToListAsync(
            from payment in collectionQuery
            join receipt in receiptQuery on payment.FeeRecordMetadataId equals receipt.Id
            where Enumerable.Contains(ids, payment.ChargeId) && receipt.Status != FeeCollectionStatus.Reversed
            select new { payment.ChargeId, payment.Amount });
        var waived = await waivers.GetListAsync(x => Enumerable.Contains(ids, x.ChargeId));
        return linked.Any(charge => charge.Amount >
            paid.Where(x => x.ChargeId == charge.Id).Sum(x => Math.Round((decimal)x.Amount, 2)) +
            waived.Where(x => x.ChargeId == charge.Id).Sum(x => x.EffectiveAmount));
    }
}
