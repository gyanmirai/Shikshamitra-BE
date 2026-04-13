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

public class EfCoreLeaveTypeRepository : EfCoreRepository<ShikshaMitraDbContext, LeaveType, Guid>, ILeaveTypeRepository
{
	public EfCoreLeaveTypeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<bool> IsTypeExistsAsync(Guid academicYearId, string type, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.Type.ToLower() == type.ToLower());

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.AnyAsync();
	}

	public async Task<LeaveType?> FindByTypeAsync(Guid academicYearId, string type)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x =>
				x.AcademicYearId == academicYearId &&
				x.Type.ToLower() == type.ToLower())
			.FirstOrDefaultAsync();
	}

	public async Task<List<LeaveType>> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet
			.AsNoTracking()
			.Where(x => x.AcademicYearId == academicYearId);

		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.Trim().ToLower();
			query = query.Where(x => x.Type.ToLower().Contains(normalizedSearch));
		}

		query = !string.IsNullOrWhiteSpace(sorting)
			? query.OrderBy(sorting)
			: query.OrderBy(x => x.Type);

		return await query.Skip(skipCount).Take(maxResultCount).ToListAsync(cancellationToken);
	}

	public async Task<long> GetCountByAcademicYearIdAsync(
		Guid academicYearId,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.AsNoTracking().Where(x => x.AcademicYearId == academicYearId);

		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.Trim().ToLower();
			query = query.Where(x => x.Type.ToLower().Contains(normalizedSearch));
		}

		return await query.LongCountAsync(cancellationToken);
	}
}
