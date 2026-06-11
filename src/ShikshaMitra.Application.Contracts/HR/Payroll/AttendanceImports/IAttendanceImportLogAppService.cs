using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public interface IAttendanceImportLogAppService : IApplicationService
{
	Task<PagedResultDto<AttendanceImportLogListItemDto>> GetListAsync(GetAttendanceImportLogListInput input);
	Task<AttendanceImportLogDetailDto> GetAsync(Guid importId);
	Task<PagedResultDto<AttendanceImportRowDto>> GetRowsAsync(Guid importId, GetAttendanceImportRowsInput input);
	Task<AttendanceImportDownloadDto> DownloadFileAsync(Guid importId);
	Task<AttendanceImportDownloadDto> DownloadErrorReportAsync(Guid importId);
	Task<AttendanceImportRowsSaveOutput> SaveRowsAsync(Guid importId, AttendanceImportRowsSaveInput input);
	Task LockBatchAsync(Guid importId);
	Task UnlockBatchAsync(Guid importId);
}
