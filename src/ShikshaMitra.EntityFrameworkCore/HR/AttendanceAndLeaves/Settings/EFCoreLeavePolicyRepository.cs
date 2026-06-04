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

public class EfCoreLeavePolicyRepository : EfCoreRepository<ShikshaMitraDbContext, LeavePolicy, Guid>, ILeavePolicyRepository
{
	public EfCoreLeavePolicyRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<bool> IsPolicyNameExistsAsync(Guid academicYearId, string policyName, Guid? excludeId = null)
	{
		var normalizedPolicyName = policyName.ToLowerInvariant();
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.PolicyName.ToLower() == normalizedPolicyName);

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.AnyAsync();
	}

	public async Task<LeavePolicy?> FindByPolicyNameAsync(Guid academicYearId, string policyName)
	{
		var normalizedPolicyName = policyName.ToLowerInvariant();
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.AcademicYearId == academicYearId && x.PolicyName.ToLower() == normalizedPolicyName)
			.Include(x => x.LeaveType)
			.FirstOrDefaultAsync();
	}

	public async Task<List<LeavePolicy>> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		IQueryable<LeavePolicy> query = dbSet
			.Include(x => x.LeaveType)
			.Where(x => x.AcademicYearId == academicYearId);

		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.Trim().ToLower();
			query = query.Where(x =>
				x.PolicyName.ToLower().Contains(normalizedSearch) ||
				(x.LeaveType != null && x.LeaveType.Type.ToLower().Contains(normalizedSearch)));
		}

		query = !string.IsNullOrWhiteSpace(sorting)
			? query.OrderBy(sorting)
			: query.OrderBy(x => x.PolicyName);

		return await query.Skip(skipCount).Take(maxResultCount).ToListAsync(cancellationToken);
	}

	public async Task<long> GetCountByAcademicYearIdAsync(
		Guid academicYearId,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		IQueryable<LeavePolicy> query = dbSet.Where(x => x.AcademicYearId == academicYearId);

		if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.Trim().ToLower();
			query = query
				.Include(x => x.LeaveType)
				.Where(x =>
					x.PolicyName.ToLower().Contains(normalizedSearch) ||
					(x.LeaveType != null && x.LeaveType.Type.ToLower().Contains(normalizedSearch)));
		}

		return await query.LongCountAsync(cancellationToken);
	}

	public async Task<LeavePolicy?> GetWithLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.Include(x => x.LeaveType).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
	}
}
