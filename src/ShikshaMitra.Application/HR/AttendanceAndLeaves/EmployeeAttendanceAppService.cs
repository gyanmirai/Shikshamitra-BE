using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves.EmployeeAttendanceContract;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Volo.Abp.Auditing;
using Volo.Abp.Uow;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Employee Attendance Application Service
/// </summary>
public class EmployeeAttendanceAppService : CrudAppService<EmployeeAttendance, EmployeeAttendanceDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeAttendanceDto>, IEmployeeAttendanceAppService, IEmployeeAttendanceContractAppService
{
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IUserScopeService _userScopeService;
	private readonly ILogger<EmployeeAttendanceAppService> _logger;
	private readonly IGuidGenerator _guidGenerator;
	private readonly Import.AttendanceFileParser _fileParser;
	private readonly Import.AttendanceImportValidator _validator;

	public EmployeeAttendanceAppService(
		IEmployeeAttendanceRepository employeeAttendanceRepository,
		IEmployeeRepository employeeRepository,
		IUserScopeService userScopeService,
		ILogger<EmployeeAttendanceAppService> logger,
		IGuidGenerator guidGenerator,
		Import.AttendanceFileParser fileParser,
		Import.AttendanceImportValidator validator)
		: base(employeeAttendanceRepository)
	{
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_employeeRepository = employeeRepository;
		_userScopeService = userScopeService;
		_logger = logger;
		_guidGenerator = guidGenerator;
		_fileParser = fileParser;
		_validator = validator;
	}

	/// <summary>
	/// Get employees by attendance date
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
	/// </summary>
	public async Task<SuccessMessage> UpdateAttendanceAsync(UpdateAttendanceRequest input)
	{
		// Validate employee exists
		var employee = await _employeeRepository.FindAsync(input.EmployeeId);
		if (employee == null)
		{
			throw new UserFriendlyException($"Employee with ID '{input.EmployeeId}' not found.");
		}

		// Get EducationMediumStageId from employee's UserScope
		Guid? mediumStageId = null;
		if (employee.UserId.HasValue)
		{
			mediumStageId = await GetEducationMediumStageIdFromUserScopeAsync(employee.UserId.Value);
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
			existingAttendance.EducationMediumStageId = mediumStageId;
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
				EducationMediumStageId = mediumStageId
			};
			await _employeeAttendanceRepository.InsertAsync(newAttendance);
		}

