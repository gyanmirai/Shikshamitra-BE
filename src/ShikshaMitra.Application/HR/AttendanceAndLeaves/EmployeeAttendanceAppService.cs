using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves.EmployeeAttendanceContract;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Employee Attendance Application Service
/// ✅ ALL 8 CRITICAL BUGS FIXED
/// </summary>
public class EmployeeAttendanceAppService : CrudAppService<EmployeeAttendance, EmployeeAttendanceDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeAttendanceDto>, IEmployeeAttendanceAppService, IEmployeeAttendanceContractAppService
{
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly ILogger<EmployeeAttendanceAppService> _logger;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeeAttendanceAppService(
		IEmployeeAttendanceRepository employeeAttendanceRepository,
		IRepository<Employee, Guid> employeeRepository,
		ILogger<EmployeeAttendanceAppService> logger,
		IGuidGenerator guidGenerator)
		: base(employeeAttendanceRepository)
	{
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_employeeRepository = employeeRepository;
		_logger = logger;
		_guidGenerator = guidGenerator;
	}

	public override async Task<EmployeeAttendanceDto> CreateAsync(CreateUpdateEmployeeAttendanceDto input)
	{
		await ValidateAndNormalizeEducationMediumStageIdAsync(input);
		return await base.CreateAsync(input);
	}

	public override async Task<EmployeeAttendanceDto> UpdateAsync(Guid id, CreateUpdateEmployeeAttendanceDto input)
	{
		await ValidateAndNormalizeEducationMediumStageIdAsync(input);
		return await base.UpdateAsync(id, input);
	}

	/// <summary>
	/// Validates and normalizes the EducationMediumStageId to ensure consistency with the Employee.
	/// If EducationMediumStageId is not provided, it will be auto-populated from the Employee.
	/// If provided, it must match the Employee's EducationMediumStageId.
	/// </summary>
	private async Task ValidateAndNormalizeEducationMediumStageIdAsync(CreateUpdateEmployeeAttendanceDto input)
	{
		// Get the employee to validate against
		var employee = await _employeeRepository.FindAsync(input.EmployeeId);
		
		if (employee == null)
		{
			var correlationId = Guid.NewGuid();
			_logger.LogWarning(
				"Employee not found for attendance record. EmployeeId: {EmployeeId}, CorrelationId: {CorrelationId}",
				input.EmployeeId, correlationId);
			
			throw new UserFriendlyException(
				$"Employee with ID '{input.EmployeeId}' does not exist. Please verify the employee ID. (Ref: {correlationId})",
				code: "EmployeeAttendance:EmployeeNotFound"
			);
		}

		// Auto-populate EducationMediumStageId if not provided
		if (!input.EducationMediumStageId.HasValue)
		{
			input.EducationMediumStageId = employee.EducationMediumStageId;
			_logger.LogDebug(
				"Auto-populated EducationMediumStageId for attendance. EmployeeId: {EmployeeId}, EducationMediumStageId: {EducationMediumStageId}",
				input.EmployeeId, input.EducationMediumStageId);
			return;
		}

		// Validate that the provided EducationMediumStageId matches the Employee's value
		if (input.EducationMediumStageId.Value != employee.EducationMediumStageId)
		{
			var correlationId = Guid.NewGuid();
			_logger.LogWarning(
				"EducationMediumStageId mismatch detected. EmployeeId: {EmployeeId}, " +
				"Expected: {ExpectedStageId}, Provided: {ProvidedStageId}, CorrelationId: {CorrelationId}",
				input.EmployeeId, employee.EducationMediumStageId, input.EducationMediumStageId, correlationId);
			
			throw new BusinessException(
				code: "EmployeeAttendance:EducationMediumStageMismatch",
				message: $"The EducationMediumStageId provided ({input.EducationMediumStageId}) does not match " +
				         $"the employee's EducationMediumStageId ({employee.EducationMediumStageId}). " +
				         $"Attendance records must use the same EducationMediumStageId as the employee for proper scope filtering. " +
				         $"(Ref: {correlationId})"
			);
		}
	}
	
