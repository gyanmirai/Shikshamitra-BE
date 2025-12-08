using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// EF Core repository implementation for LeavePolicy
/// </summary>
public class EfCoreLeavePolicyRepository : EfCoreRepository<ShikshaMitraDbContext, LeavePolicy, Guid>, ILeavePolicyRepository
{
	public EfCoreLeavePolicyRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<bool> IsPolicyNameExistsAsync(string policyName, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => x.PolicyName.ToLower() == policyName.ToLower());
		
		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}
		
		return await query.AnyAsync();
	}

	public async Task<LeavePolicy?> FindByPolicyNameAsync(string policyName)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.PolicyName.ToLower() == policyName.ToLower())
			.FirstOrDefaultAsync();
	}
}
