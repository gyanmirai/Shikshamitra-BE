using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
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
	IRepository<Employee, Guid> employeeRepository,
	ISalarySlipRepository salarySlipRepository)
	: ApplicationService, IAttendanceImportLogAppService
{
	public async Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(GetAttendanceImportLogListInput input)
	{
		ValidatePaging(input.SkipCount, input.MaxResultCount, maxUpperBound: 200);

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
			RunSource = baseDto.RunSource,
			ExternalCorrelationId = baseDto.ExternalCorrelationId,
			IntegrationProvider = baseDto.IntegrationProvider,
			IsBatchLocked = baseDto.IsBatchLocked,
			ConfirmedAt = entity.ConfirmedAt,
			PreviewTokenExpiresAt = entity.PreviewTokenExpiresAt,
			ErrorsSummary = [.. entity.ErrorsSummary],
			WarningsSummary = [.. entity.WarningsSummary]
		};
	}

	public async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId, GetAttendanceImportRowsInput input)
	{
		ValidatePaging(input.SkipCount, input.MaxResultCount, maxUpperBound: 500);

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
		var employeeLookup = await BuildEmployeeLookupByCodeAsync(items);

		return new PagedResultDto<AttendanceImportRowDto>(
			totalCount,
			items.Select(row => MapToRowDto(row, employeeLookup)).ToList());
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

	private static AttendanceImportRowDto MapToRowDto(AttendanceImportRow row, Dictionary<string, Guid> employeeLookup)
	{
		var resolvedEmployeeId = TryResolveEmployeeId(row.EmployeeCode, employeeLookup);
		var statusCode = TryNormalizeStatusCode(row.Status, out var normalizedWarning);
		var warnings = row.Warnings.ToList();
		if (!resolvedEmployeeId.HasValue && !string.IsNullOrWhiteSpace(row.EmployeeCode))
		{
			warnings.Add($"Unable to resolve employeeId for employee code '{row.EmployeeCode}'.");
		}

		if (!string.IsNullOrWhiteSpace(normalizedWarning))
		{
			warnings.Add(normalizedWarning);
		}

		return new AttendanceImportRowDto
		{
			Id = row.Id,
			EmployeeId = resolvedEmployeeId,
			RowNumber = row.RowNumber,
			Action = row.Action,
			AttendanceDate = row.AttendanceDate,
			EmployeeCode = row.EmployeeCode,
			EmployeeName = row.EmployeeName,
			ContactNo = row.ContactNo,
			TimeIn = row.TimeIn,
			TimeOut = row.TimeOut,
			Status = row.Status,
			StatusCode = statusCode,
			Remarks = row.Remarks,
			Errors = [.. row.Errors],
			Warnings = warnings
		};
	}

	private async Task<Dictionary<string, Guid>> BuildEmployeeLookupByCodeAsync(List<AttendanceImportRow> rows)
	{
		var employeeCodes = rows
			.Select(row => row.EmployeeCode?.Trim())
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (employeeCodes.Count == 0)
		{
			return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		}
		var normalizedEmployeeCodes = employeeCodes
			.Select(code => code!.ToUpperInvariant())
			.ToList();

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var employees = await AsyncExecuter.ToListAsync(
			employeeQuery
				.Where(employee =>
					employee.EmployeeCode != null &&
					normalizedEmployeeCodes.Contains(employee.EmployeeCode.ToUpper()))
				.Select(employee => new { employee.EmployeeCode, employee.Id }));
		return employees
			.GroupBy(x => x.EmployeeCode, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
	}

	private static Guid? TryResolveEmployeeId(string? employeeCode, Dictionary<string, Guid> employeeLookup)
	{
		if (string.IsNullOrWhiteSpace(employeeCode))
		{
			return null;
		}

		return employeeLookup.TryGetValue(employeeCode.Trim(), out var employeeId) ? employeeId : null;
	}

	private static AttendanceImportRowStatusCode? TryNormalizeStatusCode(string? status, out string? warning)
	{
		warning = null;
		if (string.IsNullOrWhiteSpace(status))
		{
			return null;
		}

		var normalized = status.Trim().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
		return normalized switch
		{
			"PRESENT" => AttendanceImportRowStatusCode.Present,
			"ABSENT" => AttendanceImportRowStatusCode.Absent,
			"FULLDAYLEAVE" => AttendanceImportRowStatusCode.Absent,
			"HALFDAY" => AttendanceImportRowStatusCode.HalfDay,
			"HALFDAYLEAVE" => AttendanceImportRowStatusCode.HalfDay,
			"PENDING" => null,
			"NOTMARKED" => null,
			_ => SetStatusWarning(status, out warning)
		};
	}

	private static AttendanceImportRowStatusCode? SetStatusWarning(string originalStatus, out string? warning)
	{
		warning = $"Unable to normalize attendance status '{originalStatus}'.";
		return null;
	}

	private static void ValidatePaging(int skipCount, int maxResultCount, int maxUpperBound)
	{
		if (skipCount < 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidSkipCount);
		}

		if (maxResultCount <= 0 || maxResultCount > maxUpperBound)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidMaxResultCount)
				.WithData("allowedRange", $"1-{maxUpperBound}")
				.WithData("providedValue", maxResultCount);
		}
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