	/// <summary>
	/// Get employees by attendance date
	/// ✅ FIX #5: Use AttendanceDate instead of CreationTime
	/// </summary>
	public async Task<PagedResultDto<EmployeeAttendanceDto>> GetEmployeesByDate(DateTimeOffset date)
	{
		// Fix Bug #5: Use AttendanceDate instead of CreationTime for accurate filtering
		var targetDate = DateOnly.FromDateTime(date.Date);
		List<EmployeeAttendance> employees =
			await _employeeAttendanceRepository.GetListAsync(e => e.AttendanceDate == targetDate,
				includeDetails: true);

		return new PagedResultDto<EmployeeAttendanceDto>
		{
			TotalCount = employees.Count,
			Items = ObjectMapper.Map<List<EmployeeAttendance>, List<EmployeeAttendanceDto>>(employees)
		};
	}

	/// <summary>
	/// Get employee attendance summary for a month
	/// ✅ FIX #6: Use AttendanceDate instead of TimeIn
	/// </summary>
	public async Task<List<EmployeeAttendanceSummaryDto>> GetEmployeeAttendanceSummary(List<Guid> employeeIds,
		DateTimeOffset month)
	{
		// Fix Bug #6: Use AttendanceDate instead of TimeIn for accurate monthly filtering
		// TimeIn can be null for leave days, causing records to be missed
		var startOfMonth = DateOnly.FromDateTime(new DateTime(month.Year, month.Month, 1));
		var endOfMonth = DateOnly.FromDateTime(new DateTime(month.Year, month.Month, 1).AddMonths(1).AddDays(-1));

		// Retrieve attendance records within the given time range
		List<EmployeeAttendance> employeeAttendances = await _employeeAttendanceRepository.GetListAsync(
			e => employeeIds.Contains(e.EmployeeId) && e.AttendanceDate >= startOfMonth && e.AttendanceDate <= endOfMonth,
			includeDetails: true);

		var summary = new List<EmployeeAttendanceSummaryDto>();

		foreach (var employeeId in employeeIds)
		{
			var employeeAttendance = employeeAttendances.Where(e => e.EmployeeId == employeeId).ToList();
			var totalWorkingDays = employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.Present);
			var totalFullDayLeaves =
				employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.FullDayLeave);
			var totalHalfDayLeaves = employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);

			var employee = employeeAttendance.Find(e => e.EmployeeId == employeeId)?.Employee;
			summary.Add(new EmployeeAttendanceSummaryDto
			{
				EmployeeId = employeeId,
				EmployeeName = employee?.EmployeeName ?? string.Empty,
				TotalWorkingDays = totalWorkingDays + totalFullDayLeaves + totalHalfDayLeaves,
				PresentDays = totalWorkingDays,
				LeaveDays = totalFullDayLeaves,
				HalfDayLeaves = totalHalfDayLeaves,
				EmployeeAttendances = new PagedResultDto<EmployeeAttendanceDto>
				{
					TotalCount = employeeAttendance.Count,
					Items = ObjectMapper.Map<List<EmployeeAttendance>, List<EmployeeAttendanceDto>>(
						employeeAttendance)
				}
			});
		}

		return summary;
	}

	#region Contract Methods (IEmployeeAttendanceContractAppService)

	/// <summary>
	/// Get paginated employee attendance by date (GET /attendance/employees)
	/// </summary>
	public async Task<PaginatedAttendanceResponse> GetPaginatedByDateAsync(GetPaginatedAttendanceInput input)
	{
		var targetDate = DateOnly.FromDateTime(input.Date.Date);

		var attendanceQuery = await _employeeAttendanceRepository.GetQueryableAsync();
		var employeeQuery = await _employeeRepository.GetQueryableAsync();

		// Filter by tenant
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		// Join attendance with employee data
		var query = from a in attendanceQuery
					where a.AttendanceDate == targetDate
					join e in employeeQuery on a.EmployeeId equals e.Id
					select new
					{
						a.EmployeeId,
						a.TimeIn,
						a.TimeOut,
						a.StatusType,
						Employee = e
					};

		var totalCount = await AsyncExecuter.CountAsync(query);

		// Apply pagination (0-based page index)
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.Employee.FirstName)
				.ThenBy(x => x.Employee.LastName)
				.Skip(input.Page * input.PageSize)
				.Take(input.PageSize)
		);

		return new PaginatedAttendanceResponse
		{
			Date = input.Date,
			Page = input.Page,
			PageSize = input.PageSize,
			TotalRecords = totalCount,
			TotalPages = (int)Math.Ceiling((double)totalCount / input.PageSize),
			Items = items.Select(a => new AttendanceItem
			{
				EmployeeName = FormatEmployeeName(a.Employee.FirstName, a.Employee.MiddleName, a.Employee.LastName),
				EmployeeId = a.EmployeeId,
				EmployeeIdentifier = a.Employee.EmployeeCode ?? string.Empty,
				PhoneNo = a.Employee.MobileNo,
				InTime = a.TimeIn,
				OutTime = a.TimeOut,
				HoursWorked = FormatHoursWorked(a.TimeIn, a.TimeOut),
				Status = MapToContractStatus(a.StatusType)
			}).ToList()
		};
	}

	/// <summary>
	/// Update employee attendance (PUT /attendance/employees)
	/// Upserts by (EmployeeId, AttendanceDate)
	/// ✅ FIX #2 & #3: Support optional OutTime and update EducationMediumStageId
	/// </summary>
	public async Task<SuccessMessage> UpdateAttendanceAsync(UpdateAttendanceRequest input)
	{
		// Validate employee exists
		var employee = await _employeeRepository.FindAsync(input.EmployeeId);
		if (employee == null)
		{
			throw new UserFriendlyException($"Employee with ID '{input.EmployeeId}' not found.");
		}

		var targetDate = DateOnly.FromDateTime(input.Date.Date);

		// Try to find existing attendance record
		var query = await _employeeAttendanceRepository.GetQueryableAsync();
		
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var existingAttendance = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(a => a.EmployeeId == input.EmployeeId && a.AttendanceDate == targetDate)
		);

		// Determine status based on times
		var status = DetermineAttendanceStatus(input.InTime, input.OutTime);

		if (existingAttendance != null)
		{
			// Update existing record
			existingAttendance.TimeIn = input.InTime;
			existingAttendance.TimeOut = input.OutTime;
			existingAttendance.StatusType = status;
			// Fix Bug #2: Update EducationMediumStageId to match employee's current organization
			existingAttendance.EducationMediumStageId = employee.EducationMediumStageId;
			await _employeeAttendanceRepository.UpdateAsync(existingAttendance);
		}
		else
		{
			// Create new record
			var newAttendance = new EmployeeAttendance
			{
				TenantId = CurrentTenant.Id,
				EmployeeId = input.EmployeeId,
				AttendanceDate = targetDate,
				TimeIn = input.InTime,
				TimeOut = input.OutTime,
				StatusType = status,
				EducationMediumStageId = employee.EducationMediumStageId
			};
			await _employeeAttendanceRepository.InsertAsync(newAttendance);
		}

		return new SuccessMessage { Message = "Attendance updated successfully" };
	}

	/// <summary>
	/// Import attendance from Excel file (POST /attendance/import)
	/// Expected columns: EmployeeIdentifier, InTime, OutTime
	/// ✅ FIX #8: Track and warn about duplicate employees in same file
	/// </summary>
	public async Task<AttendanceImportResponse> ImportAttendanceAsync(DateTimeOffset date, Stream fileStream, string fileName)
	{
		if (fileStream == null || fileStream.Length == 0)
		{
			throw new UserFriendlyException("Please upload a valid Excel file.");
		}

		var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
		if (fileExtension != ".xlsx")
		{
			throw new UserFriendlyException("Only .xlsx files are supported.");
		}

		var targetDate = DateOnly.FromDateTime(date.Date);
		var errors = new List<ImportError>();
		var successCount = 0;
		var totalRecords = 0;

		// Copy stream to memory stream for processing
		using var stream = new MemoryStream();
		await fileStream.CopyToAsync(stream);
		stream.Position = 0;

		using var workbook = new XLWorkbook(stream);
		var worksheet = workbook.Worksheets.FirstOrDefault();
		
		if (worksheet == null)
		{
			throw new UserFriendlyException("Excel file contains no worksheets.");
		}

		// Find header row and column indices
		var headerRow = worksheet.Row(1);
		int? identifierCol = null;
		int? inTimeCol = null;
		int? outTimeCol = null;

		foreach (var cell in headerRow.CellsUsed())
		{
			var headerText = cell.Value.ToString().Trim().ToLowerInvariant();
			switch (headerText)
			{
				case "employeeidentifier":
				case "employee identifier":
				case "employee_identifier":
				case "emp_id":
				case "empid":
					identifierCol = cell.Address.ColumnNumber;
					break;
				case "intime":
				case "in_time":
				case "in time":
				case "timein":
				case "time_in":
					inTimeCol = cell.Address.ColumnNumber;
					break;
				case "outtime":
				case "out_time":
				case "out time":
				case "timeout":
				case "time_out":
					outTimeCol = cell.Address.ColumnNumber;
					break;
			}
		}

		if (!identifierCol.HasValue || !inTimeCol.HasValue || !outTimeCol.HasValue)
		{
			throw new UserFriendlyException(
				"Excel file must contain columns: EmployeeIdentifier, InTime, OutTime");
		}

		// Pre-load all employees for batch lookup
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery);
		var employeeByCode = employees.ToDictionary(
			e => e.EmployeeCode?.ToUpperInvariant() ?? string.Empty,
			e => e);

		// Pre-load existing attendance records for the date
		var attendanceQuery = await _employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}
		var existingAttendances = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a => a.AttendanceDate == targetDate)
		);
		var attendanceByEmployee = existingAttendances.ToDictionary(a => a.EmployeeId);

		// Process data rows
		var dataRows = worksheet.RowsUsed().Skip(1); // Skip header
		var attendancesToInsert = new List<EmployeeAttendance>();
		var attendancesToUpdate = new List<EmployeeAttendance>();
		// Fix Bug #8: Track duplicate employee IDs within the Excel file
		var processedEmployeeIds = new HashSet<Guid>();
		var duplicateWarnings = new List<string>();

		foreach (var row in dataRows)
		{
			totalRecords++;
			var rowNumber = row.RowNumber();

			try
			{
				var identifier = row.Cell(identifierCol.Value).Value.ToString().Trim().ToUpperInvariant();
				var inTimeValue = row.Cell(inTimeCol.Value).Value;
				var outTimeValue = row.Cell(outTimeCol.Value).Value;

				// Validate employee exists
				if (string.IsNullOrWhiteSpace(identifier) || !employeeByCode.TryGetValue(identifier, out var employee))
				{
					errors.Add(new ImportError
					{
						RowNumber = rowNumber,
						Reason = $"Employee with identifier '{identifier}' not found"
					});
					continue;
				}

				// Fix Bug #8: Check for duplicate employee in the same file
				if (processedEmployeeIds.Contains(employee.Id))
				{
					duplicateWarnings.Add($"Row {rowNumber}: Duplicate entry for employee '{identifier}' - only the last entry will be used");
				}

				// Parse times
				DateTimeOffset? inTime = ParseTimeValue(inTimeValue, date);
				DateTimeOffset? outTime = ParseTimeValue(outTimeValue, date);

				if (!inTime.HasValue)
				{
					errors.Add(new ImportError
					{
						RowNumber = rowNumber,
						Reason = "Invalid InTime value"
					});
					continue;
				}

				// Determine status
				var status = DetermineAttendanceStatus(inTime, outTime);

				// Upsert
				if (attendanceByEmployee.TryGetValue(employee.Id, out var existing))
				{
					// Update existing record (from DB or earlier in the file)
					existing.TimeIn = inTime;
					existing.TimeOut = outTime;
					existing.StatusType = status;
					existing.EducationMediumStageId = employee.EducationMediumStageId;
					
					// Only add to update list if not already there
					if (!attendancesToUpdate.Contains(existing))
					{
						attendancesToUpdate.Add(existing);
					}
				}
				else
				{
					var newAttendance = new EmployeeAttendance
					{
						TenantId = CurrentTenant.Id,
						EmployeeId = employee.Id,
						AttendanceDate = targetDate,
						TimeIn = inTime,
						TimeOut = outTime,
						StatusType = status,
						EducationMediumStageId = employee.EducationMediumStageId
					};
					attendancesToInsert.Add(newAttendance);
					attendanceByEmployee[employee.Id] = newAttendance; // Track for duplicates in same file
				}

				// Mark employee as processed
				processedEmployeeIds.Add(employee.Id);
				successCount++;
			}
			catch (Exception ex)
			{
				errors.Add(new ImportError
				{
					RowNumber = rowNumber,
					Reason = ex.Message
				});
			}
		}

		// Batch save
		if (attendancesToInsert.Any())
		{
			await _employeeAttendanceRepository.InsertManyAsync(attendancesToInsert);
		}
		if (attendancesToUpdate.Any())
		{
			await _employeeAttendanceRepository.UpdateManyAsync(attendancesToUpdate);
		}

		// Fix Bug #8 Part 3: Add duplicate warnings to errors list
		if (duplicateWarnings.Any())
		{
			foreach (var warning in duplicateWarnings)
			{
				errors.Add(new ImportError
				{
					RowNumber = 0,
					Reason = $"WARNING: {warning}"
				});
			}
		}

		return new AttendanceImportResponse
		{
			Date = date,
			TotalRecords = totalRecords,
			SuccessfulRecords = successCount,
			FailedRecords = errors.Count(e => !e.Reason.StartsWith("WARNING")),
			Errors = errors.Any() ? errors : null
		};
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Formats employee name, handling null/empty middle names to avoid double spaces
	/// </summary>
	private static string FormatEmployeeName(string? firstName, string? middleName, string? lastName)
	{
		var parts = new[] { firstName, middleName, lastName }
			.Where(s => !string.IsNullOrWhiteSpace(s));
		return string.Join(" ", parts);
	}

	/// <summary>
	/// Format hours worked with validation
	/// ✅ FIX #7: Add negative duration validation
	/// </summary>
	private static string? FormatHoursWorked(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue || !timeOut.HasValue)
			return null;

		var duration = timeOut.Value - timeIn.Value;
		
		// Fix Bug #7: Validate duration is not negative
		if (duration.TotalSeconds < 0)
			return "Invalid (Out < In)";

		var hours = (int)duration.TotalHours;
		var minutes = duration.Minutes;
		return $"{hours:D2}h {minutes:D2}m";
	}

	private static AttendanceStatus MapToContractStatus(EmployeeAttendanceStatusType statusType)
	{
		return statusType switch
		{
			EmployeeAttendanceStatusType.Present => AttendanceStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => AttendanceStatus.Halfday,
			EmployeeAttendanceStatusType.FullDayLeave => AttendanceStatus.Leave,
			_ => AttendanceStatus.Pending
		};
	}

	/// <summary>
	/// Determine attendance status based on time
	/// ✅ FIX #4: Improved logic with better edge case handling
	/// </summary>
	private static EmployeeAttendanceStatusType DetermineAttendanceStatus(DateTimeOffset? inTime, DateTimeOffset? outTime)
	{
		// Fix Bug #4: Improved status determination logic
		// If no InTime, treat as absent/leave (not Present)
		if (!inTime.HasValue)
			return EmployeeAttendanceStatusType.FullDayLeave;

		// If both InTime and OutTime are present, calculate hours worked
		if (inTime.HasValue && outTime.HasValue)
		{
			var hoursWorked = (outTime.Value - inTime.Value).TotalHours;
			
			// Handle invalid negative duration
			if (hoursWorked < 0)
				return EmployeeAttendanceStatusType.Present; // Default to Present for invalid data
			
			// Less than 4 hours = Half day leave
			if (hoursWorked < 4)
				return EmployeeAttendanceStatusType.HalfDayLeave;
			
			// 4-8 hours = Present (partial or full day)
			if (hoursWorked < 8)
				return EmployeeAttendanceStatusType.Present;
			
			// 8+ hours = Full day present
			return EmployeeAttendanceStatusType.Present;
		}

		// If only InTime is present (no OutTime yet), mark as Present (ongoing)
		// This allows tracking of employees who checked in but haven't checked out
		return EmployeeAttendanceStatusType.Present;
	}

	private static DateTimeOffset? ParseTimeValue(object? value, DateTimeOffset baseDate)
	{
		if (value == null)
			return null;

		var stringValue = value.ToString()?.Trim();
		if (string.IsNullOrEmpty(stringValue))
			return null;

		// Try parsing as DateTime directly (Excel DateTime)
		if (value is DateTime dt)
		{
			return new DateTimeOffset(
				baseDate.Year, baseDate.Month, baseDate.Day,
				dt.Hour, dt.Minute, dt.Second, baseDate.Offset);
		}

		// Try parsing as time string (HH:mm or HH:mm:ss)
		if (TimeSpan.TryParse(stringValue, out var time))
		{
			return new DateTimeOffset(
				baseDate.Year, baseDate.Month, baseDate.Day,
				time.Hours, time.Minutes, time.Seconds, baseDate.Offset);
		}

		// Try parsing as full DateTime string
		if (DateTime.TryParse(stringValue, out var parsed))
		{
			return new DateTimeOffset(
				baseDate.Year, baseDate.Month, baseDate.Day,
				parsed.Hour, parsed.Minute, parsed.Second, baseDate.Offset);
		}

		return null;
	}

	#endregion
}
