using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class FinancePolicyVersionCreatorResolver
{
	public static async Task PopulateUserNamesAsync(
		IReadOnlyCollection<FinancePolicyVersionDto> versions,
		IRepository<IdentityUser, Guid> userRepository)
	{
		var creatorIds = versions
			.Where(x => x.CreatorId.HasValue)
			.Select(x => x.CreatorId!.Value)
			.Distinct()
			.ToList();
		if (creatorIds.Count == 0)
		{
			return;
		}

		var users = await userRepository.GetListAsync(x => creatorIds.Contains(x.Id));
		var userNamesById = users.ToDictionary(x => x.Id, x => x.UserName);

		foreach (var version in versions.Where(x => x.CreatorId.HasValue))
		{
			version.CreatorUserName = userNamesById.GetValueOrDefault(version.CreatorId!.Value);
		}
	}
}
