using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Policies;
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
		return await IncludeRules(dbSet)
			.Where(x => x.AcademicYearId == academicYearId && x.PolicyName.ToLower() == normalizedPolicyName)
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
		IQueryable<LeavePolicy> query = IncludeRules(dbSet)
			.Where(x => x.AcademicYearId == academicYearId);

		query = ApplyActiveFilter(query, includeInactive);
		query = ApplySearch(query, search);

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
		query = ApplyActiveFilter(query, includeInactive);
		query = ApplySearch(query, search);

		return await query.LongCountAsync(cancellationToken);
	}

	public async Task<LeavePolicy?> GetWithRulesAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await IncludeRules(dbSet).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
	}

	public async Task<bool> HasActiveDefaultAsync(
		Guid academicYearId,
		Guid? excludeId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsActive &&
			x.Status == HrPolicyStatus.Active &&
			x.IsDefault);

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.AnyAsync(cancellationToken);
	}

	public async Task<List<LeavePolicy>> GetActiveDefaultsAsync(
		Guid academicYearId,
		Guid? excludeId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsActive &&
			x.Status == HrPolicyStatus.Active &&
			x.IsDefault);

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.ToListAsync(cancellationToken);
	}

	public async Task<LeavePolicyRule?> GetRuleAsync(Guid leavePolicyId, LeavePaidType paidType, CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		return await dbContext.EmployeeLeavePolicyRules
			.FirstOrDefaultAsync(x =>
				x.LeavePolicyId == leavePolicyId &&
				x.PaidType == paidType &&
				x.IsActive,
				cancellationToken);
	}

	public async Task<LeavePolicy?> FindWithoutDetailsAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.IgnoreAutoIncludes()
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
	}

	private static IQueryable<LeavePolicy> IncludeRules(IQueryable<LeavePolicy> query) =>
		query.Include(x => x.Rules);

	private static IQueryable<LeavePolicy> ApplyActiveFilter(IQueryable<LeavePolicy> query, bool includeInactive) =>
		includeInactive ? query : query.Where(x => x.IsActive);

	private static IQueryable<LeavePolicy> ApplySearch(IQueryable<LeavePolicy> query, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return query;
		}

		var normalizedSearch = search.Trim().ToLower();
		return query.Where(x =>
			x.PolicyName.ToLower().Contains(normalizedSearch) ||
			x.Rules.Any(rule => rule.PaidType.ToString().ToLower().Contains(normalizedSearch)));
	}
}
