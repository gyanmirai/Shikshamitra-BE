using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance.Settings;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance.Settings;

/// <summary>
/// EF Core repository implementation for StudentLeaveType
/// </summary>
public class EfCoreStudentLeaveTypeRepository :
	EfCoreRepository<ShikshaMitraDbContext, StudentLeaveType, Guid>,
	IStudentLeaveTypeRepository
{
	public EfCoreStudentLeaveTypeRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<StudentLeaveType?> FindByNameAsync(
		string leaveTypeName,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.TenantId == tenantId)
			.FirstOrDefaultAsync(
				x => x.LeaveTypeName == leaveTypeName,
				GetCancellationToken(cancellationToken));
	}

	public async Task<bool> IsNameExistsAsync(
		string leaveTypeName,
		Guid? excludeId = null,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.TenantId == tenantId)
			.Where(x => x.LeaveTypeName == leaveTypeName)
			.WhereIf(excludeId.HasValue, x => x.Id != excludeId!.Value)
			.AnyAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<List<StudentLeaveType>> GetActiveLeaveTypesAsync(
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.TenantId == tenantId)
			.Where(x => x.IsActive)
			.OrderBy(x => x.LeaveTypeName)
			.ToListAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<List<StudentLeaveType>> GetListAsync(
		string? filterText = null,
		bool? isActive = null,
		string? sorting = null,
		int maxResultCount = int.MaxValue,
		int skipCount = 0,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		var query = ApplyFilter(dbSet, filterText, isActive, tenantId);

		query = query.OrderBy(sorting.IsNullOrWhiteSpace() ? nameof(StudentLeaveType.LeaveTypeName) : sorting);

		return await query
			.PageBy(skipCount, maxResultCount)
			.ToListAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<long> GetCountAsync(
		string? filterText = null,
		bool? isActive = null,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		var query = ApplyFilter(dbSet, filterText, isActive, tenantId);

		return await query.LongCountAsync(GetCancellationToken(cancellationToken));
	}

	private IQueryable<StudentLeaveType> ApplyFilter(
		IQueryable<StudentLeaveType> query,
		string? filterText,
		bool? isActive,
		Guid? tenantId)
	{
		return query
			.Where(x => x.TenantId == tenantId)
			.WhereIf(!filterText.IsNullOrWhiteSpace(),
				x => x.LeaveTypeName.Contains(filterText!) ||
				     x.Color.Contains(filterText!))
			.WhereIf(isActive.HasValue, x => x.IsActive == isActive!.Value);
	}
}
