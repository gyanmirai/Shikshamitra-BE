using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

/// <summary>
/// Service for parsing attendance import files (Excel and CSV)
/// </summary>
public class AttendanceFileParser : ITransientDependency
{
	private readonly ILogger<AttendanceFileParser> _logger;
	private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

	// Expected column names (case-insensitive)
	private static readonly string[] ExpectedColumns = new[]
	{
		"Employee Name",
		"Hours Worked",
		"Time In",
		"Time Out",
		"status",
		"Employee Code",
		"Contact No"
	};

	public AttendanceFileParser(ILogger<AttendanceFileParser> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Parses attendance file (Excel or CSV) and extracts rows
	/// </summary>
	public async Task<AttendanceFileParseResult> ParseFileAsync(
		Stream fileStream, 
		string fileName,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(fileStream);
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

		var result = new AttendanceFileParseResult();

		// Validate file size
		if (fileStream.Length == 0)
		{
			result.AddError("File is empty. Please upload a valid file with attendance data.");
			return result;
		}

		if (fileStream.Length > MaxFileSizeBytes)
		{
			result.AddError($"File size ({fileStream.Length / 1024 / 1024}MB) exceeds maximum allowed size ({MaxFileSizeBytes / 1024 / 1024}MB).");
			return result;
		}

		var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

		try
		{
			switch (fileExtension)
			{
				case ".xlsx":
					await ParseExcelFileAsync(fileStream, result);
					break;
				case ".csv":
					await ParseCsvFileAsync(fileStream, result);
					break;
				default:
					result.AddError($"Unsupported file format: '{fileExtension}'. Please upload .xlsx or .csv files.");
					break;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error parsing attendance file: {FileName}", fileName);
			result.AddError($"Error reading file: {ex.Message}. Please ensure the file is not corrupted.");
		}

		return result;
	}

	/// <summary>
	/// Parses Excel file
	/// </summary>
	private async Task ParseExcelFileAsync(Stream fileStream, AttendanceFileParseResult result)
	{
		// Copy to memory stream for processing
		using var stream = new MemoryStream();
		await fileStream.CopyToAsync(stream);
		stream.Position = 0;

		using var workbook = new XLWorkbook(stream);
		var worksheet = workbook.Worksheets.FirstOrDefault();

		if (worksheet == null)
		{
			result.AddError("Excel file contains no worksheets.");
			return;
		}

		var usedRows = worksheet.RowsUsed().ToList();
		if (usedRows.Count < 2)
		{
			result.AddError("File must contain at least a header row and one data row.");
			return;
		}

		// Parse header row
		var headerRow = usedRows[0];
		var columnMapping = ParseHeaderRow(headerRow, result);
		
		if (!result.IsValid)
			return;

		// Parse data rows
		foreach (var row in usedRows.Skip(1))
		{
			var rowNumber = row.RowNumber();
			
			// Skip empty rows
			if (row.CellsUsed().Count() == 0)
				continue;

			var importRow = new AttendanceImportRow
			{
				RowNumber = rowNumber,
				EmployeeName = GetCellValue(row, columnMapping, "Employee Name"),
				HoursWorked = GetCellValue(row, columnMapping, "Hours Worked"),
				TimeIn = GetCellValue(row, columnMapping, "Time In"),
				TimeOut = GetCellValue(row, columnMapping, "Time Out"),
				Status = GetCellValue(row, columnMapping, "status"),
				EmployeeCode = GetCellValue(row, columnMapping, "Employee Code"),
				ContactNo = GetCellValue(row, columnMapping, "Contact No")
			};

			result.Rows.Add(importRow);
		}

		_logger.LogInformation("Parsed {RowCount} rows from Excel file", result.Rows.Count);
	}

	/// <summary>
	/// Parses CSV file
	/// </summary>
	private async Task ParseCsvFileAsync(Stream fileStream, AttendanceFileParseResult result)
	{
		using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = true,
			HeaderValidated = null,
			MissingFieldFound = null,
			BadDataFound = null,
			TrimOptions = TrimOptions.Trim,
			DetectDelimiter = true // Auto-detect delimiter
		};

		using var csv = new CsvReader(reader, config);

		try
		{
			// Read header
			await csv.ReadAsync();
			csv.ReadHeader();

			if (csv.HeaderRecord == null || csv.HeaderRecord.Length == 0)
			{
				result.AddError("CSV file must contain a header row.");
				return;
			}

			// Validate header
			var columnMapping = ParseCsvHeader(csv.HeaderRecord, result);
			if (!result.IsValid)
				return;

			// Read data rows
			int rowNumber = 2; // Row 1 is header
			while (await csv.ReadAsync())
			{
				var importRow = new AttendanceImportRow
				{
					RowNumber = rowNumber,
					EmployeeName = GetCsvValue(csv, columnMapping, "Employee Name"),
					HoursWorked = GetCsvValue(csv, columnMapping, "Hours Worked"),
					TimeIn = GetCsvValue(csv, columnMapping, "Time In"),
					TimeOut = GetCsvValue(csv, columnMapping, "Time Out"),
					Status = GetCsvValue(csv, columnMapping, "status"),
					EmployeeCode = GetCsvValue(csv, columnMapping, "Employee Code"),
					ContactNo = GetCsvValue(csv, columnMapping, "Contact No")
				};

				result.Rows.Add(importRow);
				rowNumber++;
			}

			_logger.LogInformation("Parsed {RowCount} rows from CSV file", result.Rows.Count);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error parsing CSV file");
			result.AddError($"Error parsing CSV: {ex.Message}. Ensure the file uses proper CSV format with comma, semicolon, or tab delimiters.");
		}
	}

	/// <summary>
	/// Parses and validates Excel header row
	/// </summary>
	private Dictionary<string, int> ParseHeaderRow(IXLRow headerRow, AttendanceFileParseResult result)
	{
		var columnMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var foundColumns = new List<string>();

		foreach (var cell in headerRow.CellsUsed())
		{
			var headerText = cell.Value.ToString().Trim();
			var normalizedHeader = NormalizeHeaderName(headerText);

			if (ExpectedColumns.Any(col => NormalizeHeaderName(col).Equals(normalizedHeader, StringComparison.OrdinalIgnoreCase)))
			{
				columnMapping[headerText] = cell.Address.ColumnNumber;
				foundColumns.Add(headerText);
			}
		}

		// Validate all required columns are present
		ValidateRequiredColumns(foundColumns, result);

		return columnMapping;
	}

	/// <summary>
	/// Parses and validates CSV header
	/// </summary>
	private Dictionary<string, int> ParseCsvHeader(string[] headers, AttendanceFileParseResult result)
	{
		var columnMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var foundColumns = new List<string>();

		for (int i = 0; i < headers.Length; i++)
		{
			var headerText = headers[i].Trim();
			var normalizedHeader = NormalizeHeaderName(headerText);

			if (ExpectedColumns.Any(col => NormalizeHeaderName(col).Equals(normalizedHeader, StringComparison.OrdinalIgnoreCase)))
			{
				columnMapping[headerText] = i;
				foundColumns.Add(headerText);
			}
		}

		// Validate all required columns are present
		ValidateRequiredColumns(foundColumns, result);

		return columnMapping;
	}

	/// <summary>
	/// Validates that all required columns are present
	/// </summary>
	private void ValidateRequiredColumns(List<string> foundColumns, AttendanceFileParseResult result)
	{
		var missingColumns = new List<string>();

		foreach (var expectedCol in ExpectedColumns)
		{
			var normalized = NormalizeHeaderName(expectedCol);
			if (!foundColumns.Any(found => NormalizeHeaderName(found).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
			{
				missingColumns.Add(expectedCol);
			}
		}

		if (missingColumns.Any())
		{
			var message = $"Missing required columns: {string.Join(", ", missingColumns)}. " +
			              $"Expected columns: {string.Join(", ", ExpectedColumns)}";
			result.AddError(message);
		}
	}

	/// <summary>
	/// Normalizes header name for comparison (removes spaces, underscores, etc.)
	/// </summary>
	private string NormalizeHeaderName(string header)
	{
		return header.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
	}

	/// <summary>
	/// Gets cell value from Excel row
	/// </summary>
	private string? GetCellValue(IXLRow row, Dictionary<string, int> columnMapping, string columnName)
	{
		// Find the actual header name in the mapping (case-insensitive)
		var actualHeader = columnMapping.Keys.FirstOrDefault(k =>
			NormalizeHeaderName(k).Equals(NormalizeHeaderName(columnName), StringComparison.OrdinalIgnoreCase));

		if (actualHeader == null || !columnMapping.TryGetValue(actualHeader, out var columnIndex))
			return null;

		var cell = row.Cell(columnIndex);
		var value = cell.Value.ToString().Trim();
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	/// <summary>
	/// Gets field value from CSV row
	/// </summary>
	private string? GetCsvValue(CsvReader csv, Dictionary<string, int> columnMapping, string columnName)
	{
		// Find the actual header name in the mapping (case-insensitive)
		var actualHeader = columnMapping.Keys.FirstOrDefault(k =>
			NormalizeHeaderName(k).Equals(NormalizeHeaderName(columnName), StringComparison.OrdinalIgnoreCase));

		if (actualHeader == null || !columnMapping.TryGetValue(actualHeader, out var columnIndex))
			return null;

		try
		{
			var value = csv.GetField(columnIndex)?.Trim();
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
		catch
		{
			return null;
		}
	}
}

/// <summary>
/// Result of file parsing operation
/// </summary>
public class AttendanceFileParseResult
{
	public List<AttendanceImportRow> Rows { get; set; } = new();
	public List<string> Errors { get; set; } = new();
	public bool IsValid => !Errors.Any();

	public void AddError(string error)
	{
		Errors.Add(error);
	}
}
