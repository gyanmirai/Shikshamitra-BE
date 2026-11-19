using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
namespace ShikshaMitra.Finance.Accounts.Settings;
public class AccountSettingVersionHistoryReader(
    IAuditLogRepository auditLogRepository,
    IRepository<IdentityUser, Guid> userRepository,
    IAsyncQueryableExecuter executer) : ITransientDependency
{
    public async Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> ReadAsync<T>(
        Guid id, DateTime creationTime, Guid? creatorId, IReadOnlySet<string> visibleFields,
        int skipCount, int maxResultCount, int? currentVersion = null)
    {
        var entityId = id.ToString();
        var entityType = typeof(T).FullName!;
        var query = (await auditLogRepository.GetQueryableAsync())
            .SelectMany(log => log.EntityChanges, (log, change) => new { log.UserId, Change = change })
            .Where(x => x.Change.EntityId == entityId && x.Change.EntityTypeFullName == entityType);
        var count = await executer.CountAsync(query);
        var hasCreation = await executer.AnyAsync(query.Where(x => x.Change.ChangeType == EntityChangeType.Created));
        var syntheticCreation = hasCreation ? 0 : 1;
        var missing = Math.Max(0, (currentVersion ?? count + syntheticCreation) - count - syntheticCreation);
        var total = count + syntheticCreation + missing;
        var skip = Math.Max(0, skipCount);
        var take = Math.Min(Math.Clamp(maxResultCount, 1, 100), Math.Max(0, total - skip));
        if (take == 0) return new(total, []);
        var ordered = query.OrderByDescending(x => x.Change.ChangeTime).ThenByDescending(x => x.Change.Id);
        var prefixCount = count - (hasCreation ? 1 : 0);
        var prefixTake = Math.Max(0, Math.Min(take, prefixCount - skip));
        var fields = visibleFields.ToArray();
        var history = new List<AccountSettingVersionHistoryItemDto>();
        async Task LoadAsync(int offset, int size, int firstVersion)
        {
            if (size == 0) return;
            var page = await executer.ToListAsync(ordered.Skip(offset).Take(size).Select(x => new AccountSettingVersionHistoryItemDto
            {
                Id = x.Change.Id, ChangeTime = x.Change.ChangeTime, ActorId = x.UserId,
                ChangeAction = x.Change.ChangeType == EntityChangeType.Created ? "created" :
                    x.Change.ChangeType == EntityChangeType.Deleted ? "deleted" : "updated",
                ChangedFields = x.Change.PropertyChanges.Where(p => Enumerable.Contains(fields, p.PropertyName))
                    .Select(p => new AccountSettingVersionFieldChangeDto { FieldName = p.PropertyName, OriginalValue = p.OriginalValue, NewValue = p.NewValue }).ToList()
            }));
            for (var index = 0; index < page.Count; index++) page[index].VersionNumber = firstVersion - index;
            history.AddRange(page);
        }
        await LoadAsync(skip, prefixTake, total - skip);
        for (var index = skip + prefixTake; index < skip + take; index++)
        {
            if (index == total - 1)
            {
                if (hasCreation) await LoadAsync(count - 1, 1, 1);
                else history.Add(new() { Id = id, VersionNumber = 1, ChangeAction = "created", ChangeTime = creationTime, ActorId = creatorId });
            }
            else history.Add(new() { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{id}:{total - index}")).AsSpan(0, 16)), VersionNumber = total - index, ChangeAction = "unavailable" });
        }
        var actorIds = history.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value).Distinct().ToArray();
        if (actorIds.Length > 0)
        {
            var users = await userRepository.GetListAsync(x => Enumerable.Contains(actorIds, x.Id));
            var names = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
            foreach (var item in history) item.ActorName = item.ActorId.HasValue ? names.GetValueOrDefault(item.ActorId.Value) : null;
        }
        return new(total, history);
    }
}
