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
/// EF Core repository implementation for LeaveType
/// </summary>
public class EfCoreLeaveTypeRepository : EfCoreRepository<ShikshaMitraDbContext, LeaveType, Guid>, ILeaveTypeRepository
{
	public EfCoreLeaveTypeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<bool> IsTypeExistsAsync(Guid educationMediumStageId, string type, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => 
			x.EducationMediumStageId == educationMediumStageId && 
			x.Type.ToLower() == type.ToLower());
		
		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}
		
		return await query.AnyAsync();
	}

	public async Task<LeaveType?> FindByTypeAsync(Guid educationMediumStageId, string type)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => 
				x.EducationMediumStageId == educationMediumStageId && 
				x.Type.ToLower() == type.ToLower())
			.FirstOrDefaultAsync();
	}

	public async Task<List<LeaveType>> GetListByMediumStageIdAsync(
		Guid educationMediumStageId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		// Build query with tenant filtering
		var query = dbSet
			.AsNoTracking()
			.Where(x => x.EducationMediumStageId == educationMediumStageId);

		// Apply active filter
		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		// Apply sorting
		if (!string.IsNullOrWhiteSpace(sorting))
		{
			query = query.OrderBy(sorting);
		}
		else
		{
			query = query.OrderBy(x => x.Type);
		}

		// Apply pagination and execute
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

		// Build query with filtering
		var query = dbSet
			.AsNoTracking()
			.Where(x => x.EducationMediumStageId == educationMediumStageId);

		// Apply active filter
		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		return await query.LongCountAsync(cancellationToken);
	}
}
