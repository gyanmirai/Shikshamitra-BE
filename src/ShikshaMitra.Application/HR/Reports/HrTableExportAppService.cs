using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Reports;

[Authorize, RemoteService(false)]
public class HrTableExportAppService(IRepository<HrReportRun, Guid> runs, HrTableExportSources sources,
	HrTableExportAccess access, HrReportExportScopeAuthorizationService scope, IBackgroundJobManager jobs,
	IBlobContainer blobs, HrReportingTimeZone timezone) : ApplicationService, IHrTableExportAppService
{
	public async Task<HrTableExportStatus> CreateAsync(HrTableExportRequest request)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue) throw new AbpAuthorizationException();
		HrTableExportSources.Validate(request);
		await sources.EnsureAccessAsync(request);
		var snapshot = await scope.ResolveCurrentScopeAsync(CurrentUser.Id.Value) ?? throw new AbpAuthorizationException();
		var definition = new HrReportDesignDto { DataSource = "employee", Columns = request.Columns.Select(x => new HrReportDisplayColumnDto { Field = x.Id, Label = x.Label }).ToList() };
		var presentation = await sources.PresentationAsync(request, await timezone.GetAsync());
		var run = new HrReportRun(GuidGenerator.Create(), CurrentTenant.Id, CurrentUser.Id.Value, presentation.Name,
			"table-export", 1, "table:" + request.Table, JsonSerializer.Serialize(definition, HrReportingEngine.Json),
			JsonSerializer.Serialize(presentation.Parameters, HrReportingEngine.Json), JsonSerializer.Serialize(snapshot), Clock.Now);
		run.ExtraProperties["tableExportRequest"] = JsonSerializer.Serialize(request, HrReportingEngine.Json);
		run.ExtraProperties["tableExportContext"] = presentation.Context;
		await runs.InsertAsync(run, true);
		await jobs.EnqueueAsync(new HrTableExportJobArgs { RunId = run.Id, TenantId = run.TenantId, AttemptId = run.AttemptId });
		return Status(run);
	}
	public async Task<HrTableExportStatus> GetAsync(Guid id) => Status(await AuthorizedAsync(id));
	public async Task CancelAsync(Guid id)
	{
		var run = await AuthorizedAsync(id); run.Cancel(); await runs.UpdateAsync(run, true);
	}
	public async Task<HrReportDownloadDto> DownloadAsync(Guid id)
	{
		var run = await AuthorizedAsync(id);
		var request = Request(run);
		if (run.Status != "Completed" || !(request.Format == "excel" ? run.ExcelReady : run.PdfReady)) throw new BusinessException(HRErrorCodes.ReportExportNotReady);
		return new() { Bytes = await blobs.GetAllBytesAsync(HrReportingWorkspaceAppService.BlobName(run, request.Format)),
			ContentType = request.Format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			FileName = $"{request.Table}-{run.CapturedAt:yyyyMMdd-HHmm}.{(request.Format == "pdf" ? "pdf" : "xlsx")}" };
	}
	private async Task<HrReportRun> AuthorizedAsync(Guid id)
	{
		var run = await runs.FindAsync(id);
		if (run == null || run.TenantId != CurrentTenant.Id || run.OwnerUserId != CurrentUser.Id || !run.DataSource.StartsWith("table:", StringComparison.Ordinal)) throw new AbpAuthorizationException();
		if (run.ExpiresAt <= Clock.Now) throw HrReportingEngine.Invalid("This export expired. Export the table again.");
		await access.EnsureAsync(run);
		return run;
	}
	internal static HrTableExportRequest Request(HrReportRun run) => JsonSerializer.Deserialize<HrTableExportRequest>(run.ExtraProperties["tableExportRequest"].ToString()!, HrReportingEngine.Json)!;
	internal static HrTableExportStatus Status(HrReportRun run) => new() { Id = run.Id, Name = run.Name, Status = run.Status, Error = run.ErrorMessage, RowCount = run.RowCount, ExpiresAt = run.ExpiresAt };
}
