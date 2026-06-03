using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportLogReader(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	ISalarySlipRepository salarySlipRepository,
	AttendanceImportEmployeeResolver employeeResolver)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(GetAttendanceImportLogListInput input)
	{
		AttendanceImportLogPaging.Validate(input.SkipCount, input.MaxResultCount, maxUpperBound: 200);

		var query = await attendanceImportBatchRepository.GetQueryableAsync();

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
			items.Select(x => AttendanceImportLogProjection.ToListItemDto(x, finalizedMonthLookup)).ToList());
	}

	public async Task<AttendanceImportLogDetailDto> GetAsync(Guid importId)
	{
		var entity = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);

		var finalizedMonthLookup = await BuildFinalizedMonthLookupAsync([entity]);
		return AttendanceImportLogProjection.ToDetailDto(entity, finalizedMonthLookup);
	}

	public async Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId, GetAttendanceImportRowsInput input)
	{
		AttendanceImportLogPaging.Validate(input.SkipCount, input.MaxResultCount, maxUpperBound: 500);

		var batch = await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);

		var query = await attendanceImportRowRepository.GetQueryableAsync();
		query = query.Where(x => x.BatchId == importId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var skipCount = Math.Max(0, input.SkipCount);
		var maxResultCount = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.RowNumber)
				.Skip(skipCount)
				.Take(maxResultCount));
		var employeeLookup = await employeeResolver.BuildLookupByCodeAsync(items);

		return new PagedResultDto<AttendanceImportRowDto>(
			totalCount,
			items.Select(row => AttendanceImportLogProjection.ToRowDto(row, employeeLookup)).ToList());
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
}
