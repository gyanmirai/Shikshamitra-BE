using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Application service for employee attendance management.
/// Provides operations for viewing, updating, and importing attendance records.
/// </summary>
public interface IEmployeeAttendanceAppService : IApplicationService
{
	#region Query Operations

	/// <summary>
	/// Gets employees with their attendance for a specific date.
	/// </summary>
	/// <param name="date">The attendance date.</param>
	/// <returns>Paged list of employee attendance records.</returns>
	Task<PagedResultDto<EmployeeAttendanceDto>> GetEmployeesByDate(DateTimeOffset date);

	/// <summary>
	/// Gets attendance summary for multiple employees for a month.
	/// </summary>
	/// <param name="employeeIds">List of employee IDs.</param>
	/// <param name="month">Any date within the target month.</param>
	/// <returns>List of attendance summaries per employee.</returns>
	Task<List<EmployeeAttendanceSummaryDto>> GetEmployeeAttendanceSummary(
		List<Guid> employeeIds,
		DateTimeOffset month);

	/// <summary>
	/// Gets paginated attendance records for a specific date.
	/// </summary>
	/// <param name="input">Pagination and filter parameters.</param>
	/// <returns>Paginated attendance response.</returns>
	Task<PaginatedAttendanceResponse> GetPaginatedByDateAsync(GetPaginatedAttendanceInput input);

	#endregion

	#region Command Operations

	/// <summary>
	/// Updates or creates an attendance record for an employee.
	/// Uses upsert logic based on (EmployeeId, Date).
	/// </summary>
	/// <param name="input">The attendance update request.</param>
	/// <returns>Success response.</returns>
	Task<AttendanceSuccessResponse> UpdateAttendanceAsync(UpdateAttendanceRequest input);

	/// <summary>
	/// Imports attendance records from an Excel or CSV file.
	/// </summary>
	/// <param name="date">The attendance date for imported records.</param>
	/// <param name="fileStream">The file stream.</param>
	/// <param name="fileName">The file name (used to determine format).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Import result with per-row details.</returns>
	Task<AttendanceImportResponse> ImportAttendanceAsync(
		DateTimeOffset date,
		Stream fileStream,
		string fileName,
		CancellationToken cancellationToken = default);

	#endregion
}
