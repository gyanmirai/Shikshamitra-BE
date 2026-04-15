using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
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
			var payrollMonth = input.PayrollMonth.Trim().ToUpperInvariant();
			query = query.Where(x => x.PayrollMonth == payrollMonth);
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

		if (entity.TenantId != CurrentTenant.Id)
		{
			throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		}

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
		if (batch.TenantId != CurrentTenant.Id)
		{
			throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
		}

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
			AttendanceImportBatchStatus.Preview => "preview",
			AttendanceImportBatchStatus.Confirmed when isPayrollLocked => "payroll_locked",
			AttendanceImportBatchStatus.Confirmed => "confirmed",
			_ => "preview"
		};

		return new AttendanceImportLogListItemDto
		{
			Id = entity.Id,
			FileName = entity.FileName,
			ImportedByUserId = entity.ImportedByUserId,
			ImportedByName = entity.ImportedByName,
			ImportedAt = entity.ImportedAt,
			Status = status,
			PayrollMonth = entity.PayrollMonth ?? ToApiMonthYear(entity.PayrollMonthYear),
			TotalRows = entity.TotalRows,
			ValidRows = entity.ValidRows,
			InvalidRows = entity.InvalidRows,
			CreateCount = entity.CreateCount,
			ReplaceCount = entity.ReplaceCount,
			ProcessedRows = entity.ProcessedRows,
			CreatedCount = entity.CreatedCount,
			ReplacedCount = entity.ReplacedCount,
			CanOpen = entity.TotalRows > 0
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
}
