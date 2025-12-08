using System;
using System.Linq;
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

	public async Task<bool> IsTypeExistsAsync(string type, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => x.Type.ToLower() == type.ToLower());
		
		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}
		
		return await query.AnyAsync();
	}

	public async Task<LeaveType?> FindByTypeAsync(string type)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.Type.ToLower() == type.ToLower())
			.FirstOrDefaultAsync();
	}
}
