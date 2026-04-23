using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportLogAppService(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	ISalarySlipRepository salarySlipRepository)
	: ApplicationService, IAttendanceImportLogAppService
{
	public async Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(GetAttendanceImportLogListInput input)
	{
		var query = await attendanceImportBatchRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			query = query.Where(x =>
				x.FileName.Contains(search) ||
				x.ImportedByName.Contains(search));
		}

		if (!string.IsNullOrWhiteSpace(input.PayrollMonth))
		{
			if (!PayrollMonthNormalizer.TryNormalizeToInternal(input.PayrollMonth, out var normalizedPayrollMonth))
			{
				throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat)
					.WithData("providedMonth", input.PayrollMonth)
					.WithData("acceptedFormats", "YYYY-MM, MMMM-YYYY, or date-like values");
			}

			query = query.Where(x => x.PayrollMonthYear == normalizedPayrollMonth);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var skipCount = Math.Max(0, input.SkipCount);
		var maxResultCount = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.ImportedAt)
				.Skip(skipCount)
				.Take(maxResultCount));

		var finalizedMonthLookup = await BuildFinalizedMonthLookupAsync(items);
		return new PagedResultDto<AttendanceImportLogListItemDto>(
			totalCount,
			items.Select(x => MapToListItemDto(x, finalizedMonthLookup)).ToList());
	}

	public async Task<AttendanceImportLogDetailDto> GetAsync(Guid importId)
	{
		var entity = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);

		EnsureTenantOwnership(entity, importId);

		var finalizedMonthLookup = await BuildFinalizedMonthLookupAsync([entity]);
		var baseDto = MapToListItemDto(entity, finalizedMonthLookup);
		return new AttendanceImportLogDetailDto
		{
			Id = baseDto.Id,
			FileName = baseDto.FileName,
			ImportedByUserId = baseDto.ImportedByUserId,
			ImportedByName = baseDto.ImportedByName,
			ImportedAt = baseDto.ImportedAt,
			Status = baseDto.Status,
			PayrollMonth = baseDto.PayrollMonth,
			TotalRows = baseDto.TotalRows,
			ValidRows = baseDto.ValidRows,
			InvalidRows = baseDto.InvalidRows,
			CreateCount = baseDto.CreateCount,
			ReplaceCount = baseDto.ReplaceCount,
			ProcessedRows = baseDto.ProcessedRows,
			CreatedCount = baseDto.CreatedCount,
			ReplacedCount = baseDto.ReplacedCount,
			CanOpen = baseDto.CanOpen,
			ConfirmedAt = entity.ConfirmedAt,
			PreviewTokenExpiresAt = entity.PreviewTokenExpiresAt,
			ErrorsSummary = [.. entity.ErrorsSummary],
			WarningsSummary = [.. entity.WarningsSummary]
		};
	}

	public async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId, GetAttendanceImportRowsInput input)
	{
		var batch = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		EnsureTenantOwnership(batch, importId);

		var query = await attendanceImportRowRepository.GetQueryableAsync();
		query = query.Where(x => x.BatchId == importId);
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var skipCount = Math.Max(0, input.SkipCount);
		var maxResultCount = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.RowNumber)
				.Skip(skipCount)
				.Take(maxResultCount));

		return new PagedResultDto<AttendanceImportRowDto>(
			totalCount,
			items.Select(MapToRowDto).ToList());
	}

	public async Task LockBatchAsync(Guid importId)
	{
		var batch = await GetOwnedBatchAsync(importId);
		if (batch.IsBatchLocked)
		{
			return;
		}

		batch.SetBatchLocked(true);
		await attendanceImportBatchRepository.UpdateAsync(batch, autoSave: true);
	}

	public async Task UnlockBatchAsync(Guid importId)
	{
		var batch = await GetOwnedBatchAsync(importId);
		if (!batch.IsBatchLocked)
		{
			return;
		}

		batch.SetBatchLocked(false);
		await attendanceImportBatchRepository.UpdateAsync(batch, autoSave: true);
	}

	private async Task<Dictionary<string, bool>> BuildFinalizedMonthLookupAsync(IEnumerable<AttendanceImportBatch> items)
	{
		var monthYears = items
			.Select(x => x.PayrollMonthYear)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		foreach (var monthYear in monthYears)
		{
			result[monthYear!] = await salarySlipRepository.HasFinalizedSlipsForMonthAsync(monthYear!);
		}

		return result;
	}

	private static AttendanceImportLogListItemDto MapToListItemDto(
		AttendanceImportBatch entity,
		Dictionary<string, bool> finalizedMonthLookup)
	{
		var isPayrollLocked = entity.Status == AttendanceImportBatchStatus.Confirmed &&
			!string.IsNullOrWhiteSpace(entity.PayrollMonthYear) &&
			finalizedMonthLookup.GetValueOrDefault(entity.PayrollMonthYear!, false);

		var status = entity.Status switch
		{
			AttendanceImportBatchStatus.Preview => AttendanceImportLogStatus.Preview,
			AttendanceImportBatchStatus.Confirmed when isPayrollLocked => AttendanceImportLogStatus.PayrollLocked,
			AttendanceImportBatchStatus.Confirmed when entity.IsBatchLocked => AttendanceImportLogStatus.BatchLocked,
			AttendanceImportBatchStatus.Confirmed => AttendanceImportLogStatus.Confirmed,
			_ => AttendanceImportLogStatus.Preview
		};

		return new AttendanceImportLogListItemDto
		{
			Id = entity.Id,
			FileName = entity.FileName,
			ImportedByUserId = entity.ImportedByUserId,
			ImportedByName = entity.ImportedByName,
			ImportedAt = entity.ImportedAt,
			Status = status,
			PayrollMonth = entity.PayrollMonth ?? PayrollMonthNormalizer.ToApiMonthYear(entity.PayrollMonthYear),
			TotalRows = entity.TotalRows,
			ValidRows = entity.ValidRows,
			InvalidRows = entity.InvalidRows,
			CreateCount = entity.CreateCount,
			ReplaceCount = entity.ReplaceCount,
			ProcessedRows = entity.ProcessedRows,
			CreatedCount = entity.CreatedCount,
			ReplacedCount = entity.ReplacedCount,
			CanOpen = entity.TotalRows > 0,
			RunSource = entity.RunSource,
			ExternalCorrelationId = entity.ExternalCorrelationId,
			IntegrationProvider = entity.IntegrationProvider,
			IsBatchLocked = entity.IsBatchLocked
		};
	}

	private static AttendanceImportRowDto MapToRowDto(AttendanceImportRow row)
	{
		return new AttendanceImportRowDto
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
			Errors = [.. row.Errors],
			Warnings = [.. row.Warnings]
		};
	}

	private async Task<AttendanceImportBatch> GetOwnedBatchAsync(Guid importId)
	{
		var batch = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		EnsureTenantOwnership(batch, importId);
		return batch;
	}

	private void EnsureTenantOwnership(AttendanceImportBatch batch, Guid importId)
	{
		if (batch.TenantId != CurrentTenant.Id)
		{
			throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		}
	}
}
