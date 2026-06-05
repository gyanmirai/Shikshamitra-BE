using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public class AttendanceImportSaveEntryNormalizer(
	AttendanceImportEmployeeResolver employeeResolver,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver,
	AttendancePolicyWorkedTimeEvaluator workedTimeEvaluator)
	: ITransientDependency
{
	public async Task<List<NormalizedAttendanceImportSaveEntry>> NormalizeAsync(
		IReadOnlyList<AttendanceImportRowsSaveEntryDto> entries,
		Dictionary<Guid, AttendanceImportRow> rowLookup,
		Dictionary<string, Guid> employeeLookup)
	{
		var normalizedEntries = entries
			.Select(entry => NormalizeSaveEntry(entry, rowLookup[entry.RowId], employeeLookup))
			.ToList();

		return await EvaluateSaveEntriesAsync(normalizedEntries);
	}

	private NormalizedAttendanceImportSaveEntry NormalizeSaveEntry(
		AttendanceImportRowsSaveEntryDto entry,
		AttendanceImportRow row,
		Dictionary<string, Guid> employeeLookup)
	{
		var expectedEmployeeId = AttendanceImportLogProjection.TryResolveEmployeeId(row.EmployeeCode, employeeLookup);
		if (!expectedEmployeeId.HasValue || expectedEmployeeId.Value != entry.EmployeeId)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("rowId", entry.RowId)
				.WithData("reason", "Employee does not match the import row.");
		}

		var rowDate = ParseImportRowDate(row.AttendanceDate);
		var entryDate = ParseImportRowDate(entry.AttendanceDate);
		if (rowDate != entryDate)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("rowId", entry.RowId)
				.WithData("reason", "Attendance date does not match the import row.");
		}

		var status = NormalizeStatusForImportSave(entry.StatusWire);
		var inTime = NormalizeTimeForSave(entry.InTime, rowDate);
		var outTime = NormalizeTimeForSave(entry.OutTime, rowDate);

		return new NormalizedAttendanceImportSaveEntry(
			entry.RowId,
			entry.EmployeeId,
			rowDate,
			status.SaveWire,
			status.ImportRowStatus,
			inTime.SaveValue,
			outTime.SaveValue,
			inTime.ImportRowValue,
			outTime.ImportRowValue,
			entry.Remarks);
	}

	private async Task<List<NormalizedAttendanceImportSaveEntry>> EvaluateSaveEntriesAsync(
		List<NormalizedAttendanceImportSaveEntry> entries)
	{
		if (entries.Count == 0)
		{
			return entries;
		}

		var employeeIds = entries.Select(x => x.EmployeeId).Distinct().ToList();
		var employeeById = await employeeResolver.BuildLookupByIdAsync(employeeIds);
		var captureModeLookup = await captureModeLookupResolver.ResolveForPairsAsync(
			entries.Select(x => (x.EmployeeId, x.AttendanceDate)));
		var punchContextLookup = entries
			.Select(x => new
			{
				x.EmployeeId,
				x.AttendanceDate,
				TimeIn = ParseNullableOffset(x.InTimeForSave)
			})
			.Where(x => x.TimeIn.HasValue)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<AttendancePolicyEvaluationPunchContext>)x
					.Select(entry => new AttendancePolicyEvaluationPunchContext(entry.AttendanceDate, entry.TimeIn!.Value))
					.ToList());
		var result = new List<NormalizedAttendanceImportSaveEntry>(entries.Count);
		foreach (var entry in entries)
		{
			if (!employeeById.TryGetValue(entry.EmployeeId, out var employee))
			{
				throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
					.WithData("rowId", entry.RowId)
					.WithData("reason", "Employee does not match the import row.");
			}

			if (captureModeLookup.GetValueOrDefault((entry.EmployeeId, entry.AttendanceDate)) == AttendanceCaptureMode.ManualStatus)
			{
				result.Add(entry);
				continue;
			}

			punchContextLookup.TryGetValue(entry.EmployeeId, out var punchContext);
			var evaluation = await workedTimeEvaluator.EvaluateAsync(
				employee,
				entry.AttendanceDate,
				ParseNullableOffset(entry.InTimeForSave),
				ParseNullableOffset(entry.OutTimeForSave),
				AttendanceImportSaveEntryParsing.ParseDomainStatus(entry.SaveStatusWire),
				punchContext);

			result.Add(entry with
			{
				SaveStatusWire = AttendanceImportSaveEntryParsing.ToSaveWire(evaluation.Status),
				ImportRowStatus = evaluation.Status.ToString(),
				Warnings = evaluation.Warnings
			});
		}

		return result;
	}

	private static NormalizedAttendanceImportStatus NormalizeStatusForImportSave(string? statusWire)
	{
		return AttendanceImportSaveEntryParsing.NormalizeStatusKey(statusWire) switch
		{
			"PRESENT" => new NormalizedAttendanceImportStatus("Present", "Present"),
			"ABSENT" or "FULLDAYLEAVE" => new NormalizedAttendanceImportStatus("Absent", "FullDayLeave"),
			"HALFDAY" or "HALFDAYLEAVE" => new NormalizedAttendanceImportStatus("HalfDay", "HalfDayLeave"),
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};
	}

	private static DateTimeOffset? ParseNullableOffset(string? value) =>
		AttendanceImportSaveEntryParsing.ParseNullableOffset(value);

	private static DateOnly ParseImportRowDate(string? value)
	{
		return AttendanceImportSaveEntryParsing.ParseImportRowDate(value);
	}

	private static NormalizedAttendanceImportTime NormalizeTimeForSave(string? value, DateOnly attendanceDate)
	{
		return AttendanceImportSaveEntryParsing.NormalizeTimeForSave(value, attendanceDate);
	}

	private sealed record NormalizedAttendanceImportStatus(string SaveWire, string ImportRowStatus);
}
