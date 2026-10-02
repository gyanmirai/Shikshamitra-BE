using System;
using System.Collections.Generic;
using System.Linq;
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

	public async Task<List<AttendanceApprovedLeave>> GetApprovedForAttendanceAsync(IReadOnlyCollection<Guid> studentIds,
		DateTimeOffset start, DateTimeOffset endExclusive, Guid? tenantId, CancellationToken cancellationToken = default)
	{
		if (studentIds.Count == 0) return [];
		var ids = studentIds.ToArray();
		var query = (await GetQueryableAsync()).IgnoreAutoIncludes().Where(l => l.TenantId == tenantId &&
			Enumerable.Contains(ids, l.StudentId) && l.Status == StudentLeaveStatusEnum.Approved);
		var dbContext = await GetDbContextAsync();
		var isSqlite = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		if (!isSqlite) query = query.Where(l => l.LeaveDate < endExclusive && (l.EndDate ?? l.LeaveDate) >= start);
		// SQLite cannot compare DateTimeOffset. Read only approved cohort metadata, never attachments or student graphs.
		var rows = await query.Select(l => new AttendanceApprovedLeave(l.Id, l.StudentId, l.IsHalfDay,
			l.LeaveDate, l.EndDate, l.ConcurrencyStamp)).ToListAsync(GetCancellationToken(cancellationToken));
		return isSqlite ? rows.Where(l => l.LeaveDate < endExclusive && (l.EndDate ?? l.LeaveDate) >= start).ToList() : rows;
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
		IReadOnlyCollection<Guid>? studentIds = null,
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
			studentIds,
			tenantId);

		return await ApplySortingAndPagingAsync(
			query,
			sorting,
			maxResultCount,
			skipCount,
			cancellationToken);
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
		IReadOnlyCollection<Guid>? studentIds = null,
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
			studentIds,
			tenantId);

		return await query.LongCountAsync(GetCancellationToken(cancellationToken));
	}

	public async Task<StudentLeave?> GetWithDetailsAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var query = await GetQueryableAsync();

		return await query
			.IgnoreAutoIncludes()
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
		IReadOnlyCollection<Guid>? studentIds = null,
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
			studentIds,
			tenantId);

		query = query
			.IgnoreAutoIncludes()
			.Include(x => x.Student)
				.ThenInclude(x => x!.AcademicMedium)
			.Include(x => x.Student)
				.ThenInclude(x => x!.Standard)
			.Include(x => x.Student)
				.ThenInclude(x => x!.Division)
			.Include(x => x.StudentLeaveType);

		return await ApplySortingAndPagingAsync(
			query,
			sorting,
			maxResultCount,
			skipCount,
			cancellationToken);
	}

	private async Task<List<StudentLeave>> ApplySortingAndPagingAsync(
		IQueryable<StudentLeave> query,
		string? sorting,
		int maxResultCount,
		int skipCount,
		CancellationToken cancellationToken)
	{
		var dbContext = await GetDbContextAsync();
		var isSqlite = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

		if (isSqlite)
		{
			return await ApplySqliteSortingAndPagingAsync(query, sorting, maxResultCount, skipCount, cancellationToken);
		}

		return await ApplySorting(query, sorting)
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToListAsync(GetCancellationToken(cancellationToken));
	}

	private async Task<List<StudentLeave>> ApplySqliteSortingAndPagingAsync(
		IQueryable<StudentLeave> query,
		string? sorting,
		int maxResultCount,
		int skipCount,
		CancellationToken cancellationToken)
	{
		var rows = await query
			.Select(l => new StudentLeavePageKey(
				l.Id,
				l.LeaveDate,
				l.EndDate,
				l.TotalDays,
				l.Status,
				l.Remarks,
				l.Student != null ? l.Student.FirstName : null,
				l.Student != null ? l.Student.LastName : null,
				l.Student != null && l.Student.AcademicMedium != null ? l.Student.AcademicMedium.Name : null,
				l.Student != null && l.Student.Standard != null ? l.Student.Standard.Name : null,
				l.Student != null && l.Student.Division != null ? l.Student.Division.Name : null,
				l.StudentLeaveType != null ? l.StudentLeaveType.LeaveTypeName : null))
			.ToListAsync(GetCancellationToken(cancellationToken));

		var pageIds = ApplyInMemorySorting(rows, sorting)
			.Skip(skipCount)
			.Take(maxResultCount)
			.Select(l => l.Id)
			.ToList();

		if (pageIds.Count == 0)
		{
			return [];
		}

		var leaves = await query
			.Where(l => pageIds.Contains(l.Id))
			.ToListAsync(GetCancellationToken(cancellationToken));
		var leavesById = leaves.ToDictionary(l => l.Id);

		return pageIds
			.Where(leavesById.ContainsKey)
			.Select(id => leavesById[id])
			.ToList();
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
		IReadOnlyCollection<Guid>? studentIds = null,
		Guid? tenantId = null)
	{
		// Apply tenant filter
		if (tenantId.HasValue)
		{
			query = query.Where(x => x.TenantId == tenantId);
		}

		if (studentIds != null)
		{
			query = studentIds.Count == 0
				? query.Where(x => false)
				: query.Where(x => studentIds.Contains(x.StudentId));
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
			query = query.Where(x => (x.EndDate ?? x.LeaveDate) >= startDate.Value);
		}

		if (endDate.HasValue)
		{
			query = query.Where(x => x.LeaveDate <= endDate.Value);
		}

		return query;
	}

	private static IOrderedQueryable<StudentLeave> ApplySorting(IQueryable<StudentLeave> query, string? sorting)
	{
		var (field, descending) = ParseSorting(sorting);

		return field switch
		{
			"studentname" => descending
				? query
					.OrderByDescending(l => l.Student != null ? l.Student.FirstName : null)
					.ThenByDescending(l => l.Student != null ? l.Student.LastName : null)
					.ThenByDescending(l => l.LeaveDate)
					.ThenBy(l => l.Id)
				: query
					.OrderBy(l => l.Student != null ? l.Student.FirstName : null)
					.ThenBy(l => l.Student != null ? l.Student.LastName : null)
					.ThenBy(l => l.LeaveDate)
					.ThenBy(l => l.Id),
			"organizationname" => descending
				? query
					.OrderByDescending(l => l.Student != null && l.Student.AcademicMedium != null ? l.Student.AcademicMedium.Name : null)
					.ThenByDescending(l => l.LeaveDate)
					.ThenBy(l => l.Id)
				: query
					.OrderBy(l => l.Student != null && l.Student.AcademicMedium != null ? l.Student.AcademicMedium.Name : null)
					.ThenBy(l => l.LeaveDate)
					.ThenBy(l => l.Id),
			"classname" => descending
				? query
					.OrderByDescending(l => l.Student != null && l.Student.Standard != null ? l.Student.Standard.Name : null)
					.ThenByDescending(l => l.LeaveDate)
					.ThenBy(l => l.Id)
				: query
					.OrderBy(l => l.Student != null && l.Student.Standard != null ? l.Student.Standard.Name : null)
					.ThenBy(l => l.LeaveDate)
					.ThenBy(l => l.Id),
			"divisionname" => descending
				? query
					.OrderByDescending(l => l.Student != null && l.Student.Division != null ? l.Student.Division.Name : null)
					.ThenByDescending(l => l.LeaveDate)
					.ThenBy(l => l.Id)
				: query
					.OrderBy(l => l.Student != null && l.Student.Division != null ? l.Student.Division.Name : null)
					.ThenBy(l => l.LeaveDate)
					.ThenBy(l => l.Id),
			"leavetypename" => descending
				? query
					.OrderByDescending(l => l.StudentLeaveType != null ? l.StudentLeaveType.LeaveTypeName : null)
					.ThenByDescending(l => l.LeaveDate)
					.ThenBy(l => l.Id)
				: query
					.OrderBy(l => l.StudentLeaveType != null ? l.StudentLeaveType.LeaveTypeName : null)
					.ThenBy(l => l.LeaveDate)
					.ThenBy(l => l.Id),
			"formattedenddate" or "enddate" => descending
				? query.OrderByDescending(l => l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id),
			"totaldays" => descending
				? query.OrderByDescending(l => l.TotalDays).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.TotalDays).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"status" => descending
				? query.OrderByDescending(l => l.Status).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.Status).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"remarks" => descending
				? query.OrderByDescending(l => l.Remarks).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.Remarks).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"formattedleavedate" or "leavedate" => descending
				? query.OrderByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.LeaveDate).ThenBy(l => l.Id),
			_ => descending
				? query.OrderByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.LeaveDate).ThenBy(l => l.Id)
		};
	}

	private static IOrderedEnumerable<StudentLeavePageKey> ApplyInMemorySorting(
		IEnumerable<StudentLeavePageKey> rows,
		string? sorting)
	{
		var (field, descending) = ParseSorting(sorting);

		return field switch
		{
			"studentname" => descending
				? rows.OrderByDescending(l => l.StudentFirstName).ThenByDescending(l => l.StudentLastName).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.StudentFirstName).ThenBy(l => l.StudentLastName).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"organizationname" => descending
				? rows.OrderByDescending(l => l.OrganizationName).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.OrganizationName).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"classname" => descending
				? rows.OrderByDescending(l => l.ClassName).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.ClassName).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"divisionname" => descending
				? rows.OrderByDescending(l => l.DivisionName).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.DivisionName).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"leavetypename" => descending
				? rows.OrderByDescending(l => l.LeaveTypeName).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.LeaveTypeName).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"formattedenddate" or "enddate" => descending
				? rows.OrderByDescending(l => l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id),
			"totaldays" => descending
				? rows.OrderByDescending(l => l.TotalDays).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.TotalDays).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"status" => descending
				? rows.OrderByDescending(l => l.Status).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.Status).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"remarks" => descending
				? rows.OrderByDescending(l => l.Remarks).ThenByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.Remarks).ThenBy(l => l.LeaveDate).ThenBy(l => l.Id),
			"formattedleavedate" or "leavedate" => descending
				? rows.OrderByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.LeaveDate).ThenBy(l => l.Id),
			_ => descending
				? rows.OrderByDescending(l => l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.LeaveDate).ThenBy(l => l.Id)
		};
	}

	private static (string Field, bool Descending) ParseSorting(string? sorting)
	{
		var token = (sorting ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.FirstOrDefault();
		var parts = token?
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			?? [];
		var field = parts.FirstOrDefault()?.ToLowerInvariant();
		var descending = parts.Length < 2 || parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

		return (field ?? "leavedate", descending);
	}

	private sealed record StudentLeavePageKey(
		Guid Id,
		DateTimeOffset LeaveDate,
		DateTimeOffset? EndDate,
		decimal TotalDays,
		StudentLeaveStatusEnum Status,
		string? Remarks,
		string? StudentFirstName,
		string? StudentLastName,
		string? OrganizationName,
		string? ClassName,
		string? DivisionName,
		string? LeaveTypeName);
}
