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

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// EF Core repository implementation for StudentLeave entity
/// </summary>
public class EfCoreStudentLeaveRepository : EfCoreRepository<ShikshaMitraDbContext, StudentLeave, Guid>, IStudentLeaveRepository
{
	public EfCoreStudentLeaveRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<List<StudentLeave>> GetListAsync(
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		string? sorting = null,
		int maxResultCount = int.MaxValue,
		int skipCount = 0,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var query = ApplyFilter(
			await GetQueryableAsync(),
			filterText,
			studentId,
			educationMediumId,
			standardId,
			divisionId,
			studentLeaveTypeId,
			status,
			startDate,
			endDate,
			tenantId);

		query = query.OrderBy(sorting ?? nameof(StudentLeave.LeaveDate) + " DESC");

		return await query
			.PageBy(skipCount, maxResultCount)
			.ToListAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<long> GetCountAsync(
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var query = ApplyFilter(
			await GetQueryableAsync(),
			filterText,
			studentId,
			educationMediumId,
			standardId,
			divisionId,
			studentLeaveTypeId,
			status,
			startDate,
			endDate,
			tenantId);

		return await query.LongCountAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<StudentLeave?> GetWithDetailsAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var query = await GetQueryableAsync();

		return await query
			.Include(x => x.Student)
				.ThenInclude(x => x!.AcademicMedium)
			.Include(x => x.Student)
				.ThenInclude(x => x!.Standard)
			.Include(x => x.Student)
				.ThenInclude(x => x!.Division)
			.Include(x => x.StudentLeaveType)
			.Where(x => x.Id == id)
			.FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<List<StudentLeave>> GetListWithDetailsAsync(
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		string? sorting = null,
		int maxResultCount = int.MaxValue,
		int skipCount = 0,
		Guid? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var query = ApplyFilter(
			await GetQueryableAsync(),
			filterText,
			studentId,
			educationMediumId,
			standardId,
			divisionId,
			studentLeaveTypeId,
			status,
			startDate,
			endDate,
			tenantId);

		query = query
			.Include(x => x.Student)
				.ThenInclude(x => x!.AcademicMedium)
			.Include(x => x.Student)
				.ThenInclude(x => x!.Standard)
			.Include(x => x.Student)
				.ThenInclude(x => x!.Division)
			.Include(x => x.StudentLeaveType);

		query = query.OrderBy(sorting ?? nameof(StudentLeave.LeaveDate) + " DESC");

		return await query
			.PageBy(skipCount, maxResultCount)
			.ToListAsync(GetCancellationToken(cancellationToken));
	}

	private IQueryable<StudentLeave> ApplyFilter(
		IQueryable<StudentLeave> query,
		string? filterText = null,
		Guid? studentId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		Guid? studentLeaveTypeId = null,
		StudentLeaveStatusEnum? status = null,
		DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null,
		Guid? tenantId = null)
	{
		// Apply tenant filter
		if (tenantId.HasValue)
		{
			query = query.Where(x => x.TenantId == tenantId);
		}

		// Apply text search filter (search in description, remarks, rejection reason)
		if (!string.IsNullOrWhiteSpace(filterText))
		{
			query = query.Where(x =>
				(x.Description != null && x.Description.Contains(filterText)) ||
				(x.Remarks != null && x.Remarks.Contains(filterText)) ||
				(x.RejectionReason != null && x.RejectionReason.Contains(filterText)));
		}

		// Apply specific filters
		if (studentId.HasValue)
		{
			query = query.Where(x => x.StudentId == studentId.Value);
		}

		if (educationMediumId.HasValue)
		{
			query = query.Where(x => x.EducationMediumId == educationMediumId.Value);
		}

		if (standardId.HasValue)
		{
			query = query.Where(x => x.StandardId == standardId.Value);
		}

		if (divisionId.HasValue)
		{
			query = query.Where(x => x.DivisionId == divisionId.Value);
		}

		if (studentLeaveTypeId.HasValue)
		{
			query = query.Where(x => x.StudentLeaveTypeId == studentLeaveTypeId.Value);
		}

		if (status.HasValue)
		{
			query = query.Where(x => x.Status == status.Value);
		}

		// Apply date range filter
		if (startDate.HasValue)
		{
			query = query.Where(x => x.LeaveDate >= startDate.Value);
		}

		if (endDate.HasValue)
		{
			query = query.Where(x => x.EndDate == null || x.EndDate <= endDate.Value);
		}

		return query;
	}
}
