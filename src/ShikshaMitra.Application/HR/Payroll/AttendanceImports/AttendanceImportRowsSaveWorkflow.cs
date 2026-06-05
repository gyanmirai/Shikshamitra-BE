using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportRowsSaveWorkflow(
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	ISalarySlipRepository salarySlipRepository,
	ITakeAttendanceAppService takeAttendanceAppService,
	AttendanceImportEmployeeResolver employeeResolver,
	AttendanceImportSaveEntryNormalizer saveEntryNormalizer,
	AttendanceImportLogAccessService accessService)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceImportRowsSaveOutput> SaveRowsAsync(Guid importId, AttendanceImportRowsSaveInput input)
	{
		if (input.Entries == null || input.Entries.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		if (input.Entries.Select(x => x.RowId).Distinct().Count() != input.Entries.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("reason", "Duplicate import row ids are not allowed.");
		}

		var batch = await accessService.GetOwnedBatchAsync(importId);
		if (batch.IsBatchLocked)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportBatchLocked);
		}

		if (!string.IsNullOrWhiteSpace(batch.PayrollMonthYear) &&
		    await salarySlipRepository.HasFinalizedSlipsForMonthAsync(batch.PayrollMonthYear!))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPayrollLocked);
		}

		var requestedRowIds = input.Entries.Select(x => x.RowId).ToList();
		var rowsQuery = await attendanceImportRowRepository.GetQueryableAsync();
		rowsQuery = rowsQuery.Where(x => x.BatchId == importId && requestedRowIds.Contains(x.Id));

		var importRows = await AsyncExecuter.ToListAsync(rowsQuery);
		if (importRows.Count != requestedRowIds.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch);
		}

		var employeeLookup = await employeeResolver.BuildLookupByCodeAsync(importRows);
		var rowLookup = importRows.ToDictionary(x => x.Id);
		var normalizedEntries = await saveEntryNormalizer.NormalizeAsync(input.Entries, rowLookup, employeeLookup);

		if (normalizedEntries
		    .Select(x => $"{x.EmployeeId:N}|{x.AttendanceDate:yyyy-MM-dd}")
		    .Distinct(StringComparer.OrdinalIgnoreCase)
		    .Count() != normalizedEntries.Count)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
				.WithData("reason", "Duplicate employee/date entries are not allowed.");
		}

		var upsertedCount = await SaveAttendanceByDateAsync(normalizedEntries);
		foreach (var entry in normalizedEntries)
		{
			rowLookup[entry.RowId].UpdateEditableValues(
				entry.InTimeForImportRow,
				entry.OutTimeForImportRow,
				entry.ImportRowStatus,
				entry.Remarks,
				entry.Warnings);
		}

		await attendanceImportRowRepository.UpdateManyAsync(importRows, autoSave: true);
		return new AttendanceImportRowsSaveOutput { UpsertedCount = upsertedCount };
	}

	private async Task<int> SaveAttendanceByDateAsync(System.Collections.Generic.List<NormalizedAttendanceImportSaveEntry> normalizedEntries)
	{
		var entriesByDate = normalizedEntries.GroupBy(x => x.AttendanceDate).ToList();
		var upsertedCount = 0;
		foreach (var dateGroup in entriesByDate)
		{
			var saveOutput = await takeAttendanceAppService.SaveDayAsync(new TakeAttendanceSaveInput
			{
				AttendanceDate = dateGroup.Key.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
				Entries = dateGroup.Select(entry => new TakeAttendanceSaveEntryDto
				{
					EmployeeId = entry.EmployeeId,
					StatusWire = entry.SaveStatusWire,
					InTime = entry.InTimeForSave,
					OutTime = entry.OutTimeForSave,
					Remarks = entry.Remarks
				}).ToList()
			});
			upsertedCount += saveOutput.UpsertedCount;
		}

		return upsertedCount;
	}
}
