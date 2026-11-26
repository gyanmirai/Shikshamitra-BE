using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.HR;

internal static class HrSchoolScopeTestHelper
{
	public static async Task EnsureCurrentActorAsync(IServiceProvider services)
	{
		var userId = services.GetRequiredService<ICurrentUser>().GetId();
		var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
		var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>();
		if (await users.FindAsync(userId) == null)
		{
			await users.InsertAsync(new IdentityUser(userId, $"hr-fixture-{userId:N}",
				$"hr-fixture-{userId:N}@example.invalid", tenantId), autoSave: true);
		}

		var scopes = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		if (!(await scopes.GetListAsync(x => x.UserId == userId && x.IsActive)).Any(x => x.GrantsAllAcademicYears()))
		{
			await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId,
				userId: userId, description: "School-wide application test scope"), autoSave: true);
		}
	}
}
