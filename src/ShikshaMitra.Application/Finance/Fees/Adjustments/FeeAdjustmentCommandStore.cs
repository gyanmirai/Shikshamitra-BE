using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Audit;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

/// <summary>Durable command receipts use the audit table's primary key to reject
/// concurrent duplicate keys in the same balance transaction.</summary>
public class FeeAdjustmentCommandStore(
    IRepository<FinanceAuditEvent, Guid> events,
    ICurrentTenant tenant,
    ICurrentUser user,
    IClock clock) : ITransientDependency
{
    private static string Hash(object payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));

    private Guid ReceiptId(string action, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            throw new BusinessException(FeeAdjustmentErrorCodes.DuplicateIdempotencyKey);
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"adjustment:{tenant.Id}:{user.GetId():N}:{action}:{key}")).AsSpan(0, 16));
    }

    public async Task<Guid?> ReplayAsync(string action, string key, object payload)
    {
        var receipt = await events.FindAsync(ReceiptId(action, key));
        if (receipt == null) return null;
        if (receipt.ActorUserId != user.GetId() || receipt.CorrelationId != Hash(payload))
            throw new BusinessException(FeeAdjustmentErrorCodes.DuplicateIdempotencyKey);
        return receipt.EntityId;
    }

    public Task RecordAsync(string action, string key, object payload, Guid requestId,
        Guid studentId, Guid academicYearId) => events.InsertAsync(
            new FinanceAuditEvent(ReceiptId(action, key), tenant.Id, academicYearId,
                "fees.adjustment", "adjustment_command_receipt", requestId, string.Empty,
                action, studentId, null, null, null, user.GetId(), null, null,
                clock.Now, "Adjustment command completed.", correlationId: Hash(payload)),
            autoSave: false);
}
