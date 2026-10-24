using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

// Called only after the owning inquiry/application/onboarding scope has been checked.
[RemoteService(false)]
public class StudentJourneyActorReader(IRepository<IdentityUser, Guid> users) : ApplicationService, ITransientDependency
{
	public async Task<Dictionary<Guid, string>> ReadAsync(IEnumerable<Guid?> actorIds)
	{
		var ids = actorIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
		if (ids.Length == 0) return [];
		var query = (await users.GetQueryableAsync()).Where(user => user.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, user.Id));
		var names = await AsyncExecuter.ToListAsync(query.Select(user => new { user.Id, user.Name, user.Surname, user.UserName }));
		return names.ToDictionary(user => user.Id, user =>
			string.IsNullOrWhiteSpace(user.Name) && string.IsNullOrWhiteSpace(user.Surname)
				? user.UserName : $"{user.Name} {user.Surname}".Trim());
	}

	public static string? Name(Dictionary<Guid, string> names, Guid? actorId) =>
		actorId.HasValue ? names.GetValueOrDefault(actorId.Value, "Former staff (name unavailable)") : null;
}
