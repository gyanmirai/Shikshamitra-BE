using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

[RemoteService(false)]
public class AttendanceImportPreviewWorkflow(
	IDistributedCache<AttendanceImportPreviewCacheItem> previewCache,
	AttendanceImportPreviewRowEnricher rowEnricher,
	AttendanceImportPreviewRowClassifier rowClassifier,
	AttendanceImportBatchRecorder batchRecorder)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceImportPreviewResponseDto> PreviewAsync(string fileName, byte[] fileContent)
	{
		AttendanceImportFileParser.ValidateUpload(fileName, fileContent);

		var rawRows = AttendanceImportFileParser.ParseFile(fileName, fileContent);
		var previewRows = rawRows.Select(AttendanceImportPreviewMapper.MapRawToPreviewRow).ToList();
		AttendanceImportPreviewMapper.EnsureSinglePayrollMonth(previewRows);

		AttendanceImportPreviewMapper.MarkDuplicateRows(previewRows);
		await rowEnricher.EnrichAsync(previewRows);

		var validRows = previewRows.Where(x => x.Errors.Count == 0).ToList();
		await rowClassifier.ClassifyAsync(validRows);

		foreach (var invalidRow in previewRows.Where(x => x.Errors.Count > 0))
		{
			invalidRow.Action = "invalid";
		}

		string? previewToken = null;
		var previewTokenExpiresAt = default(DateTime?);
		var batchId = GuidGenerator.Create();

		if (validRows.Count > 0)
		{
			previewToken = await CachePreviewAsync(validRows, batchId);
			previewTokenExpiresAt = Clock.Now.AddMinutes(30);
		}

		var monthYear = AttendanceImportPreviewMapper.ResolveInternalPayrollMonthYear(previewRows);
		await batchRecorder.EnsureFileNameIsUniqueForPayrollMonthAsync(fileName, monthYear);
		try
		{
			await batchRecorder.CreateAsync(
				batchId,
				fileName,
				previewRows,
				monthYear,
				previewToken,
				previewTokenExpiresAt);
		}
		catch
		{
			if (!string.IsNullOrWhiteSpace(previewToken))
			{
				await previewCache.RemoveAsync(GetPreviewCacheKey(previewToken));
			}

			throw;
		}

		return new AttendanceImportPreviewResponseDto
		{
			PreviewToken = previewToken,
			CanConfirm = validRows.Count > 0,
			Summary = new AttendanceImportSummaryDto
			{
				TotalRows = previewRows.Count,
				ValidRows = validRows.Count,
				InvalidRows = previewRows.Count(x => x.Errors.Count > 0),
				CreateCount = previewRows.Count(x => x.Action == "create"),
				ReplaceCount = previewRows.Count(x => x.Action == "replace")
			},
			Rows = previewRows
				.OrderBy(x => x.RowNumber)
				.Select(AttendanceImportPreviewMapper.MapToResultDto)
			.ToList()
		};
	}

	private async Task<string> CachePreviewAsync(List<AttendanceImportPreviewRow> validRows, Guid importBatchId)
	{
		var userId = CurrentUser.Id
			?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var token = GuidGenerator.Create().ToString("N");

		await previewCache.SetAsync(
			GetPreviewCacheKey(token),
			new AttendanceImportPreviewCacheItem
			{
				Token = token,
				TenantId = CurrentTenant.Id,
				UserId = userId,
				ImportBatchId = importBatchId,
				Rows = validRows.Select(x => new AttendanceImportPreviewCacheRow
				{
					EmployeeId = x.EmployeeId,
					EducationMediumStageId = x.EducationMediumStageId,
					AttendanceDate = x.AttendanceDateValue!.Value,
					TimeIn = x.TimeInValue,
					TimeOut = x.TimeOutValue,
					StatusType = x.StatusValue,
					Remarks = AttendanceImportPreviewMapper.TruncateForStorage(x.Remarks, AttendanceImportPreviewMapper.RowRemarksMaxLength)
				}).ToList()
			},
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
			});

		return token;
	}

	private string GetPreviewCacheKey(string token) =>
		$"AttendanceImportPreview:Tenant:{CurrentTenant.Id ?? Guid.Empty}:Token:{token}";
}
