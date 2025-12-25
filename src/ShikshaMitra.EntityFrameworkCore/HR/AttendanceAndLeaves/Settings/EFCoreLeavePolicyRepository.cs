using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
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

	public async Task<bool> IsPolicyNameExistsAsync(Guid educationMediumStageId, string policyName, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => 
			x.EducationMediumStageId == educationMediumStageId && 
			x.PolicyName.ToLower() == policyName.ToLower());
		
		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}
		
		return await query.AnyAsync();
	}

	public async Task<LeavePolicy?> FindByPolicyNameAsync(Guid educationMediumStageId, string policyName)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => 
				x.EducationMediumStageId == educationMediumStageId && 
				x.PolicyName.ToLower() == policyName.ToLower())
			.Include(x => x.LeaveType)
			.FirstOrDefaultAsync();
	}

	public async Task<List<LeavePolicy>> GetListByMediumStageIdAsync(
		Guid educationMediumStageId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		
		// Build base query with includes
		IQueryable<LeavePolicy> query = dbSet
			.Include(x => x.LeaveType)
			.Where(x => x.EducationMediumStageId == educationMediumStageId);

		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		if (!string.IsNullOrWhiteSpace(sorting))
		{
			query = query.OrderBy(sorting);
		}
		else
		{
			query = query.OrderBy(x => x.PolicyName);
		}

		return await query
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToListAsync(cancellationToken);
	}

	public async Task<long> GetCountByMediumStageIdAsync(
		Guid educationMediumStageId,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet
			.Where(x => x.EducationMediumStageId == educationMediumStageId);

		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		return await query.LongCountAsync(cancellationToken);
	}

	public async Task<LeavePolicy?> GetWithLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Include(x => x.LeaveType)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
	}
}
