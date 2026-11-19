using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceImportFileParser
{
	public const string AttendanceDateHeader = "Attendance Date";
	public const string EmployeeCodeHeader = "Staff Code";
	public const string TimeInHeader = "Time In";
	public const string TimeOutHeader = "Time Out";
	public const string StatusHeader = "Status";
	public const string RemarksHeader = "Remarks";
	public const string EmployeeNameHeader = "Staff Name";
	public const string ContactNoHeader = "Contact No";

	private const long MaxFileSizeBytes = 5 * 1024 * 1024;

	public static void ValidateUpload(string fileName, byte[] fileContent)
	{
		if (fileContent.Length == 0)
			throw new BusinessException(HRErrorCodes.AttendanceImportFileEmpty);
		if (fileContent.Length > MaxFileSizeBytes)
			throw new BusinessException(HRErrorCodes.AttendanceImportFileTooLarge);

		var extension = Path.GetExtension(fileName.Trim()).ToLowerInvariant();
		if (extension is not ".xlsx" and not ".xls" and not ".csv")
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidExtension);
	}

	public static List<AttendanceImportRawRow> ParseFile(string fileName, byte[] fileContent)
	{
		try
		{
			return Path.GetExtension(fileName.Trim()).ToLowerInvariant() switch
			{
				".xlsx" => ParseExcel(fileContent),
				".xls" => ParseExcel(fileContent),
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

		AttendanceImportHeaderMap.Validate(headers);
		var headerIndex = AttendanceImportHeaderMap.Build(headers);
		var result = new List<AttendanceImportRawRow>();

		for (var rowNumber = 2; rowNumber <= usedRange.LastRow().RowNumber(); rowNumber++)
		{
			var row = worksheet.Row(rowNumber);
			var rawRow = new AttendanceImportRawRow
			{
				RowNumber = rowNumber,
				AttendanceDate = AttendanceImportHeaderMap.GetCellText(row, headerIndex, AttendanceDateHeader),
				EmployeeCode = AttendanceImportHeaderMap.GetCellText(row, headerIndex, EmployeeCodeHeader),
				TimeIn = AttendanceImportHeaderMap.GetCellText(row, headerIndex, TimeInHeader),
				TimeOut = AttendanceImportHeaderMap.GetCellText(row, headerIndex, TimeOutHeader),
				Status = AttendanceImportHeaderMap.GetCellText(row, headerIndex, StatusHeader),
				Remarks = AttendanceImportHeaderMap.GetCellText(row, headerIndex, RemarksHeader),
				EmployeeName = AttendanceImportHeaderMap.GetCellText(row, headerIndex, EmployeeNameHeader),
				ContactNo = AttendanceImportHeaderMap.GetCellText(row, headerIndex, ContactNoHeader),
				AttendanceDateCell = AttendanceImportHeaderMap.GetCellValue(row, headerIndex, AttendanceDateHeader),
				TimeInCell = AttendanceImportHeaderMap.GetCellValue(row, headerIndex, TimeInHeader),
				TimeOutCell = AttendanceImportHeaderMap.GetCellValue(row, headerIndex, TimeOutHeader)
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
			TrimOptions = TrimOptions.Trim,
			PrepareHeaderForMatch = args => AttendanceImportHeaderMap.NormalizeHeader(args.Header)
		};

		using var csv = new CsvReader(reader, config);
		if (!csv.Read() || !csv.ReadHeader())
			throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);

		var headers = csv.HeaderRecord?.ToList()
			?? throw new BusinessException(HRErrorCodes.AttendanceImportMalformedFile);
		AttendanceImportHeaderMap.Validate(headers);

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
}
