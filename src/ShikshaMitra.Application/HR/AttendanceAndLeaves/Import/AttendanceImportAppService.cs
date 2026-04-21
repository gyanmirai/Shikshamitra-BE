using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_ImportAttendance.View)]
public class AttendanceImportAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	IDistributedCache<AttendanceImportPreviewCacheItem> previewCache,
	IExcelService excelService)
	: ApplicationService, IAttendanceImportAppService
{
	private static readonly string[] AllowedHeaders =
	[
		"Attendance Date",
		"Employee Code",
		"Time In",
		"Time Out",
		"Status",
		"Remarks",
		"Employee Name",
		"Contact No"
	];

	private const string AttendanceDateHeader = "Attendance Date";
	private const string EmployeeCodeHeader = "Employee Code";
	private const string TimeInHeader = "Time In";
	private const string TimeOutHeader = "Time Out";
	private const string StatusHeader = "Status";
	private const string RemarksHeader = "Remarks";
	private const string EmployeeNameHeader = "Employee Name";
	private const string ContactNoHeader = "Contact No";
	private const long MaxFileSizeBytes = 5 * 1024 * 1024;
	private const int ImportBatchFileNameMaxLength = 255;
	private const int ImportBatchImportedByNameMaxLength = 128;
	private const int ImportBatchPayrollMonthMaxLength = 20;
	private const int RowActionMaxLength = 32;
	private const int RowAttendanceDateMaxLength = 32;
	private const int RowEmployeeCodeMaxLength = 64;
	private const int RowEmployeeNameMaxLength = 256;
	private const int RowContactNoMaxLength = 64;
	private const int RowTimeMaxLength = 32;
	private const int RowStatusMaxLength = 64;
	private const int RowRemarksMaxLength = 512;
	private static readonly TimeZoneInfo DefaultImportTimeZone = TimeZoneInfo.Local;

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_ImportAttendance.Add)]
	public async Task<AttendanceImportPreviewResponseDto> PreviewAsync(string fileName, string? contentType, byte[] fileContent)
	{
		ValidateUpload(fileName, fileContent);

		var rawRows = ParseFile(fileName, fileContent);
		var previewRows = rawRows.Select(MapRawToPreviewRow).ToList();
		EnsureSinglePayrollMonth(previewRows);

		MarkDuplicateRows(previewRows);
		await EnrichRowsAsync(previewRows);

		var validRows = previewRows.Where(x => x.Errors.Count == 0).ToList();
		await ClassifyRowsAsync(validRows);

		foreach (var invalidRow in previewRows.Where(x => x.Errors.Count > 0))
		{
			invalidRow.Action = "invalid";
		}

		string? previewToken = null;
		var previewTokenExpiresAt = default(DateTime?);
		var batchId = GuidGenerator.Create();

		if (validRows.Count > 0)
		{
			previewToken = await CachePreviewAsync(validRows, batchId);
			previewTokenExpiresAt = Clock.Now.AddMinutes(30);
		}

		var monthYear = ResolveInternalPayrollMonthYear(previewRows);
		try
		{
			await CreateImportBatchAsync(
				batchId,
				fileName,
				previewRows,
				monthYear,
				previewToken,
				previewTokenExpiresAt);
		}
		catch
		{
			if (!string.IsNullOrWhiteSpace(previewToken))
			{
				await previewCache.RemoveAsync(GetPreviewCacheKey(previewToken));
			}

			throw;
		}

		return new AttendanceImportPreviewResponseDto
		{
			PreviewToken = previewToken,
			CanConfirm = validRows.Count > 0,
			Summary = new AttendanceImportSummaryDto
			{
				TotalRows = previewRows.Count,
				ValidRows = validRows.Count,
				InvalidRows = previewRows.Count(x => x.Errors.Count > 0),
				CreateCount = previewRows.Count(x => x.Action == "create"),
				ReplaceCount = previewRows.Count(x => x.Action == "replace")
			},
			Rows = previewRows
				.OrderBy(x => x.RowNumber)
				.Select(MapToResultDto)
				.ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_ImportAttendance.Add)]
	public async Task<AttendanceImportConfirmResponseDto> ConfirmAsync(AttendanceImportConfirmRequestDto input)
	{
		var userId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var cacheKey = GetPreviewCacheKey(input.PreviewToken);
		var cached = await previewCache.GetAsync(cacheKey);
		if (cached == null)
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewExpired);

		if (cached.UserId != userId || cached.TenantId != CurrentTenant.Id)
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		if (cached.Rows.Count == 0)
			throw new BusinessException(HRErrorCodes.AttendanceImportNoValidRows);

		var employeeIds = cached.Rows.Select(x => x.EmployeeId).Distinct().ToList();
		var minDate = cached.Rows.Min(x => x.AttendanceDate);
		var maxDate = cached.Rows.Max(x => x.AttendanceDate);

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var existingRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				x.AttendanceDate >= minDate &&
				x.AttendanceDate <= maxDate));

		var existingLookup = existingRows.ToDictionary(
			x => BuildRowKey(x.EmployeeId, x.AttendanceDate),
			x => x);

		var createdCount = 0;
		var replacedCount = 0;

		foreach (var row in cached.Rows)
		{
			var key = BuildRowKey(row.EmployeeId, row.AttendanceDate);
			if (existingLookup.TryGetValue(key, out var existing))
			{
				existing.TimeIn = row.TimeIn;
				existing.TimeOut = row.TimeOut;
				existing.StatusType = row.StatusType;
				existing.EducationMediumStageId = row.EducationMediumStageId;
				existing.Remarks = row.Remarks;
				await employeeAttendanceRepository.UpdateAsync(existing, autoSave: false);
				replacedCount++;
				continue;
			}

			await employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = CurrentTenant.Id,
				EmployeeId = row.EmployeeId,
				AttendanceDate = row.AttendanceDate,
				TimeIn = row.TimeIn,
				TimeOut = row.TimeOut,
				StatusType = row.StatusType,
				EducationMediumStageId = row.EducationMediumStageId,
				Remarks = row.Remarks
			}, autoSave: false);

			createdCount++;
		}

		var importBatch = await attendanceImportBatchRepository.FindAsync(cached.ImportBatchId);
		if (importBatch != null && importBatch.TenantId == CurrentTenant.Id)
		{
			importBatch.MarkConfirmed(
				cached.Rows.Count,
				createdCount,
				replacedCount,
				Clock.Now);
			await attendanceImportBatchRepository.UpdateAsync(importBatch, autoSave: true);
		}
		await previewCache.RemoveAsync(cacheKey);

		return new AttendanceImportConfirmResponseDto
		{
			ProcessedRows = cached.Rows.Count,
			CreatedCount = createdCount,
			ReplacedCount = replacedCount
		};
	}

	public Task<AttendanceImportTemplateDto> GetTemplateAsync()
	{
		var document = new ExcelDocument
		{
			OutputFileName = "employee-attendance-import-template.xlsx",
			Sheets =
			[
				new ExcelSheet
				{
					SheetName = "AttendanceImport",
					Rows =
					[
						CreateExcelRow(AttendanceDateHeader, EmployeeCodeHeader, TimeInHeader, TimeOutHeader, StatusHeader, RemarksHeader, EmployeeNameHeader, ContactNoHeader),
						CreateExcelRow("11-04-2026", "EMP0001", "09:00 AM", "05:30 PM", string.Empty, "Regular shift", "John Doe", "9876543210"),
						CreateExcelRow("11-04-2026", "EMP0002", string.Empty, string.Empty, "FullDayLeave", "Approved leave", "Jane Doe", "9876543211"),
						CreateExcelRow("11-04-2026", "EMP0003", string.Empty, string.Empty, "HalfDayLeave", "Training half day", "Alex Doe", "9876543212")
					]
				}
			]
		};

		return Task.FromResult(new AttendanceImportTemplateDto
		{
			FileName = document.OutputFileName ?? "employee-attendance-import-template.xlsx",
			Content = excelService.GenerateExcelAsBytes(document)
		});
	}

	private static ExcelRow CreateExcelRow(params object?[] values)
	{
		return new ExcelRow
		{
			Cells = values.Select(x => new ExcelCell { Value = x ?? string.Empty }).ToList()
		};
	}

	private static void ValidateUpload(string fileName, byte[] fileContent)
	{
		if (fileContent.Length == 0)
			throw new BusinessException(HRErrorCodes.AttendanceImportFileEmpty);
		if (fileContent.Length > MaxFileSizeBytes)
			throw new BusinessException(HRErrorCodes.AttendanceImportFileTooLarge);

		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (extension is not ".xlsx" and not ".csv")
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidExtension);
	}

	private static List<AttendanceImportRawRow> ParseFile(string fileName, byte[] fileContent)
	{
		try
		{
			return Path.GetExtension(fileName).ToLowerInvariant() switch
			{
				".xlsx" => ParseExcel(fileContent),
				".csv" => ParseCsv(fileContent),
				_ => throw new BusinessException(HRErrorCodes.AttendanceImportInvalidExtension)
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);
		}
	}

	private static List<AttendanceImportRawRow> ParseExcel(byte[] fileContent)
	{
		using var stream = new MemoryStream(fileContent);
		using var workbook = new XLWorkbook(stream);
		var worksheet = workbook.Worksheets.FirstOrDefault()
			?? throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);
		var usedRange = worksheet.RangeUsed()
			?? throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);

		var headerRow = worksheet.Row(1);
		var lastHeaderCell = headerRow.LastCellUsed()
			?? throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);
		var headers = Enumerable.Range(1, lastHeaderCell.Address.ColumnNumber)
			.Select(index => headerRow.Cell(index).GetString())
			.ToList();

		ValidateHeaders(headers);
		var headerIndex = BuildHeaderIndex(headers);
		var result = new List<AttendanceImportRawRow>();

		for (var rowNumber = 2; rowNumber <= usedRange.LastRow().RowNumber(); rowNumber++)
		{
			var row = worksheet.Row(rowNumber);
			var rawRow = new AttendanceImportRawRow
			{
				RowNumber = rowNumber,
				AttendanceDate = GetCellText(row, headerIndex, AttendanceDateHeader),
				EmployeeCode = GetCellText(row, headerIndex, EmployeeCodeHeader),
				TimeIn = GetCellText(row, headerIndex, TimeInHeader),
				TimeOut = GetCellText(row, headerIndex, TimeOutHeader),
				Status = GetCellText(row, headerIndex, StatusHeader),
				Remarks = GetCellText(row, headerIndex, RemarksHeader),
				EmployeeName = GetCellText(row, headerIndex, EmployeeNameHeader),
				ContactNo = GetCellText(row, headerIndex, ContactNoHeader),
				AttendanceDateCell = GetCellValue(row, headerIndex, AttendanceDateHeader),
				TimeInCell = GetCellValue(row, headerIndex, TimeInHeader),
				TimeOutCell = GetCellValue(row, headerIndex, TimeOutHeader)
			};

			if (!rawRow.IsEmpty())
				result.Add(rawRow);
		}

		return result;
	}

	private static List<AttendanceImportRawRow> ParseCsv(byte[] fileContent)
	{
		using var stream = new MemoryStream(fileContent);
		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			IgnoreBlankLines = false,
			TrimOptions = TrimOptions.Trim
		};

		using var csv = new CsvReader(reader, config);
		if (!csv.Read() || !csv.ReadHeader())
			throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);

		var headers = csv.HeaderRecord?.ToList()
			?? throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);
		ValidateHeaders(headers);

		var result = new List<AttendanceImportRawRow>();
		while (csv.Read())
		{
			var rawRow = new AttendanceImportRawRow
			{
				RowNumber = csv.Parser.Row,
				AttendanceDate = csv.GetField(AttendanceDateHeader),
				EmployeeCode = csv.GetField(EmployeeCodeHeader),
				TimeIn = csv.TryGetField(TimeInHeader, out string? timeIn) ? timeIn : null,
				TimeOut = csv.TryGetField(TimeOutHeader, out string? timeOut) ? timeOut : null,
				Status = csv.TryGetField(StatusHeader, out string? status) ? status : null,
				Remarks = csv.TryGetField(RemarksHeader, out string? remarks) ? remarks : null,
				EmployeeName = csv.TryGetField(EmployeeNameHeader, out string? employeeName) ? employeeName : null,
				ContactNo = csv.TryGetField(ContactNoHeader, out string? contactNo) ? contactNo : null
			};

			if (!rawRow.IsEmpty())
				result.Add(rawRow);
		}

		return result;
	}

	private static void ValidateHeaders(IReadOnlyCollection<string> headers)
	{
		var normalizedHeaders = headers.Select(NormalizeHeader).ToList();
		var allowed = AllowedHeaders.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (normalizedHeaders.Any(x => !allowed.Contains(x)))
			throw new BusinessException(HRErrorCodes.AttendanceImportUnknownHeaders);

		if (!normalizedHeaders.Contains(NormalizeHeader(AttendanceDateHeader), StringComparer.OrdinalIgnoreCase) ||
			!normalizedHeaders.Contains(NormalizeHeader(EmployeeCodeHeader), StringComparer.OrdinalIgnoreCase))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMissingHeaders);
		}
	}

	private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
	{
		var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < headers.Count; i++)
		{
			result[NormalizeHeader(headers[i])] = i + 1;
		}

		return result;
	}

	private static string? GetCellText(IXLRow row, IReadOnlyDictionary<string, int> headerIndex, string header)
	{
		if (!headerIndex.TryGetValue(NormalizeHeader(header), out var column))
			return null;

		var value = row.Cell(column).GetFormattedString();
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private static object? GetCellValue(IXLRow row, IReadOnlyDictionary<string, int> headerIndex, string header)
	{
		if (!headerIndex.TryGetValue(NormalizeHeader(header), out var column))
			return null;

		var cell = row.Cell(column);
		if (cell.IsEmpty())
			return null;
		if (cell.TryGetValue<DateTime>(out var dateTimeValue))
			return dateTimeValue;
		if (cell.TryGetValue<TimeSpan>(out var timeSpanValue))
			return timeSpanValue;

		return cell.GetFormattedString();
	}

	private static AttendanceImportPreviewRow MapRawToPreviewRow(AttendanceImportRawRow rawRow)
	{
		var previewRow = new AttendanceImportPreviewRow
		{
			RowNumber = rawRow.RowNumber,
			AttendanceDate = rawRow.AttendanceDate?.Trim() ?? string.Empty,
			EmployeeCode = rawRow.EmployeeCode?.Trim() ?? string.Empty,
			EmployeeName = rawRow.EmployeeName?.Trim(),
			ContactNo = rawRow.ContactNo?.Trim(),
			TimeIn = rawRow.TimeIn?.Trim(),
			TimeOut = rawRow.TimeOut?.Trim(),
			Status = rawRow.Status?.Trim(),
			Remarks = rawRow.Remarks?.Trim()
		};

		if (string.IsNullOrWhiteSpace(previewRow.AttendanceDate))
		{
			previewRow.Errors.Add("Attendance Date is required.");
		}
		else if (TryParseDate(rawRow.AttendanceDateCell ?? rawRow.AttendanceDate!, out var attendanceDate))
		{
			previewRow.AttendanceDateValue = attendanceDate;
			previewRow.AttendanceDate = attendanceDate.ToString("dd-MM-yyyy");
		}
		else
		{
			previewRow.Errors.Add("Attendance Date is invalid. Use dd-MM-yyyy or ISO date format.");
		}

		if (string.IsNullOrWhiteSpace(previewRow.EmployeeCode))
		{
			previewRow.Errors.Add("Employee Code is required.");
		}

		MapTimeAndStatus(rawRow, previewRow);
		ValidateRowColumnLengths(previewRow);
		return previewRow;
	}

	private static void MapTimeAndStatus(AttendanceImportRawRow rawRow, AttendanceImportPreviewRow previewRow)
	{
		var hasTimeIn = TryParseTime(rawRow.TimeInCell ?? rawRow.TimeIn, out var timeIn);
		var hasTimeOut = TryParseTime(rawRow.TimeOutCell ?? rawRow.TimeOut, out var timeOut);
		var hasAnyTime = hasTimeIn || hasTimeOut;
		var hasBothTimes = hasTimeIn && hasTimeOut;

		if (hasAnyTime && !hasBothTimes)
		{
			previewRow.Errors.Add("Time In and Time Out must both be provided for present rows.");
		}

		if (hasBothTimes)
		{
			if (previewRow.AttendanceDateValue.HasValue)
			{
				var timeInOffset = CombineDateAndTime(previewRow.AttendanceDateValue.Value, timeIn);
				var timeOutOffset = CombineDateAndTime(previewRow.AttendanceDateValue.Value, timeOut);
				if (timeOutOffset <= timeInOffset)
				{
					previewRow.Errors.Add("Time Out must be after Time In.");
				}
				else
				{
					previewRow.TimeInValue = timeInOffset;
					previewRow.TimeOutValue = timeOutOffset;
					previewRow.TimeIn = timeIn.ToString(@"hh\:mm");
					previewRow.TimeOut = timeOut.ToString(@"hh\:mm");
					previewRow.StatusValue = EmployeeAttendanceStatusType.Present;
					previewRow.Status = nameof(EmployeeAttendanceStatusType.Present);
				}
			}
		}
		else
		{
			if (string.IsNullOrWhiteSpace(previewRow.Status))
			{
				previewRow.Errors.Add("Status is required when Time In and Time Out are empty.");
			}
			else if (TryParseStatus(previewRow.Status, out var statusValue))
			{
				if (statusValue == EmployeeAttendanceStatusType.Present)
				{
					previewRow.Errors.Add("Present status requires both Time In and Time Out.");
				}
				else
				{
					previewRow.StatusValue = statusValue;
					previewRow.Status = statusValue.ToString();
				}
			}
			else
			{
				previewRow.Errors.Add("Status is invalid. Allowed values: Present, HalfDayLeave, FullDayLeave.");
			}
		}

		if (hasBothTimes && !string.IsNullOrWhiteSpace(rawRow.Status))
		{
			previewRow.Errors.Add("Status must be empty when Time In and Time Out are provided.");
		}
	}

	private static void MarkDuplicateRows(List<AttendanceImportPreviewRow> rows)
	{
		var duplicateGroups = rows
			.Where(x => x.Errors.Count == 0 && x.AttendanceDateValue.HasValue && !string.IsNullOrWhiteSpace(x.EmployeeCode))
			.GroupBy(x => $"{x.EmployeeCode.Trim().ToUpperInvariant()}|{x.AttendanceDateValue:yyyy-MM-dd}")
			.Where(g => g.Count() > 1);

		foreach (var group in duplicateGroups)
		{
			foreach (var row in group)
			{
				row.Errors.Add("Duplicate Employee Code + Attendance Date found in the file.");
			}
		}
	}

	private async Task EnrichRowsAsync(List<AttendanceImportPreviewRow> rows)
	{
		var employeeCodes = rows
			.Where(x => x.Errors.Count == 0 && !string.IsNullOrWhiteSpace(x.EmployeeCode))
			.Select(x => x.EmployeeCode)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var employeesByCode = await employeeRepository.GetEmployeesByCodesAsync(employeeCodes);
		foreach (var row in rows.Where(x => x.Errors.Count == 0))
		{
			if (!employeesByCode.TryGetValue(row.EmployeeCode.ToUpperInvariant(), out var employee))
			{
				row.Errors.Add("Employee Code was not found.");
				continue;
			}

			if (employee.EmployeeStatusType.HasValue && employee.EmployeeStatusType.Value != EmployeeStatusType.Active)
			{
				row.Errors.Add("Employee is not active.");
				continue;
			}

			row.EmployeeId = employee.Id;
			row.EducationMediumStageId = null;

			if (!string.IsNullOrWhiteSpace(row.EmployeeName))
			{
				var normalizedIncoming = NormalizeComparableValue(row.EmployeeName);
				var normalizedStored = NormalizeComparableValue(employee.EmployeeNameWithFather);
				if (!string.Equals(normalizedIncoming, normalizedStored, StringComparison.OrdinalIgnoreCase))
				{
					row.Warnings.Add("Employee Name does not match the current employee record.");
				}
			}
		}
	}

	private async Task ClassifyRowsAsync(List<AttendanceImportPreviewRow> validRows)
	{
		if (validRows.Count == 0)
			return;

		var employeeIds = validRows.Select(x => x.EmployeeId).Distinct().ToList();
		var minDate = validRows.Min(x => x.AttendanceDateValue!.Value);
		var maxDate = validRows.Max(x => x.AttendanceDateValue!.Value);

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var existingKeys = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(x =>
					employeeIds.Contains(x.EmployeeId) &&
					x.AttendanceDate >= minDate &&
					x.AttendanceDate <= maxDate)
				.Select(x => new { x.EmployeeId, x.AttendanceDate }));

		var existingLookup = existingKeys.Select(x => BuildRowKey(x.EmployeeId, x.AttendanceDate)).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var row in validRows)
		{
			row.Action = existingLookup.Contains(BuildRowKey(row.EmployeeId, row.AttendanceDateValue!.Value))
				? "replace"
				: "create";
		}
	}

	private async Task<string> CachePreviewAsync(List<AttendanceImportPreviewRow> validRows, Guid importBatchId)
	{
		var userId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var token = GuidGenerator.Create().ToString("N");

		await previewCache.SetAsync(
			GetPreviewCacheKey(token),
			new AttendanceImportPreviewCacheItem
			{
				Token = token,
				TenantId = CurrentTenant.Id,
				UserId = userId,
				ImportBatchId = importBatchId,
				Rows = validRows.Select(x => new AttendanceImportPreviewCacheRow
				{
					EmployeeId = x.EmployeeId,
					EducationMediumStageId = x.EducationMediumStageId,
					AttendanceDate = x.AttendanceDateValue!.Value,
					TimeIn = x.TimeInValue,
					TimeOut = x.TimeOutValue,
					StatusType = x.StatusValue,
					Remarks = TruncateForStorage(x.Remarks, RowRemarksMaxLength)
				}).ToList()
			},
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
			});

		return token;
	}

	private async Task<AttendanceImportBatch> CreateImportBatchAsync(
		Guid batchId,
		string fileName,
		List<AttendanceImportPreviewRow> previewRows,
		string? monthYear,
		string? previewToken,
		DateTime? previewTokenExpiresAt)
	{
		var importedByUserId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var importedByName = BuildImportedByName();
		var errorsSummary = previewRows
			.SelectMany(x => x.Errors)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(20)
			.ToList();
		var warningsSummary = previewRows
			.SelectMany(x => x.Warnings)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(20)
			.ToList();

		var importBatch = new AttendanceImportBatch(
			batchId,
			CurrentTenant.Id,
			TruncateForStorage(fileName, ImportBatchFileNameMaxLength),
			importedByUserId,
			TruncateForStorage(importedByName, ImportBatchImportedByNameMaxLength),
			Clock.Now,
			monthYear,
			TruncateForStorage(ToApiMonthYear(monthYear), ImportBatchPayrollMonthMaxLength),
			previewRows.Count,
			previewRows.Count(x => x.Errors.Count == 0),
			previewRows.Count(x => x.Errors.Count > 0),
			previewRows.Count(x => x.Action == "create"),
			previewRows.Count(x => x.Action == "replace"),
				errorsSummary,
				warningsSummary);

		if (!string.IsNullOrWhiteSpace(previewToken) && previewTokenExpiresAt.HasValue)
		{
			importBatch.SetPreviewToken(previewToken, previewTokenExpiresAt.Value);
		}

		await attendanceImportBatchRepository.InsertAsync(importBatch, autoSave: true);

		var rowEntities = previewRows.Select(x => new AttendanceImportRow(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			importBatch.Id,
			x.RowNumber,
			TruncateForStorage(x.Action, RowActionMaxLength) ?? "invalid",
			TruncateForStorage(x.AttendanceDate, RowAttendanceDateMaxLength) ?? string.Empty,
			TruncateForStorage(x.EmployeeCode, RowEmployeeCodeMaxLength) ?? string.Empty,
			TruncateForStorage(x.EmployeeName, RowEmployeeNameMaxLength),
			TruncateForStorage(x.ContactNo, RowContactNoMaxLength),
			TruncateForStorage(x.TimeIn, RowTimeMaxLength),
			TruncateForStorage(x.TimeOut, RowTimeMaxLength),
			TruncateForStorage(x.Status, RowStatusMaxLength),
			TruncateForStorage(x.Remarks, RowRemarksMaxLength),
			[.. x.Errors],
			[.. x.Warnings]))
			.ToList();

		if (rowEntities.Count > 0)
		{
			await attendanceImportRowRepository.InsertManyAsync(rowEntities, autoSave: true);
		}

		return importBatch;
	}

	private static void ValidateRowColumnLengths(AttendanceImportPreviewRow row)
	{
		ValidateMaxLength(row.Action, RowActionMaxLength, "Action", row.Errors);
		ValidateMaxLength(row.AttendanceDate, RowAttendanceDateMaxLength, "Attendance Date", row.Errors);
		ValidateMaxLength(row.EmployeeCode, RowEmployeeCodeMaxLength, "Employee Code", row.Errors);
		ValidateMaxLength(row.EmployeeName, RowEmployeeNameMaxLength, "Employee Name", row.Errors);
		ValidateMaxLength(row.ContactNo, RowContactNoMaxLength, "Contact No", row.Errors);
		ValidateMaxLength(row.TimeIn, RowTimeMaxLength, "Time In", row.Errors);
		ValidateMaxLength(row.TimeOut, RowTimeMaxLength, "Time Out", row.Errors);
		ValidateMaxLength(row.Status, RowStatusMaxLength, "Status", row.Errors);
		ValidateMaxLength(row.Remarks, RowRemarksMaxLength, "Remarks", row.Errors);
	}

	private static void ValidateMaxLength(string? value, int maxLength, string fieldName, ICollection<string> errors)
	{
		if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
		{
			errors.Add($"{fieldName} exceeds maximum length of {maxLength} characters.");
		}
	}

	private static string? TruncateForStorage(string? value, int maxLength)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
		{
			return value;
		}

		return value[..maxLength];
	}

	private string GetPreviewCacheKey(string token) =>
		$"AttendanceImportPreview:Tenant:{CurrentTenant.Id ?? Guid.Empty}:Token:{token}";

	private static string BuildRowKey(Guid employeeId, DateOnly attendanceDate) =>
		$"{employeeId:N}|{attendanceDate:yyyy-MM-dd}";

	private static string NormalizeHeader(string value) =>
		string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

	private static string NormalizeComparableValue(string? value) =>
		string.Join(" ", (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

	private static void EnsureSinglePayrollMonth(List<AttendanceImportPreviewRow> previewRows)
	{
		var distinctMonthKeys = previewRows
			.Where(x => x.AttendanceDateValue.HasValue)
			.Select(x => $"{x.AttendanceDateValue!.Value.Year:D4}-{x.AttendanceDateValue.Value.Month:D2}")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (distinctMonthKeys.Count > 1)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMultiplePayrollMonths);
		}
	}

	private static string? ResolveInternalPayrollMonthYear(List<AttendanceImportPreviewRow> previewRows)
	{
		var rowWithDate = previewRows.FirstOrDefault(x => x.AttendanceDateValue.HasValue);
		if (rowWithDate?.AttendanceDateValue == null)
		{
			return null;
		}

		var date = rowWithDate.AttendanceDateValue.Value;
		return $"{date.Year:D4}-{date.Month:D2}";
	}

	private static string? ToApiMonthYear(string? internalMonthYear)
	{
		if (string.IsNullOrWhiteSpace(internalMonthYear))
		{
			return null;
		}

		if (!DateTime.TryParseExact(
			$"{internalMonthYear}-01",
			"yyyy-MM-dd",
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var monthDate))
		{
			return null;
		}

		return $"{monthDate.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant()}-{monthDate.Year}";
	}

	private string BuildImportedByName()
	{
		var fullName = string.Join(" ",
			(CurrentUser.Name ?? string.Empty).Trim(),
			(CurrentUser.SurName ?? string.Empty).Trim()).Trim();

		if (!string.IsNullOrWhiteSpace(fullName))
		{
			return fullName;
		}

		if (!string.IsNullOrWhiteSpace(CurrentUser.UserName))
		{
			return CurrentUser.UserName!;
		}

		return CurrentUser.Id?.ToString() ?? "unknown-user";
	}

	private static bool TryParseDate(object value, out DateOnly date)
	{
		switch (value)
		{
			case DateTime dateTime:
				date = DateOnly.FromDateTime(dateTime);
				return true;
			case string stringValue when DateTime.TryParseExact(
				stringValue.Trim(),
				["dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"],
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var parsedExact):
				date = DateOnly.FromDateTime(parsedExact);
				return true;
			case string stringValue when DateTime.TryParse(
				stringValue.Trim(),
				CultureInfo.InvariantCulture,
				DateTimeStyles.AllowWhiteSpaces,
				out var parsedFlexible):
				date = DateOnly.FromDateTime(parsedFlexible);
				return true;
			default:
				date = default;
				return false;
		}
	}

	private static bool TryParseTime(object? value, out TimeSpan time)
	{
		switch (value)
		{
			case null:
				time = default;
				return false;
			case TimeSpan timeSpan:
				time = timeSpan;
				return true;
			case DateTime dateTime:
				time = dateTime.TimeOfDay;
				return true;
			case string stringValue when string.IsNullOrWhiteSpace(stringValue):
				time = default;
				return false;
			case string stringValue:
				var normalized = stringValue.Trim();
				if (DateTime.TryParseExact(
					normalized,
					["HH:mm", "H:mm", "hh:mm tt", "h:mm tt", "hh:mmtt", "h:mmtt", "HH:mm:ss", "hh:mm:ss tt", "h:mm:ss tt"],
					CultureInfo.InvariantCulture,
					DateTimeStyles.AllowWhiteSpaces,
					out var parsedDateTime))
				{
					time = parsedDateTime.TimeOfDay;
					return true;
				}

				if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var parsedTimeSpan))
				{
					time = parsedTimeSpan;
					return true;
				}

				time = default;
				return false;
			default:
				time = default;
				return false;
		}
	}

	private static bool TryParseStatus(string input, out EmployeeAttendanceStatusType status)
	{
		var normalized = input.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
		if (normalized.Equals("Present", StringComparison.OrdinalIgnoreCase))
		{
			status = EmployeeAttendanceStatusType.Present;
			return true;
		}

		if (normalized.Equals("HalfDayLeave", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("HalfDay", StringComparison.OrdinalIgnoreCase))
		{
			status = EmployeeAttendanceStatusType.HalfDayLeave;
			return true;
		}

		if (normalized.Equals("FullDayLeave", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("Absent", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("FullDay", StringComparison.OrdinalIgnoreCase))
		{
			status = EmployeeAttendanceStatusType.FullDayLeave;
			return true;
		}

		status = default;
		return false;
	}

	private static DateTimeOffset CombineDateAndTime(DateOnly attendanceDate, TimeSpan time)
	{
		var localDateTime = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(time), DateTimeKind.Unspecified);
		var offset = DefaultImportTimeZone.GetUtcOffset(localDateTime);
		return new DateTimeOffset(localDateTime, offset);
	}

	private static AttendanceImportRowResultDto MapToResultDto(AttendanceImportPreviewRow row)
	{
		return new AttendanceImportRowResultDto
		{
			RowNumber = row.RowNumber,
			Action = row.Action,
			AttendanceDate = row.AttendanceDate,
			EmployeeCode = row.EmployeeCode,
			EmployeeName = row.EmployeeName,
			ContactNo = row.ContactNo,
			TimeIn = row.TimeIn,
			TimeOut = row.TimeOut,
			Status = row.Status,
			Remarks = row.Remarks,
			Errors = row.Errors,
			Warnings = row.Warnings
		};
	}

	private sealed class AttendanceImportRawRow
	{
		public int RowNumber { get; set; }
		public string? AttendanceDate { get; set; }
		public string? EmployeeCode { get; set; }
		public string? TimeIn { get; set; }
		public string? TimeOut { get; set; }
		public string? Status { get; set; }
		public string? Remarks { get; set; }
		public string? EmployeeName { get; set; }
		public string? ContactNo { get; set; }
		public object? AttendanceDateCell { get; set; }
		public object? TimeInCell { get; set; }
		public object? TimeOutCell { get; set; }

		public bool IsEmpty() =>
			string.IsNullOrWhiteSpace(AttendanceDate) &&
			string.IsNullOrWhiteSpace(EmployeeCode) &&
			string.IsNullOrWhiteSpace(TimeIn) &&
			string.IsNullOrWhiteSpace(TimeOut) &&
			string.IsNullOrWhiteSpace(Status) &&
			string.IsNullOrWhiteSpace(Remarks) &&
			string.IsNullOrWhiteSpace(EmployeeName) &&
			string.IsNullOrWhiteSpace(ContactNo);
	}

	private sealed class AttendanceImportPreviewRow
	{
		public int RowNumber { get; set; }
		public string Action { get; set; } = "invalid";
		public string AttendanceDate { get; set; } = string.Empty;
		public DateOnly? AttendanceDateValue { get; set; }
		public string EmployeeCode { get; set; } = string.Empty;
		public string? EmployeeName { get; set; }
		public string? ContactNo { get; set; }
		public string? TimeIn { get; set; }
		public DateTimeOffset? TimeInValue { get; set; }
		public string? TimeOut { get; set; }
		public DateTimeOffset? TimeOutValue { get; set; }
		public string? Status { get; set; }
		public EmployeeAttendanceStatusType StatusValue { get; set; }
		public string? Remarks { get; set; }
		public Guid EmployeeId { get; set; }
		public Guid? EducationMediumStageId { get; set; }
		public List<string> Errors { get; } = [];
		public List<string> Warnings { get; } = [];
	}


}


