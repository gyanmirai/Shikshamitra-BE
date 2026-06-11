using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportLogAppService(
	AttendanceImportLogReader reader,
	AttendanceImportDownloadService downloadService,
	AttendanceImportRowsSaveWorkflow rowsSaveWorkflow,
	AttendanceImportBatchStateWorkflow batchStateWorkflow)
	: ApplicationService, IAttendanceImportLogAppService
{
	public Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(GetAttendanceImportLogListInput input) =>
		reader.GetListAsync(input);

	public Task<AttendanceImportLogDetailDto> GetAsync(Guid importId) =>
		reader.GetAsync(importId);

	public Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId, GetAttendanceImportRowsInput input) =>
		reader.GetRowsAsync(importId, input);

	public Task<AttendanceImportDownloadDto> DownloadFileAsync(Guid importId) =>
		downloadService.DownloadFileAsync(importId);

	public Task<AttendanceImportDownloadDto> DownloadErrorReportAsync(Guid importId) =>
		downloadService.DownloadErrorReportAsync(importId);

	public Task<AttendanceImportRowsSaveOutput> SaveRowsAsync(Guid importId, AttendanceImportRowsSaveInput input) =>
		rowsSaveWorkflow.SaveRowsAsync(importId, input);

	public Task LockBatchAsync(Guid importId) =>
		batchStateWorkflow.LockBatchAsync(importId);

	public Task UnlockBatchAsync(Guid importId) =>
		batchStateWorkflow.UnlockBatchAsync(importId);
}
