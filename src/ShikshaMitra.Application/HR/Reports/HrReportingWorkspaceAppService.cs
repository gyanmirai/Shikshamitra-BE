using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.View)]
public class HrReportingWorkspaceAppService(
	IRepository<HrReportRun, Guid> runs, IRepository<HrReportRunPage, Guid> pages,
	HrReportingTemplateService templates, HrReportingAccess access, HrReportingDataReader reader,
	IUserScopeService scopes, HrReportExportScopeAuthorizationService scopeAuthorization, HrReportingCapturedAccess capturedAccess,
	IBackgroundJobManager jobs, IBlobContainer blobs, HrAuditWriter audit,
	HrReportingTimeZone reportingTimeZone, IRepository<AcademicYear, Guid> years,
	IRepository<ProfessionalReviewCycle, Guid> cycles, IRepository<EducationMediumStage, Guid> sections, HrUserScopeAccessService hrScope,
	ProfessionalManagementReadScopeService reviewScope, IRepository<PerformanceReviewAssignment, Guid> assignments)
	: ApplicationService, IHrReportingWorkspaceAppService
{
	public async Task<List<HrReportDatasetDto>> GetCatalogueAsync()
	{
		var result = new List<HrReportDatasetDto>();
		foreach (var d in HrReportingCatalogue.Datasets()) if (await access.CanReadAsync(d.Id, UserId())) { if (d.Id == "performance" && !await access.CanReadConfidentialAsync(UserId())) d.Fields.RemoveAll(x => x.Id is "finalScore" or "finalRating"); result.Add(d); }
		return result;
	}
	public async Task<HrReportWorkspaceOptionsDto> GetOptionsAsync(string dataset, string search = "")
	{
		await access.EnsureAsync(dataset, UserId());
		var scope = await scopes.GetUserScopeAsync(UserId());
		var staff = await reader.EmployeesAsync(dataset, new HrReportParametersDto(), scope);
		var departments = await AsyncExecuter.ToListAsync(staff.Where(x => x.Department != null && x.Department != "").Select(x => x.Department!).Distinct().OrderBy(x => x));
		var yearQuery = await years.GetQueryableAsync();
		if (!scope.IsAdmin && !scope.HasWildcardAcademicYear) { var ids = scope.AcademicYearIds.ToArray(); yearQuery = yearQuery.Where(x => Enumerable.Contains(ids, x.Id)); }
		var academicYears = await AsyncExecuter.ToListAsync(yearQuery.OrderByDescending(x => x.StartDate).Select(x => new { x.Id, x.Name }));
		var result = new HrReportWorkspaceOptionsDto
		{
			Departments = departments.Select(x => new HrReportOptionDto { Id = x, Label = x }).ToList(),
			AcademicYears = academicYears.Select(x => new HrReportOptionDto { Id = x.Id.ToString(), Label = x.Name }).ToList()
		};
		if (search.Length > 128) throw HrReportingEngine.Invalid("Search is too long.");
		if (!string.IsNullOrWhiteSpace(search)) staff = staff.Where(x => x.EmployeeCode.Contains(search) || (x.FirstName != null && x.FirstName.Contains(search)) || (x.LastName != null && x.LastName.Contains(search)));
		result.Staff = (await AsyncExecuter.ToListAsync(staff.OrderBy(x => x.EmployeeCode).Take(50).Select(x => new { x.Id, x.EmployeeCode, x.FirstName, x.LastName })))
			.Select(x => new HrReportOptionDto { Id = x.Id.ToString(), Label = $"{x.FirstName} {x.LastName} ({x.EmployeeCode})" }).ToList();
		result.StaffGroups = Enum.GetValues<EmployeeGroupType>().Select(x => new HrReportOptionDto { Id = ((int) x).ToString(), Label = x.ToString() }).ToList();
		var sectionQuery = await sections.GetQueryableAsync();
		if (!HrUserScopeAccess.HasHrWideAccess(scope)) { var ids = (await hrScope.ResolveAccessibleStageIdsAsync(scope)).ToArray(); sectionQuery = sectionQuery.Where(x => Enumerable.Contains(ids, x.Id)); }
		result.Sections = (await AsyncExecuter.ToListAsync(sectionQuery.Select(x => new { x.Id, x.Name }))).Select(x => new HrReportOptionDto { Id = x.Id.ToString(), Label = x.Name }).ToList();
		if (dataset == "performance")
		{
			var reviews = await reviewScope.ApplyAsync(await assignments.GetQueryableAsync());
			var scopedStaff = await reader.EmployeesAsync(dataset, new HrReportParametersDto(), scope);
			var cycleIds = reviews.Where(x => scopedStaff.Select(e => e.Id).Contains(x.EmployeeId)).Select(x => x.CycleId);
			result.Cycles = (await AsyncExecuter.ToListAsync((await cycles.GetQueryableAsync()).Where(x => cycleIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }))).Select(x => new HrReportOptionDto { Id = x.Id.ToString(), Label = x.Name }).ToList();
		}
		return result;
	}
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Build)]
	public async Task<HrReportingPreviewDto> PreviewAsync(HrReportDesignerPreviewDto input)
	{
		HrReportingEngine.Validate(input.Definition);
		await access.EnsureDesignAsync(input.Definition, UserId());
		await ResolveAcademicYearPeriodAsync(input.Definition, input.Parameters);
		var timezone = await reportingTimeZone.GetAsync();
		var p = HrReportingEngine.ResolveParameters(input.Definition, input.Parameters, Clock.Now, timezone);
		var scope = await scopes.GetUserScopeAsync(UserId());
		var rows = await reader.ReadAsync(input.Definition, p, scope, Clock.Now, timezone);
		var result = HrReportingEngine.Execute(input.Definition, p, rows.Rows);
		result.Warnings.AddRange(rows.Warnings);
		return new() { TimeZone = timezone, Columns = result.Columns, Rows = result.Rows.Take(50).ToList(), TotalCount = result.Rows.Count, Totals = result.Totals, Chart = result.Chart, Warnings = ["Preview uses current data. Generate a report to capture a fixed result.", .. result.Warnings] };
	}
	public async Task<HrReportRunDto> CreateAsync(HrReportCreateRunDto input)
	{
		var template = await templates.ResolveAsync(input.TemplateId, input.Revision);
		return await QueueAsync(template.Name, template.Id, template.Revision, template.Definition, input.Parameters);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Build)]
	public Task<HrReportRunDto> CapturePreviewAsync(HrReportDesignerPreviewDto input) =>
		QueueAsync("Report preview", "preview-export", 0, input.Definition, input.Parameters);
	private async Task<HrReportRunDto> QueueAsync(string name, string templateId, int revision, HrReportDesignDto definition, HrReportParametersDto parameters)
	{
		await access.EnsureDesignAsync(definition, UserId());
		await ResolveAcademicYearPeriodAsync(definition, parameters);
		var p = HrReportingEngine.ResolveParameters(definition, parameters, Clock.Now, await reportingTimeZone.GetAsync());
		var scope = await scopeAuthorization.CaptureAsync(UserId(), CurrentTenant.Id);
		var run = new HrReportRun(GuidGenerator.Create(), CurrentTenant.Id, UserId(), name, templateId, revision, definition.DataSource,
			JsonSerializer.Serialize(definition, HrReportingEngine.Json), JsonSerializer.Serialize(p, HrReportingEngine.Json), JsonSerializer.Serialize(scope), Clock.Now);
		if (PayrollIssuedReportBoundary.IsSalaryDocument(definition.DataSource)) run.ExtraProperties["issuedSalaryDocumentsOnly"] = true;
		await runs.InsertAsync(run, true);
		await jobs.EnqueueAsync(new HrReportingRunJobArgs { RunId = run.Id, TenantId = run.TenantId, AttemptId = run.AttemptId });
		await audit.WriteAsync(null, "hr_report_run", run.Id, "HR report", "report_queued", name);
		return Map(run);
	}
	private async Task ResolveAcademicYearPeriodAsync(HrReportDesignDto design, HrReportParametersDto p)
	{
		if (p != null && string.IsNullOrWhiteSpace(p.Period)) p.Period = design.DefaultPeriod;
		if (p?.Period != "academicYear") return;
		if (!design.Parameters.Contains("academicYearId") || !p.AcademicYearId.HasValue) throw HrReportingEngine.Invalid("Choose an academic year supported by this template.");
		var scope = await scopes.GetUserScopeAsync(UserId());
		if (!scope.IsAdmin && !scope.HasWildcardAcademicYear && !scope.AcademicYearIds.Contains(p.AcademicYearId.Value)) throw new AbpAuthorizationException();
		var year = await years.GetAsync(p.AcademicYearId.Value);
		p.FromDate = DateOnly.FromDateTime(year.StartDate); p.ToDate = DateOnly.FromDateTime(year.EndDate);
	}
	public async Task<HrReportRunHistoryDto> GetRunsAsync(int skip = 0, int take = 50, HrReportTableQueryDto? criteria = null)
	{
		var user = UserId();
		take = Math.Clamp(take, 1, 100); skip = Math.Max(0, skip);
		var table = new HrReportingTableQuery(criteria, HrReportingHistoryTable.Columns, await reportingTimeZone.GetAsync());
		var query = (await runs.GetQueryableAsync()).Where(x => x.OwnerUserId == user && !x.DataSource.StartsWith("table:") && x.TemplateId != "preview-export").OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id);
		if (criteria?.HasCriteria != true)
		{
			var visible = new List<HrReportRunDto>(); var seen = 0;
			for (var offset = 0; ; offset += 100)
			{
				var batch = await AsyncExecuter.ToListAsync(query.Skip(offset).Take(100));
				foreach (var run in batch)
					if (await CanReadRunAsync(run)) {
						if (seen++ < skip) continue;
						visible.Add(Map(run));
						if (visible.Count > take) return new() { Items = visible.Take(take).ToList(), HasMore = true };
					}
				if (batch.Count < 100) return new() { Items = visible, HasMore = false };
			}
		}
		var candidates = await AsyncExecuter.ToListAsync(query.Take(HrReportingEngine.MaximumPopulation + 1));
		if (candidates.Count > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
		var mapped = candidates.Select(run => (Run: run, Dto: Map(run))).ToList();
		var result = new List<HrReportRunDto>();
		var accessible = 0;
		foreach (var item in table.Apply(mapped, item => HrReportingHistoryTable.Values(item.Dto)))
			if (await CanReadRunAsync(item.Run))
			{
				if (accessible++ < skip) continue;
				result.Add(item.Dto);
				if (result.Count > take) break;
			}
		return new() { Items = result.Take(take).ToList(), HasMore = result.Count > take };
	}
	private async Task<bool> CanReadRunAsync(HrReportRun run)
	{
		if (!await access.CanReadAsync(run.DataSource, UserId())) return false;
		try { await EnsureAuthorizedAsync(run); return true; }
		catch (BusinessException) { return false; }
		catch (AbpAuthorizationException) { return false; }
	}
	public async Task<HrReportRunDto> GetAsync(Guid id) => Map(await AuthorizedAsync(id));
	public async Task<HrReportResultPageDto> GetRowsAsync(Guid id, int page = 1, int pageSize = 50, HrReportTableQueryDto? criteria = null)
	{
		var run = await AuthorizedAsync(id); EnsureReady(run);
		var metadata = Map(run);
		var table = new HrReportingTableQuery(criteria, metadata.Columns, metadata.Parameters.TimeZone);
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
		var start = (long)(page - 1) * pageSize;
		if (start > int.MaxValue - pageSize) throw HrReportingEngine.Invalid("Page is outside this result.");
		if (criteria?.HasCriteria == true)
		{
			var matching = table.Apply(await ReadCapturedRowsAsync(id), row => row).ToList();
			return new() { TotalCount = matching.Count, Page = page, PageSize = pageSize, Rows = matching.Skip((int)start).Take(pageSize).ToList() };
		}
		var firstPage = (int)start / 200; var lastPage = ((int)start + pageSize - 1) / 200;
		var stored = await AsyncExecuter.ToListAsync((await pages.GetQueryableAsync()).Where(x => x.RunId == id && x.PageNumber >= firstPage && x.PageNumber <= lastPage).OrderBy(x => x.PageNumber));
		return new() { TotalCount = run.RowCount, Page = page, PageSize = pageSize, Rows = stored.SelectMany(x => JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(x.RowsJson, HrReportingEngine.Json)!).Skip((int)start % 200).Take(pageSize).ToList() };
	}
	// Internal application-service contract only. The HTTP controller never exposes this bulk reader.
	public async Task<List<Dictionary<string, object?>>> GetExportRowsAsync(Guid id, HrReportTableQueryDto criteria)
	{
		var run = await AuthorizedAsync(id); EnsureReady(run);
		var metadata = Map(run);
		var table = new HrReportingTableQuery(criteria, metadata.Columns, metadata.Parameters.TimeZone);
		return table.Apply(await ReadCapturedRowsAsync(id), row => row).ToList();
	}
	private async Task<List<Dictionary<string, object?>>> ReadCapturedRowsAsync(Guid id)
	{
		var result = new List<Dictionary<string, object?>>();
		var query = (await pages.GetQueryableAsync()).Where(x => x.RunId == id);
		// Bound stored JSON residency while assembling the same maximum population as generation.
		for (var offset = 0; ; offset += 25)
		{
			var chunk = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.PageNumber).Skip(offset).Take(25));
			foreach (var stored in chunk) result.AddRange(JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(stored.RowsJson, HrReportingEngine.Json)!);
			if (result.Count > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
			if (chunk.Count < 25) break;
		}
		return result;
	}
	public async Task CancelAsync(Guid id)
	{
		var run = await AuthorizedAsync(id); run.Cancel(); await runs.UpdateAsync(run, true);
		await audit.WriteAsync(null, "hr_report_run", id, "HR report", "report_cancelled", run.Name);
	}
	public async Task<HrReportRunDto> RetryAsync(Guid id)
	{
		var run = await AuthorizedAsync(id);
		if (run.Status is not ("Failed" or "Cancelled")) throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
		var template = await templates.ResolveAsync(run.TemplateId, run.Revision);
		var p = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!;
		if (p.FromDate.HasValue) p.Period = "custom";
		return await QueueAsync(template.Name, template.Id, template.Revision, template.Definition, p);
	}
	public async Task CreateExportAsync(Guid id, string format)
	{
		var run = await AuthorizedAsync(id); EnsureReady(run); EnsureFormat(format);
		if ((format == "excel" && run.ExcelReady) || (format == "pdf" && run.PdfReady)) return;
		if (!run.QueueExport(format, Clock.Now)) return;
		await runs.UpdateAsync(run, true);
		await jobs.EnqueueAsync(new HrReportingRenderJobArgs { RunId = run.Id, TenantId = run.TenantId, Format = format });
	}
	public async Task<HrReportDownloadDto> DownloadAsync(Guid id, string format)
	{
		var run = await AuthorizedAsync(id); EnsureReady(run); EnsureFormat(format);
		if (!(format == "excel" ? run.ExcelReady : run.PdfReady)) throw new BusinessException(HRErrorCodes.ReportExportNotReady);
		return new()
		{
			Bytes = await blobs.GetAllBytesAsync(BlobName(run, format)),
			ContentType = format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			FileName = $"HR-report-{run.CapturedAt:yyyyMMdd}-{run.Id.ToString()[..8]}.{(format == "pdf" ? "pdf" : "xlsx")}"
		};
	}
	internal async Task<HrReportRun> AuthorizedAsync(Guid id)
	{
		var run = await runs.GetAsync(id);
		await EnsureAuthorizedAsync(run);
		return run;
	}
	private async Task EnsureAuthorizedAsync(HrReportRun run)
	{
		if (run.TenantId != CurrentTenant.Id || run.OwnerUserId != UserId()) throw new AbpAuthorizationException();
		await capturedAccess.EnsureAsync(run);
		run.RecoverInterruptedWork(Clock.Now);
	}
	internal static string BlobName(HrReportRun run, string format) => $"hr-report-runs/{run.TenantId:N}/{run.Id:N}.{format}";
	private Guid UserId() => CurrentUser.Id ?? throw new AbpAuthorizationException();
	private void EnsureReady(HrReportRun run)
	{
		if (run.ExpiresAt <= Clock.Now) throw new BusinessException(HRErrorCodes.ReportResultExpired);
		if (run.Status != "Completed") throw new BusinessException(HRErrorCodes.ReportExportNotReady);
	}
	private static void EnsureFormat(string format) { if (format is not ("pdf" or "excel")) throw HrReportingEngine.Invalid("Choose Excel or PDF."); }
	internal HrReportRunDto Map(HrReportRun run)
	{
		var result = JsonSerializer.Deserialize<HrReportingResult>(run.ResultJson, HrReportingEngine.Json)!;
		foreach (var format in new[] { "excel", "pdf" }) if ((format == "excel" ? run.ExcelStatus : run.PdfStatus) == "Failed" && run.ExtraProperties.TryGetValue("exportError_" + format, out var message)) result.Warnings.Add(message?.ToString() ?? "The report file could not be prepared.");
		return new()
		{
			Id = run.Id,
			Name = run.Name,
			TemplateId = run.TemplateId,
			Revision = run.Revision,
			DataSource = run.DataSource,
			Status = run.Status,
			CapturedAt = run.CapturedAt,
			ExpiresAt = run.ExpiresAt,
			RowCount = run.RowCount,
			ErrorMessage = run.ErrorMessage,
			IsExpired = run.ExpiresAt <= Clock.Now,
			ExcelReady = run.ExcelReady,
			PdfReady = run.PdfReady,
			ExcelStatus = run.ExcelStatus,
			PdfStatus = run.PdfStatus,
			Parameters = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!,
			Columns = result.Columns,
			Totals = result.Totals,
			Warnings = result.Warnings,
			Chart = result.Chart
		};
	}
}
