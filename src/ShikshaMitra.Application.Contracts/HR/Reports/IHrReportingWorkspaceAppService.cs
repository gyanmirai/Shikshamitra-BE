using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public interface IHrReportingTemplateService : IApplicationService
{
	Task<List<HrReportLibraryTemplateDto>> GetLibraryAsync(bool edit = false);
	Task<HrReportLibraryTemplateDto> SaveAsync(Guid? id, HrReportSaveDesignDto input);
	Task ArchiveAsync(Guid id, string concurrencyStamp);
}

public interface IHrReportingWorkspaceAppService : IApplicationService
{
	Task<List<HrReportDatasetDto>> GetCatalogueAsync();
	Task<HrReportWorkspaceOptionsDto> GetOptionsAsync(string dataset, string search = "");
	Task<HrReportingPreviewDto> PreviewAsync(HrReportDesignerPreviewDto input);
	Task<HrReportRunDto> CreateAsync(HrReportCreateRunDto input);
	Task<HrReportRunHistoryDto> GetRunsAsync(int skip = 0, int take = 50);
	Task<HrReportRunDto> GetAsync(Guid id);
	Task<HrReportResultPageDto> GetRowsAsync(Guid id, int page = 1, int pageSize = 50);
	Task CancelAsync(Guid id);
	Task<HrReportRunDto> RetryAsync(Guid id);
	Task CreateExportAsync(Guid id, string format);
	Task<HrReportDownloadDto> DownloadAsync(Guid id, string format);
}

public class HrReportingPreviewDto
{
	public string TimeZone { get; set; } = "Asia/Kolkata";
	public List<HrReportFieldDto> Columns { get; set; } = [];
	public List<Dictionary<string, object?>> Rows { get; set; } = [];
	public int TotalCount { get; set; }
	public Dictionary<string, decimal?> Totals { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
	public HrReportChartPreviewDto? Chart { get; set; }
}
