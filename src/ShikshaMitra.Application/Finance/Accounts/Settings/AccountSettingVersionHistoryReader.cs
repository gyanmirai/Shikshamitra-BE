using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AuditLogging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class AccountSettingVersionHistoryReader(
	IAuditLogRepository auditLogRepository,
	IRepository<IdentityUser, Guid> userRepository) : ITransientDependency
{
	public async Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> ReadAsync<T>(
		Guid id,
		DateTime creationTime,
		Guid? creatorId,
		IReadOnlySet<string> visibleFields,
		int skipCount,
		int maxResultCount,
		int? currentVersion = null)
	{
		var changes = await auditLogRepository.GetEntityChangeListAsync(
			sorting: "ChangeTime asc",
			maxResultCount: 1000,
			skipCount: 0,
			entityId: id.ToString(),
			entityTypeFullName: typeof(T).FullName!,
			includeDetails: true);

		var auditLogIds = changes.Select(x => x.AuditLogId).Distinct().ToList();
		var auditLogs = auditLogIds.Count == 0 ? [] :
			await auditLogRepository.GetListAsync(x => auditLogIds.Contains(x.Id));
		var auditLogsById = auditLogs.ToDictionary(x => x.Id);
		var actorIds = auditLogs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value)
			.Concat(creatorId.HasValue ? [creatorId.Value] : [])
			.Distinct().ToList();
		var users = actorIds.Count == 0 ? [] : await userRepository.GetListAsync(x => actorIds.Contains(x.Id));
		var actorNames = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);

		var history = changes.Select(change =>
		{
			auditLogsById.TryGetValue(change.AuditLogId, out var auditLog);
			var actorId = auditLog?.UserId;
			return new AccountSettingVersionHistoryItemDto
			{
				Id = change.Id,
				VersionNumber = 0,
				ChangeAction = change.ChangeType.ToString().ToLowerInvariant(),
				ChangeTime = change.ChangeTime,
				ActorId = actorId,
				ActorName = actorId.HasValue ? actorNames.GetValueOrDefault(actorId.Value) : null,
				ChangedFields = change.PropertyChanges
					.Where(x => visibleFields.Contains(x.PropertyName))
					.Select(x => new AccountSettingVersionFieldChangeDto
					{
						FieldName = x.PropertyName,
						OriginalValue = x.OriginalValue,
						NewValue = x.NewValue
					}).ToList()
			};
		}).ToList();

		if (!history.Any(x => x.ChangeAction == "created"))
		{
			history.Insert(0, new AccountSettingVersionHistoryItemDto
			{
				Id = id,
				VersionNumber = 1,
				ChangeAction = "created",
				ChangeTime = creationTime,
				ActorId = creatorId,
				ActorName = creatorId.HasValue ? actorNames.GetValueOrDefault(creatorId.Value) : null
			});
		}

		// Older account heads can predate entity-history auditing. Preserve the known
		// version sequence without inventing changes, timestamps, or actors.
		var missing = Math.Max(0, (currentVersion ?? history.Count) - history.Count);
		for (var index = 0; index < missing; index++)
			history.Insert(1, new AccountSettingVersionHistoryItemDto
			{
				Id = Guid.NewGuid(),
				ChangeAction = "unavailable"
			});
		for (var index = 0; index < history.Count; index++) history[index].VersionNumber = index + 1;

		var ordered = history.OrderByDescending(x => x.VersionNumber).ToList();
		return new PagedResultDto<AccountSettingVersionHistoryItemDto>(
			ordered.Count,
			ordered.Skip(Math.Max(0, skipCount)).Take(Math.Clamp(maxResultCount, 1, 100)).ToList());
	}
}
