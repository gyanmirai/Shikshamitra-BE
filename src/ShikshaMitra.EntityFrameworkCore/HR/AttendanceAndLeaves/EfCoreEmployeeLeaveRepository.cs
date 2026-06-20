using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EfCoreEmployeeLeaveRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EmployeeLeave, Guid>(dbContextProvider), IEmployeeLeaveRepository
{
	private static int _sqliteNextPublicId;

	public override async Task<EmployeeLeave> InsertAsync(
		EmployeeLeave entity,
		bool autoSave = false,
		CancellationToken cancellationToken = default)
	{
		await EnsureSqlitePublicIdAsync(entity, cancellationToken);
		return await base.InsertAsync(entity, autoSave, cancellationToken);
	}

	public async Task<EmployeeLeave?> FindWithoutDetailsAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.IgnoreAutoIncludes()
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
	}

	private async Task EnsureSqlitePublicIdAsync(EmployeeLeave entity, CancellationToken cancellationToken)
	{
		if (entity.PublicId > 0)
		{
			return;
		}

		var dbContext = await GetDbContextAsync();
		if (!string.Equals(
			    dbContext.Database.ProviderName,
			    "Microsoft.EntityFrameworkCore.Sqlite",
			    StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var maxPublicId = await dbContext.Set<EmployeeLeave>()
			.Select(x => (int?)x.PublicId)
			.MaxAsync(cancellationToken) ?? 0;
		var nextPublicId = Math.Max(
			Interlocked.Increment(ref _sqliteNextPublicId),
			maxPublicId + 1);

		typeof(EmployeeLeave)
			.GetProperty(nameof(EmployeeLeave.PublicId))!
			.SetValue(entity, nextPublicId);
	}

	public async Task<bool> HasOverlappingLeaveAsync(
		Guid employeeId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		Guid? excludeLeaveId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x =>
			x.EmployeeId == employeeId &&
			!x.IsDeleted &&
			(x.Status == EmployeeLeaveStatusType.Pending ||
			 x.Status == EmployeeLeaveStatusType.Approved ||
			 x.Status == EmployeeLeaveStatusType.Reviewed));

		if (excludeLeaveId.HasValue)
		{
			query = query.Where(x => x.Id != excludeLeaveId.Value);
		}

		var candidateLeaves = await query.ToListAsync();
		return candidateLeaves.Any(x =>
			(x.StartDate <= startDate && x.EndDate >= startDate) ||
			(x.StartDate <= endDate && x.EndDate >= endDate) ||
			(x.StartDate >= startDate && x.EndDate <= endDate));
	}

	public async Task<List<LeaveBalanceUnitRow>> GetLeaveBalanceRowsByRuleAsync(
		Guid employeeId,
		IReadOnlyCollection<Guid> leavePolicyRuleIds,
		CancellationToken cancellationToken = default)
	{
		if (leavePolicyRuleIds.Count == 0)
		{
			return new List<LeaveBalanceUnitRow>();
		}

		var ruleIds = leavePolicyRuleIds.ToList();
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.IgnoreAutoIncludes()
			.AsNoTracking()
			.Where(x =>
				x.EmployeeId == employeeId &&
				ruleIds.Contains(x.LeavePolicyRuleId) &&
				!x.IsDeleted &&
				(x.Status == EmployeeLeaveStatusType.Approved ||
				 x.Status == EmployeeLeaveStatusType.Pending))
			.Select(x => new LeaveBalanceUnitRow(
				x.EmployeeId,
				x.LeavePolicyId,
				x.LeavePolicyRuleId,
				x.PaidType,
				x.Status,
				x.ApprovedLeaveUnits,
				x.StartDate,
				x.EndDate,
				x.HalfDay,
				x.LeavePolicy != null ? x.LeavePolicy.AcademicYearId : (Guid?)null))
			.ToListAsync(cancellationToken);
	}

	public async Task<List<LeaveBalanceUnitRow>> GetLeaveBalanceRowsByPolicyAndPaidTypeAsync(
		Guid employeeId,
		Guid leavePolicyId,
		IReadOnlyCollection<LeavePaidType> paidTypes,
		CancellationToken cancellationToken = default)
	{
		if (paidTypes.Count == 0)
		{
			return new List<LeaveBalanceUnitRow>();
		}

		var typeIds = paidTypes.ToList();
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.IgnoreAutoIncludes()
			.AsNoTracking()
			.Where(x =>
				x.EmployeeId == employeeId &&
				x.LeavePolicyId == leavePolicyId &&
				typeIds.Contains(x.PaidType) &&
				!x.IsDeleted &&
				(x.Status == EmployeeLeaveStatusType.Approved ||
				 x.Status == EmployeeLeaveStatusType.Pending))
			.Select(x => new LeaveBalanceUnitRow(
				x.EmployeeId,
				x.LeavePolicyId,
				x.LeavePolicyRuleId,
				x.PaidType,
				x.Status,
				x.ApprovedLeaveUnits,
				x.StartDate,
				x.EndDate,
				x.HalfDay,
				x.LeavePolicy != null ? x.LeavePolicy.AcademicYearId : (Guid?)null))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlySet<Guid>> GetReferencedRuleIdsAsync(
		IReadOnlyCollection<Guid> leavePolicyRuleIds,
		CancellationToken cancellationToken = default)
	{
		if (leavePolicyRuleIds.Count == 0)
		{
			return new HashSet<Guid>();
		}

		var ruleIds = leavePolicyRuleIds.ToList();
		var dbSet = await GetDbSetAsync();
		var referenced = await dbSet
			.IgnoreAutoIncludes()
			.AsNoTracking()
			.Where(x => ruleIds.Contains(x.LeavePolicyRuleId) && !x.IsDeleted)
			.Select(x => x.LeavePolicyRuleId)
			.Distinct()
			.ToListAsync(cancellationToken);

		return referenced.ToHashSet();
	}

	public async Task<List<PayrollApprovedLeaveRow>> GetApprovedPayrollLeaveRowsAsync(
		IReadOnlyCollection<Guid> employeeIds,
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		CancellationToken cancellationToken = default)
	{
		if (employeeIds.Count == 0)
		{
			return new List<PayrollApprovedLeaveRow>();
		}

		var dbContext = await GetDbContextAsync();
		var dbSet = await GetDbSetAsync();
		var employeeIdList = employeeIds.ToList();
		var query = dbSet
			.IgnoreAutoIncludes()
			.AsNoTracking()
			.Where(x =>
				employeeIdList.Contains(x.EmployeeId) &&
				x.Status == EmployeeLeaveStatusType.Approved);

		if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
		{
			var sqliteRows = await ProjectPayrollLeaveRows(query).ToListAsync(cancellationToken);
			return sqliteRows
				.Where(x => x.StartDate <= rangeEnd && x.EndDate >= rangeStart)
				.ToList();
		}

		return await ProjectPayrollLeaveRows(query
				.Where(x => x.StartDate <= rangeEnd && x.EndDate >= rangeStart))
			.ToListAsync(cancellationToken);
	}

	public async Task<List<ApprovedAttendanceLeaveRow>> GetApprovedAttendanceLeaveRowsForDateAsync(
		IReadOnlyCollection<Guid> employeeIds,
		DateOnly attendanceDate,
		CancellationToken cancellationToken = default)
	{
		if (employeeIds.Count == 0)
		{
			return new List<ApprovedAttendanceLeaveRow>();
		}

		var dbSet = await GetDbSetAsync();
		var employeeIdList = employeeIds.ToList();
		return await dbSet
			.IgnoreAutoIncludes()
			.AsNoTracking()
			.Where(x =>
				employeeIdList.Contains(x.EmployeeId) &&
				x.Status == EmployeeLeaveStatusType.Approved &&
				x.StartDateOnly <= attendanceDate &&
				x.EndDateOnly >= attendanceDate)
			.Select(x => new ApprovedAttendanceLeaveRow(
				x.Id,
				x.EmployeeId,
				x.HalfDay,
				x.StartDate,
				x.EndDate))
			.ToListAsync(cancellationToken);
	}

	public async Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForManagementListAsync(
		DateTime rangeStart,
		DateTime rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId = null,
		IReadOnlyCollection<Guid>? employeeIds = null,
		CancellationToken cancellationToken = default)
	{
		return await EmployeeLeavePageQuery.GetManagementListPageAsync(
			await GetDbContextAsync(),
			await GetDbSetAsync(),
			rangeStart,
			rangeEnd,
			filters,
			search,
			skip,
			take,
			employeeId,
			employeeIds,
			cancellationToken);
	}

	public async Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForCalendarAsync(
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId = null,
		IReadOnlyCollection<Guid>? employeeIds = null,
		CancellationToken cancellationToken = default)
	{
		return await EmployeeLeavePageQuery.GetCalendarPageAsync(
			await GetDbContextAsync(),
			await GetDbSetAsync(),
			rangeStart,
			rangeEnd,
			filters,
			search,
			skip,
			take,
			employeeId,
			employeeIds,
			cancellationToken);
	}

	private static IQueryable<PayrollApprovedLeaveRow> ProjectPayrollLeaveRows(IQueryable<EmployeeLeave> query)
	{
		return query.Select(x => new PayrollApprovedLeaveRow(
			x.EmployeeId,
			x.StartDate,
			x.EndDate,
			x.HalfDay,
			x.ApprovedPaidType,
			x.LeavePolicyRule != null ? x.LeavePolicyRule.PaidType : LeavePaidType.Unpaid));
	}
}
