using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportDownloadService(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	IBlobContainer blobContainer,
	IExcelService excelService)
	: ApplicationService, ITransientDependency
{
	private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	public async Task<AttendanceImportDownloadDto> DownloadFileAsync(Guid importId)
	{
		var batch = await GetBatchAsync(importId);
		if (!string.IsNullOrWhiteSpace(batch.OriginalFileBlobName))
		{
			await using var stream = await blobContainer.GetAsync(batch.OriginalFileBlobName);
			await using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);

			return new AttendanceImportDownloadDto
			{
				FileName = NormalizeFileName(batch.FileName, "attendance-import"),
				ContentType = string.IsNullOrWhiteSpace(batch.OriginalFileContentType)
					? "application/octet-stream"
					: batch.OriginalFileContentType,
				Content = memoryStream.ToArray()
			};
		}

		var rows = await GetRowsAsync(importId);
		return BuildExcelDownload(
			BuildDownloadFileName(batch.FileName),
			"AttendanceImport",
			BuildBaseRows(rows));
	}

	public async Task<AttendanceImportDownloadDto> DownloadErrorReportAsync(Guid importId)
	{
		var batch = await GetBatchAsync(importId);
		var rows = (await GetRowsAsync(importId))
			.Where(x => x.Errors.Count > 0)
			.ToList();

		if (rows.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid, "No error rows are available for this attendance import.");
		}

		return BuildExcelDownload(
			BuildErrorReportFileName(batch.FileName),
			"AttendanceImportErrors",
			BuildErrorRows(rows));
	}

	private async Task<AttendanceImportBatch> GetBatchAsync(Guid importId) =>
		await attendanceImportBatchRepository.FindAsync(importId)
		?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);

	private async Task<List<AttendanceImportRow>> GetRowsAsync(Guid importId)
	{
		var query = await attendanceImportRowRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => x.BatchId == importId).OrderBy(x => x.RowNumber));
	}

	private AttendanceImportDownloadDto BuildExcelDownload(string fileName, string sheetName, List<ExcelRow> rows) =>
		new()
		{
			FileName = fileName,
			ContentType = ExcelContentType,
			Content = excelService.GenerateExcelAsBytes(new ExcelDocument
			{
				OutputFileName = fileName,
				Sheets =
				[
					new ExcelSheet
					{
						SheetName = sheetName,
						Rows = rows
					}
				]
			})
		};

	private static List<ExcelRow> BuildBaseRows(IEnumerable<AttendanceImportRow> rows)
	{
		var result = new List<ExcelRow>
		{
			CreateRow("Row Number", "Attendance Date", "Employee Code", "Time In", "Time Out", "Status", "Remarks", "Employee Name", "Contact No", "Action", "Errors", "Warnings")
		};

		result.AddRange(rows.Select(row => CreateRow(
			row.RowNumber,
			row.AttendanceDate,
			row.EmployeeCode,
			row.TimeIn ?? string.Empty,
			row.TimeOut ?? string.Empty,
			row.Status ?? string.Empty,
			row.Remarks ?? string.Empty,
			row.EmployeeName ?? string.Empty,
			row.ContactNo ?? string.Empty,
			row.Action,
			string.Join("; ", row.Errors),
			string.Join("; ", row.Warnings))));

		return result;
	}

	private static List<ExcelRow> BuildErrorRows(IEnumerable<AttendanceImportRow> rows)
	{
		var result = new List<ExcelRow>
		{
			CreateRow("Row Number", "Attendance Date", "Employee Code", "Employee Name", "Contact No", "Time In", "Time Out", "Status", "Remarks", "Action", "Errors", "Warnings")
		};

		result.AddRange(rows.Select(row => CreateRow(
			row.RowNumber,
			row.AttendanceDate,
			row.EmployeeCode,
			row.EmployeeName ?? string.Empty,
			row.ContactNo ?? string.Empty,
			row.TimeIn ?? string.Empty,
			row.TimeOut ?? string.Empty,
			row.Status ?? string.Empty,
			row.Remarks ?? string.Empty,
			row.Action,
			string.Join("; ", row.Errors),
			string.Join("; ", row.Warnings))));

		return result;
	}

	private static ExcelRow CreateRow(params object?[] values) =>
		new()
		{
			Cells = values.Select(value => new ExcelCell { Value = value ?? string.Empty }).ToList()
		};

	private static string BuildDownloadFileName(string fileName) =>
		$"{Path.GetFileNameWithoutExtension(NormalizeFileName(fileName, "attendance-import"))}.xlsx";

	private static string BuildErrorReportFileName(string fileName) =>
		$"{Path.GetFileNameWithoutExtension(NormalizeFileName(fileName, "attendance-import"))}-error-report.xlsx";

	private static string NormalizeFileName(string? fileName, string fallback)
	{
		var safeName = Path.GetFileName(fileName);
		return string.IsNullOrWhiteSpace(safeName) ? fallback : safeName;
	}
}