		return new SuccessMessage { Message = "Attendance updated successfully" };
	}

	/// <summary>
	/// Import attendance from Excel/CSV file (POST /attendance/import)
	/// Expected columns: Employee Name, Hours Worked, Time In, Time Out, status, Employee Code, Contact No
	/// </summary>
	[UnitOfWork]
	[Audited]
	public async Task<AttendanceImportResponse> ImportAttendanceAsync(
		DateTimeOffset date,
		Stream fileStream,
		string fileName,
		CancellationToken cancellationToken = default)
	{
		Check.NotNull(fileStream, nameof(fileStream));
		Check.NotNullOrWhiteSpace(fileName, nameof(fileName));

		var startTime = Clock.Now;
		var targetDate = DateOnly.FromDateTime(date.Date);

		Logger.LogInformation(
			"Starting attendance import for {Date}, file: {FileName}, size: {FileSize} bytes",
			date, fileName, fileStream.Length);

		try
		{
				// Step 1: Parse file (Excel or CSV) - using injected service
				var parseResult = await _fileParser.ParseFileAsync(fileStream, fileName, cancellationToken);

				if (!parseResult.IsValid)
				{
					throw new UserFriendlyException(
						$"File parsing failed: {string.Join("; ", parseResult.Errors)}");
				}

				if (!parseResult.Rows.Any())
				{
					throw new UserFriendlyException(
						"No data rows found in the file. Please ensure the file contains attendance data.");
				}

				// Step 2: Pre-load employees for bulk lookup
				var employeeCodes = parseResult.Rows
					.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeCode))
					.Select(r => r.EmployeeCode!)
					.Distinct()
					.ToList();

				var employeeLookup = await _employeeRepository.GetEmployeesByCodesAsync(employeeCodes, cancellationToken);

				// Step 3: Pre-load existing attendance records for the date
				var attendanceQuery = await _employeeAttendanceRepository.GetQueryableAsync();
				if (CurrentTenant.Id.HasValue)
				{
					attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
				}
				var existingAttendances = await AsyncExecuter.ToListAsync(
					attendanceQuery.Where(a => a.AttendanceDate == targetDate),
					cancellationToken
				);
				var attendanceByEmployee = existingAttendances.ToDictionary(a => a.EmployeeId);

				// Step 4: Validate each row - using injected validator
				var validationResults = new List<Import.AttendanceRowValidationResult>();
				var processedEmployeeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var row in parseResult.Rows)
				{
					var validationResult = _validator.ValidateRow(row, date, employeeLookup, processedEmployeeCodes);
					validationResults.Add(validationResult);

					if (validationResult.IsSuccess && !string.IsNullOrWhiteSpace(validationResult.EmployeeCode))
					{
						processedEmployeeCodes.Add(validationResult.EmployeeCode);
					}
				}

				// Step 5: Process successful rows
				var attendancesToInsert = new List<EmployeeAttendance>();
				var attendancesToUpdate = new List<EmployeeAttendance>();

				foreach (var result in validationResults.Where(r => r.IsSuccess))
				{
					var employee = result.Employee!;
					var status = result.ParsedStatus ?? EmployeeAttendanceStatusType.Present;

					// Get EducationMediumStageId from employee's UserScope
					Guid? mediumStageId = null;
					if (employee.UserId.HasValue)
					{
						mediumStageId = await GetEducationMediumStageIdFromUserScopeAsync(employee.UserId.Value);
					}

					// Upsert logic
					if (attendanceByEmployee.TryGetValue(employee.Id, out var existing))
					{
						// Update existing record
						existing.TimeIn = result.ParsedTimeIn;
						existing.TimeOut = result.ParsedTimeOut;
						existing.StatusType = status;
						existing.EducationMediumStageId = mediumStageId;

						if (!attendancesToUpdate.Contains(existing))
						{
							attendancesToUpdate.Add(existing);
						}
					}
					else
					{
						// Create new record
						var newAttendance = new EmployeeAttendance
						{
							TenantId = CurrentTenant.Id,
							EmployeeId = employee.Id,
							AttendanceDate = targetDate,
							TimeIn = result.ParsedTimeIn,
							TimeOut = result.ParsedTimeOut,
							StatusType = status,
							EducationMediumStageId = mediumStageId
						};
						attendancesToInsert.Add(newAttendance);
						attendanceByEmployee[employee.Id] = newAttendance;
					}
				}

				// Step 6: Batch save to database
				if (attendancesToInsert.Any())
				{
					await _employeeAttendanceRepository.InsertManyAsync(attendancesToInsert, cancellationToken: cancellationToken);
				}
				if (attendancesToUpdate.Any())
				{
					await _employeeAttendanceRepository.UpdateManyAsync(attendancesToUpdate, cancellationToken: cancellationToken);
				}

				// Step 7: Build response with detailed results
				var processingTime = (Clock.Now - startTime).TotalSeconds;
				var successfulRows = validationResults.Count(r => r.IsSuccess);
				var failedRows = validationResults.Count(r => !r.IsSuccess);
				var warningCount = validationResults.Sum(r => r.Messages.Count(m => m.Severity == ValidationSeverity.Warning));

				var summaryMessage = BuildSummaryMessage(parseResult.Rows.Count, successfulRows, failedRows, warningCount);

				var response = new AttendanceImportResponse
				{
					Date = date,
					Summary = new ImportSummary
					{
						TotalRows = parseResult.Rows.Count,
						SuccessfulRows = successfulRows,
						FailedRows = failedRows,
						WarningCount = warningCount,
						ProcessingTimeSeconds = Math.Round(processingTime, 2),
						Message = summaryMessage
					},
					Results = validationResults.Select(r => new ImportRowResult
					{
						RowNumber = r.RowNumber,
						IsSuccess = r.IsSuccess,
						EmployeeCode = r.EmployeeCode,
						EmployeeName = r.EmployeeName,
						Messages = r.Messages
					}).ToList()
				};

				Logger.LogInformation(
					"Completed attendance import. Total: {Total}, Success: {Success}, Failed: {Failed}, Warnings: {Warnings}, Duration: {Duration}ms",
					parseResult.Rows.Count, successfulRows, failedRows, warningCount,
					(Clock.Now - startTime).TotalMilliseconds);

				return response;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Failed to import attendance for {Date}", date);
				throw;
			}
		}

	/// <summary>
	/// Builds a user-friendly summary message for the import operation
	/// </summary>
	private string BuildSummaryMessage(int totalRows, int successfulRows, int failedRows, int warningCount)
	{
		if (failedRows == 0 && warningCount == 0)
		{
			return $"✅ All {totalRows} attendance records imported successfully!";
		}

		if (failedRows == 0 && warningCount > 0)
		{
			return $"✅ All {totalRows} records imported successfully with {warningCount} warning(s). Please review warnings.";
		}

		if (successfulRows == 0)
		{
			return $"❌ Import failed. All {totalRows} records have errors. Please fix the errors and try again.";
		}

		return $"⚠️ Partial success: {successfulRows} of {totalRows} records imported. " +
			   $"{failedRows} failed. Please review errors and re-import failed records.";
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Gets the EducationMediumStageId from the employee's UserScope.
	/// Returns the first MediumStageId from the user's scope, or null if no scope is assigned.
	/// </summary>
	private async Task<Guid?> GetEducationMediumStageIdFromUserScopeAsync(Guid employeeUserId)
	{
		try
		{
			var userScope = await _userScopeService.GetUserScopeAsync(employeeUserId);
			
			// If user is admin or has wildcard access, return null (no specific stage)
			if (userScope.IsAdmin || userScope.HasWildcardMediumStage)
			{
				return null;
			}
			
			// Return the first MediumStageId if available
			return userScope.MediumStageIds.FirstOrDefault();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get UserScope for employee user ID {UserId}", employeeUserId);
			return null;
		}
	}

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
