using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Notifications;

[RemoteService(false)]
public class LibraryConfiguredOperationalRecipientReader(IRepository<IdentityUser, Guid> users,
	ILibraryNotificationAuthorityService authority, IUnitOfWorkManager units) : ApplicationService, ITransientDependency
{
	public async Task<List<LibraryConfiguredOperationalRecipientDto>> ReadAsync(IReadOnlyCollection<Guid> savedIds)
	{
		if (!CurrentTenant.Id.HasValue || !await authority.CanManageSettingsAsync()) return [];
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var ids = savedIds.ToArray();
		var rows = await AsyncExecuter.ToListAsync((await users.GetQueryableAsync())
			.Where(user => user.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, user.Id))
			.Select(user => new { user.Id, user.UserName, user.Name, user.Surname }));
		var current = rows.ToDictionary(user => user.Id);
		var result = new List<LibraryConfiguredOperationalRecipientDto>();
		foreach (var id in savedIds)
		{
			current.TryGetValue(id, out var user);
			var eligible = user != null && await authority.CanReceiveOperationalAsync(id);
			var displayName = user == null ? null : string.Join(" ", new[] { user.Name, user.Surname }.Where(part => !string.IsNullOrWhiteSpace(part)));
			result.Add(new() { UserId = id, UserName = user?.UserName,
				DisplayName = string.IsNullOrWhiteSpace(displayName) ? user?.UserName : displayName,
				IsEligible = eligible,
				UnavailableReason = eligible ? null : user == null ? "Account details are unavailable in this school." : "This account is not currently eligible for operational Library alerts." });
		}
		await read.CompleteAsync();
		return await authority.CanManageSettingsAsync() ? result : [];
	}
}
