using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public class HrTableExportRequest
{
	[Required, MaxLength(100)] public string Table { get; set; } = "";
	public JsonElement Query { get; set; }
	[Required, MinLength(1), MaxLength(60)] public List<HrTableExportColumn> Columns { get; set; } = [];
	[Required, RegularExpression("^(excel|pdf)$")] public string Format { get; set; } = "excel";
}
public class HrTableExportColumn
{
	[Required, MaxLength(100)] public string Id { get; set; } = "";
	[Required, MaxLength(128)] public string Label { get; set; } = "";
}
public class HrTableExportStatus
{
	public string Name { get; set; } = "";
	public Guid Id { get; set; }
	public string Status { get; set; } = "Queued";
	public string? Error { get; set; }
	public int RowCount { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
}
public interface IHrTableExportAppService : IApplicationService
{
	Task<HrTableExportStatus> CreateAsync(HrTableExportRequest request);
	Task<HrTableExportStatus> GetAsync(Guid id);
	Task<HrReportDownloadDto> DownloadAsync(Guid id);
	Task CancelAsync(Guid id);
}
