using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Linq.Expressions;
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
		CancellationToken cancellationToken = default,
		IReadOnlyList<StudentLeaveTableFilter>? tableFilters = null)
	{
		var isSqlite = (await GetDbContextAsync()).Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		var query = ApplyFilter(
			await GetQueryableAsync(),
			filterText,
			studentId,
			educationMediumId,
			standardId,
			divisionId,
			studentLeaveTypeId,
			status,
			isSqlite ? null : startDate,
			isSqlite ? null : endDate,
			studentIds,
			tenantId,
			tableFilters,
			isSqlite);

		return await ApplySortingAndPagingAsync(
			query,
			sorting,
			maxResultCount,
			skipCount,
			startDate,
			endDate,
			cancellationToken,
			tableFilters);
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
		CancellationToken cancellationToken = default,
		IReadOnlyList<StudentLeaveTableFilter>? tableFilters = null)
	{
		var isSqlite = (await GetDbContextAsync()).Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		var query = ApplyFilter(
			await GetQueryableAsync(),
			filterText,
			studentId,
			educationMediumId,
			standardId,
			divisionId,
			studentLeaveTypeId,
			status,
			isSqlite ? null : startDate,
			isSqlite ? null : endDate,
			studentIds,
			tenantId,
			tableFilters,
			isSqlite);

		if (isSqlite && (startDate.HasValue || endDate.HasValue || HasDeferredTableFilters(tableFilters)))
		{
			var dates = await query.Select(x => new { x.LeaveDate, x.EndDate, x.IsHalfDay, x.TotalDays }).ToListAsync(GetCancellationToken(cancellationToken));
			return dates.LongCount(x => (!startDate.HasValue || (x.IsHalfDay ? x.LeaveDate : x.EndDate ?? x.LeaveDate) >= startDate.Value) &&
				(!endDate.HasValue || x.LeaveDate <= endDate.Value) &&
				MatchesDeferredTableFilters(x.LeaveDate, x.EndDate, x.IsHalfDay, x.TotalDays, tableFilters));
		}
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
		CancellationToken cancellationToken = default,
		IReadOnlyList<StudentLeaveTableFilter>? tableFilters = null)
	{
		var isSqlite = (await GetDbContextAsync()).Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		var query = ApplyFilter(
			await GetQueryableAsync(),
			filterText,
			studentId,
			educationMediumId,
			standardId,
			divisionId,
			studentLeaveTypeId,
			status,
			isSqlite ? null : startDate,
			isSqlite ? null : endDate,
			studentIds,
			tenantId,
			tableFilters,
			isSqlite);

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
			startDate,
			endDate,
			cancellationToken,
			tableFilters);
	}

	private async Task<List<StudentLeave>> ApplySortingAndPagingAsync(
		IQueryable<StudentLeave> query,
		string? sorting,
		int maxResultCount,
		int skipCount,
		DateTimeOffset? startDate,
		DateTimeOffset? endDate,
		CancellationToken cancellationToken,
		IReadOnlyList<StudentLeaveTableFilter>? tableFilters)
	{
		var dbContext = await GetDbContextAsync();
		var isSqlite = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

		if (isSqlite)
		{
			return await ApplySqliteSortingAndPagingAsync(query, sorting, maxResultCount, skipCount, startDate, endDate, cancellationToken, tableFilters);
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
		DateTimeOffset? startDate,
		DateTimeOffset? endDate,
		CancellationToken cancellationToken,
		IReadOnlyList<StudentLeaveTableFilter>? tableFilters)
	{
		var rows = await query
			.Select(l => new StudentLeavePageKey(
				l.Id,
				l.LeaveDate,
				l.EndDate,
				l.IsHalfDay,
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

		var pageIds = ApplyInMemorySorting(rows.Where(x =>
				(!startDate.HasValue || (x.IsHalfDay ? x.LeaveDate : x.EndDate ?? x.LeaveDate) >= startDate.Value) &&
				(!endDate.HasValue || x.LeaveDate <= endDate.Value) &&
				MatchesDeferredTableFilters(x.LeaveDate, x.EndDate, x.IsHalfDay, x.TotalDays, tableFilters)), sorting)
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
		Guid? tenantId = null,
		IReadOnlyList<StudentLeaveTableFilter>? tableFilters = null,
		bool isSqlite = false)
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
				(x.Student != null && ((x.Student.FirstName + " " + x.Student.LastName).Contains(filterText) ||
					x.Student.FirstName!.Contains(filterText) || x.Student.LastName!.Contains(filterText))) ||
				(x.StudentLeaveType != null && x.StudentLeaveType.LeaveTypeName.Contains(filterText)) ||
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

		return ApplyTableFilters(query, tableFilters, isSqlite);
	}

	private static IQueryable<StudentLeave> ApplyTableFilters(IQueryable<StudentLeave> query,
		IReadOnlyList<StudentLeaveTableFilter>? filters, bool isSqlite)
	{
		if (filters == null) return query;
		foreach (var filter in filters)
		{
			if (filter == null) continue;
			switch (filter.Column.ToLowerInvariant())
			{
				case "studentname":
					query = ApplyTextFilter(query, x => x.Student == null ? null : (x.Student.FirstName + " " + x.Student.LastName).Trim(), filter);
					break;
				case "organizationname":
					query = ApplyTextFilter(query, x => x.Student == null || x.Student.AcademicMedium == null ? null : x.Student.AcademicMedium.Name, filter);
					break;
				case "classname":
					query = ApplyTextFilter(query, x => x.Student == null || x.Student.Standard == null ? null : x.Student.Standard.Name, filter);
					break;
				case "divisionname":
					query = ApplyTextFilter(query, x => x.Student == null || x.Student.Division == null ? null : x.Student.Division.Name, filter);
					break;
				case "leavetypename":
					query = ApplyTextFilter(query, x => x.StudentLeaveType == null ? null : x.StudentLeaveType.LeaveTypeName, filter);
					break;
				case "status" when int.TryParse(filter.Value, out var statusValue) &&
					Enum.IsDefined(typeof(StudentLeaveStatusEnum), statusValue):
					query = query.Where(x => x.Status == (StudentLeaveStatusEnum)statusValue);
					break;
				case "totaldays" when !isSqlite:
					query = ApplyNumberFilter(query, filter);
					break;
				case "formattedleavedate" when !isSqlite && TryFilterDateRange(filter, out var startDay, out var nextStartDay):
					query = filter.Type switch
					{
						"equals" => query.Where(x => x.LeaveDate >= startDay && x.LeaveDate < nextStartDay),
						"after" => query.Where(x => x.LeaveDate >= nextStartDay),
						"before" => query.Where(x => x.LeaveDate < startDay),
						_ => query
					};
					break;
				case "formattedenddate" when !isSqlite && TryFilterDateRange(filter, out var endDay, out var nextEndDay):
					query = filter.Type switch
					{
						"equals" => query.Where(x => (x.IsHalfDay ? x.LeaveDate : x.EndDate ?? x.LeaveDate) >= endDay &&
							(x.IsHalfDay ? x.LeaveDate : x.EndDate ?? x.LeaveDate) < nextEndDay),
						"after" => query.Where(x => (x.IsHalfDay ? x.LeaveDate : x.EndDate ?? x.LeaveDate) >= nextEndDay),
						"before" => query.Where(x => (x.IsHalfDay ? x.LeaveDate : x.EndDate ?? x.LeaveDate) < endDay),
						_ => query
					};
					break;
			}
		}
		return query;
	}

	private static IQueryable<StudentLeave> ApplyTextFilter(IQueryable<StudentLeave> query,
		Expression<Func<StudentLeave, string?>> selector, StudentLeaveTableFilter filter)
	{
		var value = filter.Value?.Trim();
		if (string.IsNullOrEmpty(value)) return query;
		var text = selector.Body;
		var argument = Expression.Constant(value);
		Expression? comparison = filter.Type switch
		{
			"equals" => Expression.Equal(text, argument),
			"contains" => Expression.Call(text, nameof(string.Contains), Type.EmptyTypes, argument),
			"startsWith" => Expression.Call(text, nameof(string.StartsWith), Type.EmptyTypes, argument),
			"endsWith" => Expression.Call(text, nameof(string.EndsWith), Type.EmptyTypes, argument),
			_ => null
		};
		if (comparison == null) return query;
		var nonNull = Expression.NotEqual(text, Expression.Constant(null, typeof(string)));
		return query.Where(Expression.Lambda<Func<StudentLeave, bool>>(
			Expression.AndAlso(nonNull, comparison), selector.Parameters));
	}

	private static IQueryable<StudentLeave> ApplyNumberFilter(IQueryable<StudentLeave> query,
		StudentLeaveTableFilter filter)
	{
		if (filter.Type == "between" && TryNumber(filter.Min, out var min) && TryNumber(filter.Max, out var max))
			return query.Where(x => x.TotalDays >= min && x.TotalDays <= max);
		if (!TryNumber(filter.Value, out var value)) return query;
		return filter.Type switch
		{
			"equals" => query.Where(x => x.TotalDays == value),
			"greaterThan" => query.Where(x => x.TotalDays > value),
			"lessThan" => query.Where(x => x.TotalDays < value),
			_ => query
		};
	}

	private static bool HasDeferredTableFilters(IReadOnlyList<StudentLeaveTableFilter>? filters) =>
		filters?.Any(filter => filter != null && filter.Column is "totalDays" or "formattedLeaveDate" or "formattedEndDate") == true;

	private static bool MatchesDeferredTableFilters(DateTimeOffset leaveDate, DateTimeOffset? endDate,
		bool isHalfDay, decimal totalDays, IReadOnlyList<StudentLeaveTableFilter>? filters)
	{
		if (filters == null) return true;
		foreach (var filter in filters)
		{
			if (filter == null) continue;
			if (filter.Column == "totalDays" && !MatchesNumber(totalDays, filter)) return false;
			if (filter.Column == "formattedLeaveDate" && !MatchesDate(leaveDate, filter)) return false;
			if (filter.Column == "formattedEndDate" && !MatchesDate(isHalfDay ? leaveDate : endDate ?? leaveDate, filter)) return false;
		}
		return true;
	}

	private static bool MatchesNumber(decimal number, StudentLeaveTableFilter filter)
	{
		if (filter.Type == "between")
			return !TryNumber(filter.Min, out var min) || !TryNumber(filter.Max, out var max) ||
				(number >= min && number <= max);
		if (!TryNumber(filter.Value, out var value)) return true;
		return filter.Type switch
		{
			"equals" => number == value,
			"greaterThan" => number > value,
			"lessThan" => number < value,
			_ => true
		};
	}

	private static bool MatchesDate(DateTimeOffset date, StudentLeaveTableFilter filter)
	{
		if (!TryFilterDateRange(filter, out var start, out var end)) return true;
		return filter.Type switch
		{
			"equals" => date >= start && date < end,
			"after" => date >= end,
			"before" => date < start,
			_ => true
		};
	}

	private static bool TryNumber(string? value, out decimal number) =>
		decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);

	private static bool TryFilterDateRange(StudentLeaveTableFilter filter,
		out DateTimeOffset start, out DateTimeOffset end)
	{
		if (DateTimeOffset.TryParse(filter.Min, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out start) &&
			DateTimeOffset.TryParse(filter.Max, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out end) && end > start)
			return true;
		if (DateTime.TryParseExact(filter.Value, "dd-MM-yyyy", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var day))
		{
			start = new DateTimeOffset(day, TimeSpan.Zero);
			end = start.AddDays(1);
			return true;
		}
		start = default;
		end = default;
		return false;
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
				? query.OrderByDescending(l => l.IsHalfDay ? l.LeaveDate : l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id)
				: query.OrderBy(l => l.IsHalfDay ? l.LeaveDate : l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id),
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
				? rows.OrderByDescending(l => l.IsHalfDay ? l.LeaveDate : l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id)
				: rows.OrderBy(l => l.IsHalfDay ? l.LeaveDate : l.EndDate ?? l.LeaveDate).ThenBy(l => l.Id),
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
		bool IsHalfDay,
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
