using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.Linq;
namespace ShikshaMitra.Finance.Accounts.Settings;
internal static class SettingsUsageBatch
{
    public static async Task<HashSet<Guid>> ReadAsync(IQueryable<Guid?> references, Guid[] ids, IAsyncQueryableExecuter executer) =>
        (await executer.ToListAsync(references.Where(id => id.HasValue && Enumerable.Contains(ids, id.Value)).Select(id => id!.Value).Distinct())).ToHashSet();

    public static async Task<HashSet<Guid>> ReadPolicyUsageAsync(IQueryable<FinancePolicyVersion> versions, Guid[] ids, IAsyncQueryableExecuter executer)
    {
        if (ids.Length == 0) return [];
        var parameter = Expression.Parameter(typeof(FinancePolicyVersion), "version");
        Expression predicate = Expression.Constant(false);
        foreach (var id in ids)
            predicate = Expression.OrElse(predicate, Expression.Call(Expression.Property(parameter, nameof(FinancePolicyVersion.SnapshotJson)),
                nameof(string.Contains), Type.EmptyTypes, Expression.Constant(id.ToString())));
        var snapshots = await executer.ToListAsync(versions.Where(Expression.Lambda<Func<FinancePolicyVersion, bool>>(predicate, parameter)).Select(x => x.SnapshotJson));
        return ids.Where(id => snapshots.Any(snapshot => snapshot.Contains(id.ToString()))).ToHashSet();
    }
}
