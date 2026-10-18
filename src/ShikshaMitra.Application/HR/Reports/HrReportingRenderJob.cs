using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.HR.Reports;

public class HrReportingRenderJobArgs
{
	public Guid RunId { get; set; }
	public Guid? TenantId { get; set; }
	public string Format { get; set; } = "excel";
}

public class HrReportingRenderJob(IRepository<HrReportRun, Guid> runs, IRepository<HrReportRunPage, Guid> pages,
	HrReportingAccess access, HrReportingCapturedAccess capturedAccess, IHrReportingRunLock runLock,
	IBlobContainer blobs, ICurrentTenant tenant, ICurrentPrincipalAccessor principal, IPermissionChecker permissions,
	IUnitOfWorkManager uows, IAsyncQueryableExecuter executor, IClock clock, ILogger<HrReportingRenderJob> logger)
	: AsyncBackgroundJob<HrReportingRenderJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(HrReportingRenderJobArgs args)
	{
		using var changeTenant = tenant.Change(args.TenantId);
		try
		{
			using var uow = uows.Begin(requiresNew: true, isTransactional: true);
			// Serialize writers before reading Ready, so duplicate deliveries cannot overwrite published bytes.
			await runLock.LockAsync(args.RunId, args.TenantId);
			var run = await runs.FindAsync(args.RunId);
			if (run == null || run.Status != "Completed" || run.ExpiresAt <= clock.Now || args.Format is not ("excel" or "pdf")) return;
			if (args.Format == "excel" ? run.ExcelReady : run.PdfReady) return;
			var actorPrincipal = await access.PrincipalAsync(run.OwnerUserId);
			using var actor = principal.Change(actorPrincipal);
			await access.EnsureDesignAsync(JsonSerializer.Deserialize<HrReportDesignDto>(run.DefinitionJson, HrReportingEngine.Json)!, run.OwnerUserId);
			if (!await permissions.IsGrantedAsync(actorPrincipal, ShikshaMitraSubmodulePermissions.HR.Reports.Export)) throw new Volo.Abp.Authorization.AbpAuthorizationException();
			await capturedAccess.EnsureAsync(run);
			var result = JsonSerializer.Deserialize<HrReportingResult>(run.ResultJson, HrReportingEngine.Json)!;
			var stored = await executor.ToListAsync((await pages.GetQueryableAsync()).Where(x => x.RunId == run.Id).OrderBy(x => x.PageNumber));
			result.Rows = stored.SelectMany(x => JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(x.RowsJson, HrReportingEngine.Json)!).ToList();
			var bytes = args.Format == "excel" ? HrReportingDocumentWriter.Excel(run, result) : HrReportingDocumentWriter.Pdf(run, result);
			if (run.ExpiresAt <= clock.Now) throw HrReportingEngine.Invalid("This result expired during file preparation. Generate a new report.");
			await blobs.SaveAsync(HrReportingWorkspaceAppService.BlobName(run, args.Format), bytes, overrideExisting: true);
			run.AttachExport(args.Format); await runs.UpdateAsync(run, true); await uow.CompleteAsync();
		}
		catch (Volo.Abp.Data.AbpDbConcurrencyException)
		{
			// Independent Excel/PDF jobs may finish together. ABP retries from the same captured rows.
			throw;
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "HR report file {RunId} {Format} failed", args.RunId, args.Format);
			using var failed = uows.Begin(requiresNew: true, isTransactional: true);
			var run = await runs.FindAsync(args.RunId);
			if (run != null)
			{
				var message = exception is Volo.Abp.BusinessException business && business.Code == HRErrorCodes.ReportDesignInvalid
					? business.Data["errors"]?.ToString() : "The report file could not be prepared. Retry the download.";
				run.FailExport(args.Format, message);
				if (run.ExpiresAt <= clock.Now) await blobs.DeleteAsync(HrReportingWorkspaceAppService.BlobName(run, args.Format));
				await runs.UpdateAsync(run, true);
			}
			await failed.CompleteAsync();
		}
	}
}
