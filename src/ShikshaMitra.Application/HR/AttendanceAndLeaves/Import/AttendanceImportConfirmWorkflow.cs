using Microsoft.Extensions.Caching.Distributed;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

[RemoteService(false)]
public class AttendanceImportConfirmWorkflow(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IDistributedCache<AttendanceImportPreviewCacheItem> previewCache,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver)
	: ApplicationService
{
	public async Task<AttendanceImportConfirmResponseDto> ConfirmAsync(AttendanceImportConfirmRequestDto input)
	{
		var userId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var cacheKey = GetPreviewCacheKey(input.PreviewToken);
		var cached = await previewCache.GetAsync(cacheKey);
		if (cached == null)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewExpired);
		}

		// Preview payloads live in distributed cache, so ABP query filters cannot enforce this ownership boundary.
		if (cached.UserId != userId || cached.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		if (cached.Rows.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportNoValidRows);
		}

		var employeeIds = cached.Rows.Select(x => x.EmployeeId).Distinct().ToList();
		var minDate = cached.Rows.Min(x => x.AttendanceDate);
		var maxDate = cached.Rows.Max(x => x.AttendanceDate);

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();

		var existingRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				x.AttendanceDate >= minDate &&
				x.AttendanceDate <= maxDate));

		var existingLookup = existingRows.ToDictionary(
			x => AttendanceImportPreviewMapper.BuildRowKey(x.EmployeeId, x.AttendanceDate),
			x => x);

		var createdCount = 0;
		var replacedCount = 0;
		var captureModeLookup = await captureModeLookupResolver.ResolveForPairsAsync(
			cached.Rows.Select(x => (x.EmployeeId, x.AttendanceDate)));

		foreach (var row in cached.Rows)
		{
			captureModeLookup.TryGetValue((row.EmployeeId, row.AttendanceDate), out var captureMode);
			var isManualStatus = captureMode == AttendanceCaptureMode.ManualStatus;
			var timeIn = isManualStatus ? null : row.TimeIn;
			var timeOut = isManualStatus ? null : row.TimeOut;
			var key = AttendanceImportPreviewMapper.BuildRowKey(row.EmployeeId, row.AttendanceDate);
			if (existingLookup.TryGetValue(key, out var existing))
			{
				existing.TimeIn = timeIn;
				existing.TimeOut = timeOut;
				existing.StatusType = row.StatusType;
				existing.Source = AttendanceRecordSource.CsvImport;
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
				TimeIn = timeIn,
				TimeOut = timeOut,
				StatusType = row.StatusType,
				Source = AttendanceRecordSource.CsvImport,
				EducationMediumStageId = row.EducationMediumStageId,
				Remarks = row.Remarks
			}, autoSave: false);

			createdCount++;
		}

		var importBatch = await attendanceImportBatchRepository.FindAsync(cached.ImportBatchId);
		if (importBatch != null)
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

	private string GetPreviewCacheKey(string token) =>
		$"AttendanceImportPreview:Tenant:{CurrentTenant.Id ?? Guid.Empty}:Token:{token}";
}
