using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrTableExportJobArgs
{
	public Guid RunId { get; set; }
	public Guid? TenantId { get; set; }
	public Guid AttemptId { get; set; }
}

public class HrTableExportJob(IRepository<HrReportRun, Guid> runs, IRepository<HrReportRunPage, Guid> pages,
	HrTableExportSources sources, HrTableExportAccess access, HrReportingAccess principals, ICurrentPrincipalAccessor principal,
	IHrReportingRunLock runLock, HrReportingRunCancellation cancellation, IBlobContainer blobs, ICurrentTenant tenant,
	IUnitOfWorkManager uows, IClock clock, ITenantRepository tenants, IRepository<PayslipSettings, Guid> settings,
	IAsyncQueryableExecuter executor, ILogger<HrTableExportJob> logger, PayrollDocumentSchoolService school)
	: AsyncBackgroundJob<HrTableExportJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(HrTableExportJobArgs args)
	{
		using var changeTenant = tenant.Change(args.TenantId);
		try
		{
			using (var claim = uows.Begin(requiresNew: true, isTransactional: true))
			{
				await runLock.LockAsync(args.RunId, args.TenantId);
				var run = await runs.FindAsync(args.RunId);
				if (run == null || !run.DataSource.StartsWith("table:", StringComparison.Ordinal) || !run.Start(args.AttemptId, clock.Now)) return;
				await runs.UpdateAsync(run, true); await claim.CompleteAsync();
			}
			using var capture = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }, requiresNew: true);
			var current = await runs.GetAsync(args.RunId);
			using var actor = principal.Change(await principals.PrincipalAsync(current.OwnerUserId));
			await access.EnsureAsync(current);
			var request = HrTableExportAppService.Request(current);
			var result = new HrReportingResult { Columns = await sources.ColumnsAsync(request) };
			var staff = new HashSet<Guid>();
			var parameters = JsonSerializer.Deserialize<HrReportParametersDto>(current.ParametersJson, HrReportingEngine.Json)!;
			var schoolToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(current.CapturedAt, TimeZoneInfo.FindSystemTimeZoneById(parameters.TimeZone)).DateTime);
			var codes = new HashSet<string>();
			var offset = 0;
			while (true)
			{
				await cancellation.EnsureActiveAsync(current);
				var page = await sources.ReadAsync(request, offset, 100);
				if (page.Total > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
				foreach (var item in page.Rows)
				{
					result.Rows.Add(request.Columns.ToDictionary(x => x.Id, x => {
						var value = HrTableExportProjection.Value(request.Table, x.Id, item, schoolToday);
						return result.Columns.Single(c => c.Id == x.Id).Type == "date" && value is string text && text.Length >= 10 && DateOnly.TryParse(text[..10], out var date) ? date : value;
					}));
					var id = item.TryGetProperty("employeeId", out var employee) ? employee : request.Table == "employee-table" && item.TryGetProperty("id", out employee) ? employee : default;
					if (id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var employeeId)) staff.Add(employeeId);
					if (request.Table == "hr-salary-documents" && item.TryGetProperty("employeeCode", out var code) && !string.IsNullOrWhiteSpace(code.GetString())) codes.Add(code.GetString()!);
				}
				offset = page.NextOffset ?? offset + page.Rows.Count;
				if (offset >= page.Total) break;
				if ((page.Rows.Count == 0 && !page.NextOffset.HasValue) || offset > HrReportingEngine.MaximumPopulation) throw HrReportingEngine.Invalid("The table source returned an incomplete population. Retry the export.");
			}
			result.EmployeeIds = staff.ToList();
			result.Totals = await sources.TotalsAsync(request, result);
			result.EmployeeCodes = codes.ToList();
			var identity = await executor.FirstOrDefaultAsync(await settings.GetQueryableAsync());
			result.SchoolName = identity?.SchoolName ?? (await tenants.GetAsync(args.TenantId!.Value)).Name;
			result.SchoolAddress = identity?.SchoolAddress ?? "";
			if (request.Table == "hr-salary-documents")
			{
				var details = await school.ReadAsync();
				result.SchoolName = details.Name;
				result.SchoolAddress = details.Address;
			}
			await cancellation.EnsureActiveAsync(current);
			current.Complete(args.AttemptId, result.Rows.Count, JsonSerializer.Serialize(new HrReportingResult {
				Columns = result.Columns, Totals = result.Totals, EmployeeIds = result.EmployeeIds, EmployeeCodes = result.EmployeeCodes, SchoolName = result.SchoolName, SchoolAddress = result.SchoolAddress
			}, HrReportingEngine.Json));
			var bytes = request.Format == "pdf" ? HrReportingDocumentWriter.Pdf(current, result) : HrReportingDocumentWriter.Excel(current, result);
			foreach (var (chunk, index) in result.Rows.Chunk(200).Select((chunk, index) => (chunk, index)))
				await pages.InsertAsync(new HrReportRunPage(Guid.NewGuid(), current.TenantId, current.Id, index, JsonSerializer.Serialize(chunk, HrReportingEngine.Json), current.ExpiresAt));
			result.Rows = [];
			// Complete metadata and bytes publish atomically through the same database unit of work.
			await blobs.SaveAsync(HrReportingWorkspaceAppService.BlobName(current, request.Format), bytes, overrideExisting: false);
			current.AttachExport(request.Format);
			await runs.UpdateAsync(current, true); await capture.CompleteAsync();
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "HR table export {RunId} failed", args.RunId);
			using var failed = uows.Begin(requiresNew: true, isTransactional: true);
			var run = await runs.FindAsync(args.RunId);
			if (run != null)
			{
				var message = exception is BusinessException business && business.Code == HRErrorCodes.ReportDesignInvalid ? business.Data["errors"]?.ToString()
					: exception is BusinessException tooLarge && tooLarge.Code == HRErrorCodes.ReportPopulationTooLarge ? "The table exceeds 500,000 rows. Narrow the filters and export again."
					: "The table could not be exported. Retry the export.";
				run.Fail(args.AttemptId, message); await runs.UpdateAsync(run, true);
			}
			await failed.CompleteAsync();
		}
	}
}
