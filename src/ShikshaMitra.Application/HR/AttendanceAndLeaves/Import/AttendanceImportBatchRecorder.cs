using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceImportBatchRecorder(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	ICurrentUser currentUser,
	IClock clock,
	IBlobContainer blobContainer,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	private const int ImportBatchFileNameMaxLength = 255;
	private const int ImportBatchImportedByNameMaxLength = 128;
	private const int ImportBatchPayrollMonthMaxLength = 20;

	internal async Task EnsureFileNameIsUniqueForPayrollMonthAsync(string fileName, string? monthYear)
	{
		var normalizedFileName = NormalizeImportFileName(fileName);
		if (string.IsNullOrWhiteSpace(normalizedFileName))
		{
			return;
		}

		var query = await attendanceImportBatchRepository.GetQueryableAsync();

		query = monthYear == null
			? query.Where(x => x.PayrollMonthYear == null)
			: query.Where(x => x.PayrollMonthYear == monthYear);

		var existingFileNames = await asyncExecuter.ToListAsync(query.Select(x => x.FileName));
		if (existingFileNames.Any(x => NormalizeImportFileName(x) == normalizedFileName))
		{
			throw new BusinessException(
					HRErrorCodes.AttendanceImportDuplicateFileName,
					"An attendance import with this filename already exists for this payroll month.")
				.WithData("fileName", fileName)
				.WithData("payrollMonth", monthYear ?? string.Empty);
		}
	}

	internal async Task<AttendanceImportBatch> CreateAsync(
		Guid batchId,
		string fileName,
		string? contentType,
		byte[] fileContent,
		List<AttendanceImportPreviewRow> previewRows,
		string? monthYear,
		string? previewToken,
		DateTime? previewTokenExpiresAt)
	{
		var importedByUserId = currentUser.Id
			?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var errorsSummary = previewRows
			.SelectMany(x => x.Errors)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(20)
			.ToList();
		var warningsSummary = previewRows
			.SelectMany(x => x.Warnings)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(20)
			.ToList();

		var originalFileBlobName = BuildOriginalFileBlobName(batchId, fileName);
		await blobContainer.SaveAsync(originalFileBlobName, fileContent, overrideExisting: true);

		var importBatch = new AttendanceImportBatch(
			batchId,
			currentTenant.Id,
			AttendanceImportPreviewMapper.TruncateForStorage(fileName, ImportBatchFileNameMaxLength) ?? string.Empty,
			importedByUserId,
			AttendanceImportPreviewMapper.TruncateForStorage(BuildImportedByName(), ImportBatchImportedByNameMaxLength) ?? string.Empty,
			clock.Now,
			monthYear,
			AttendanceImportPreviewMapper.TruncateForStorage(ToApiMonthYear(monthYear), ImportBatchPayrollMonthMaxLength),
			previewRows.Count,
			previewRows.Count(x => x.Errors.Count == 0),
			previewRows.Count(x => x.Errors.Count > 0),
			previewRows.Count(x => x.Action == "create"),
			previewRows.Count(x => x.Action == "replace"),
			errorsSummary,
			warningsSummary,
			originalFileBlobName: originalFileBlobName,
			originalFileContentType: NormalizeContentType(contentType));

		if (!string.IsNullOrWhiteSpace(previewToken) && previewTokenExpiresAt.HasValue)
		{
			importBatch.SetPreviewToken(previewToken, previewTokenExpiresAt.Value);
		}

		await attendanceImportBatchRepository.InsertAsync(importBatch, autoSave: true);

		var rowEntities = previewRows.Select(x => new AttendanceImportRow(
			guidGenerator.Create(),
			currentTenant.Id,
			importBatch.Id,
			x.RowNumber,
			AttendanceImportPreviewMapper.TruncateForStorage(x.Action, AttendanceImportPreviewMapper.RowActionMaxLength) ?? "invalid",
			AttendanceImportPreviewMapper.TruncateForStorage(x.AttendanceDate, AttendanceImportPreviewMapper.RowAttendanceDateMaxLength) ?? string.Empty,
			AttendanceImportPreviewMapper.TruncateForStorage(x.EmployeeCode, AttendanceImportPreviewMapper.RowEmployeeCodeMaxLength) ?? string.Empty,
			AttendanceImportPreviewMapper.TruncateForStorage(x.EmployeeName, AttendanceImportPreviewMapper.RowEmployeeNameMaxLength),
			AttendanceImportPreviewMapper.TruncateForStorage(x.ContactNo, AttendanceImportPreviewMapper.RowContactNoMaxLength),
			AttendanceImportPreviewMapper.TruncateForStorage(x.TimeIn, AttendanceImportPreviewMapper.RowTimeMaxLength),
			AttendanceImportPreviewMapper.TruncateForStorage(x.TimeOut, AttendanceImportPreviewMapper.RowTimeMaxLength),
			AttendanceImportPreviewMapper.TruncateForStorage(x.Status, AttendanceImportPreviewMapper.RowStatusMaxLength),
			AttendanceImportPreviewMapper.TruncateForStorage(x.Remarks, AttendanceImportPreviewMapper.RowRemarksMaxLength),
			[.. x.Errors],
			[.. x.Warnings]))
			.ToList();

		if (rowEntities.Count > 0)
		{
			await attendanceImportRowRepository.InsertManyAsync(rowEntities, autoSave: true);
		}

		return importBatch;
	}

	private static string NormalizeImportFileName(string? value) =>
		(value ?? string.Empty).Trim().ToLowerInvariant();

	private static string BuildOriginalFileBlobName(Guid batchId, string fileName)
	{
		var safeFileName = string.Join("_", (fileName ?? "attendance-import").Split(System.IO.Path.GetInvalidFileNameChars()));
		if (string.IsNullOrWhiteSpace(safeFileName))
		{
			safeFileName = "attendance-import";
		}

		return $"hr/attendance-imports/{batchId:N}/original/{safeFileName}";
	}

	private static string NormalizeContentType(string? contentType) =>
		string.IsNullOrWhiteSpace(contentType)
			? "application/octet-stream"
			: AttendanceImportPreviewMapper.TruncateForStorage(contentType, 100)!;

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

	private string BuildImportedByName()
	{
		var fullName = string.Join(" ",
			(currentUser.Name ?? string.Empty).Trim(),
			(currentUser.SurName ?? string.Empty).Trim()).Trim();

		if (!string.IsNullOrWhiteSpace(fullName))
		{
			return fullName;
		}

		if (!string.IsNullOrWhiteSpace(currentUser.UserName))
		{
			return currentUser.UserName!;
		}

		return currentUser.Id?.ToString() ?? "unknown-user";
	}
}
