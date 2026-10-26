using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Audit;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Mobile;

/// <summary>Durable command receipts share the existing audit transaction. The deterministic primary key
/// rejects concurrent duplicate commands, rolling their entire transaction back before a retry.</summary>
public class MobileFinanceCommandStore(IRepository<FinanceAuditEvent, Guid> events, ICurrentTenant tenant,
	ICurrentUser user, IClock clock) : ITransientDependency
{
	private Guid Id(string action, string? key) => MobileFinanceCommandRules.StableId(
		$"family-command:{tenant.Id}:{user.GetId():N}:{action}:{MobileFinanceCommandRules.RequireKey(key)}");

	public async Task<Guid?> ReplayAsync(string action, string? key, object payload)
	{
		var receipt = await events.FindAsync(Id(action, key));
		if (receipt == null) return null;
		if (receipt.ActorUserId != user.GetId() || receipt.CorrelationId != MobileFinanceCommandRules.Hash(payload))
			throw new BusinessException(FamilyFinanceErrorCodes.IdempotencyConflict);
		return receipt.EntityId;
	}

	public Task RecordAsync(string action, string? key, object payload, Guid requestId, Guid child, Guid year) =>
		events.InsertAsync(new FinanceAuditEvent(Id(action, key), tenant.Id, year, "family", "family_command_receipt",
			requestId, string.Empty, action, child, null, null, null, user.GetId(), null, null,
			clock.Now, "Family command completed.", correlationId: MobileFinanceCommandRules.Hash(payload)), autoSave: false);
}
