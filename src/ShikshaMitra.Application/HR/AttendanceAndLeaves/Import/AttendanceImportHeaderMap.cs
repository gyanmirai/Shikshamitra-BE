using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceImportHeaderMap
{
	private static readonly string[] AllowedHeaders =
	[
		AttendanceImportFileParser.AttendanceDateHeader,
		AttendanceImportFileParser.EmployeeCodeHeader,
		AttendanceImportFileParser.TimeInHeader,
		AttendanceImportFileParser.TimeOutHeader,
		AttendanceImportFileParser.StatusHeader,
		AttendanceImportFileParser.RemarksHeader,
		AttendanceImportFileParser.EmployeeNameHeader,
		AttendanceImportFileParser.ContactNoHeader
	];

	public static void Validate(IReadOnlyCollection<string> headers)
	{
		var normalizedHeaders = headers.Select(NormalizeHeader).ToList();
		var allowed = AllowedHeaders.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (normalizedHeaders.Any(x => !allowed.Contains(x)))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportUnknownHeaders);
		}
		if (normalizedHeaders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedHeaders.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportDuplicateHeaders);
		}

		if (!normalizedHeaders.Contains(NormalizeHeader(AttendanceImportFileParser.AttendanceDateHeader), StringComparer.OrdinalIgnoreCase) ||
		    !normalizedHeaders.Contains(NormalizeHeader(AttendanceImportFileParser.EmployeeCodeHeader), StringComparer.OrdinalIgnoreCase))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMissingHeaders);
		}
	}

	public static Dictionary<string, int> Build(IReadOnlyList<string> headers)
	{
		var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < headers.Count; i++)
		{
			result[NormalizeHeader(headers[i])] = i + 1;
		}

		return result;
	}

	public static string? GetCellText(IXLRow row, IReadOnlyDictionary<string, int> headerIndex, string header)
	{
		if (!headerIndex.TryGetValue(NormalizeHeader(header), out var column))
		{
			return null;
		}

		var value = row.Cell(column).GetFormattedString();
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	public static object? GetCellValue(IXLRow row, IReadOnlyDictionary<string, int> headerIndex, string header)
	{
		if (!headerIndex.TryGetValue(NormalizeHeader(header), out var column))
		{
			return null;
		}

		var cell = row.Cell(column);
		if (cell.IsEmpty())
		{
			return null;
		}

		if (cell.TryGetValue<DateTime>(out var dateTimeValue))
		{
			return dateTimeValue;
		}

		if (cell.TryGetValue<TimeSpan>(out var timeSpanValue))
		{
			return timeSpanValue;
		}

		return cell.GetFormattedString();
	}

	internal static string NormalizeHeader(string value)
	{
		var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
		if (normalized.Equals("EmployeeCode", StringComparison.OrdinalIgnoreCase)) return "STAFFCODE";
		if (normalized.Equals("EmployeeName", StringComparison.OrdinalIgnoreCase)) return "STAFFNAME";
		return normalized;
	}
}
