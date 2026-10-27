using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportJobArgs
{
	public Guid JobId { get; set; }
	public Guid TenantId { get; set; }
}

public class InstituteReportExportJob(IInstituteReportJobRepository jobs, ICurrentTenant tenant,
	IUnitOfWorkManager units, InstituteReportData data, InstituteReportFiles files, InstituteReportAccess access,
	ICurrentPrincipalAccessor principal, IUserScopeService scopes,
	IBlobContainer blobs, ILogger<InstituteReportExportJob> logger) : AsyncBackgroundJob<InstituteReportJobArgs>, ITransientDependency
{
	[UnitOfWork(IsDisabled = true)]
	public override async Task ExecuteAsync(InstituteReportJobArgs args)
	{
		using var school = tenant.Change(args.TenantId);
		Guid user;
		var lease = Guid.NewGuid();
		InstituteReportRequest request;
		using (var start = units.Begin(requiresNew: true, isTransactional: true))
		{
			if (!await jobs.TryAcquireAsync(args.JobId, lease, DateTime.UtcNow)) return;
			var job = await jobs.GetAsync(args.JobId);
			user = job.RequestedBy; request = InstituteReportAppService.Read(job);
			await start.CompleteAsync();
		}
		string? blob = null;
		using var heartbeatCancellation = new CancellationTokenSource();
		var heartbeat = RenewLeaseAsync(args, lease, heartbeatCancellation.Token);
		try
		{
			using var work = units.Begin(requiresNew: true, isTransactional: false);
			using var actor = principal.Change(await access.PrincipalAsync(user, refresh: true));
			await access.EnsureAsync(user, request.ReportType, generate: true);
			var scopeHash = InstituteReportFiles.ScopeHash(await scopes.GetUserScopeAsync(user));
			var report = await data.GenerateAsync(request, user, all: true);
			var bytes = await files.CreateAsync(report, request);
			await access.EnsureAsync(user, request.ReportType, generate: true, refresh: true);
			if (scopeHash != InstituteReportFiles.ScopeHash(await scopes.GetUserScopeAsync(user))) throw new InvalidOperationException("Report scope changed during generation.");
			blob = $"institute-reports/{args.TenantId:N}/{args.JobId:N}/{scopeHash}/{Guid.NewGuid():N}";
			await blobs.SaveAsync(blob, bytes, overrideExisting: false);
			if (!await jobs.CompleteAsync(args.JobId, lease, blob, DateTime.UtcNow))
				throw new InvalidOperationException("Report generation lease was reassigned.");
			await work.CompleteAsync();
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Institute report generation failed for {JobId}", args.JobId);
			using var failure = units.Begin(requiresNew: true, isTransactional: true);
			var current = await jobs.GetAsync(args.JobId);
			if (current.Status == "Completed" && current.BlobName == blob) return;
			if (blob != null) await blobs.DeleteAsync(blob);
			await jobs.FailAsync(args.JobId, lease, DateTime.UtcNow);
			await failure.CompleteAsync();
		}
		finally
		{
			heartbeatCancellation.Cancel();
			await heartbeat;
		}
	}
	private async Task RenewLeaseAsync(InstituteReportJobArgs args, Guid lease, CancellationToken cancellation)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
			while (await timer.WaitForNextTickAsync(cancellation))
			{
				using var school = tenant.Change(args.TenantId);
				using var renewal = units.Begin(requiresNew: true, isTransactional: true);
				if (!await jobs.RenewAsync(args.JobId, lease, DateTime.UtcNow)) return;
				await renewal.CompleteAsync();
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
		catch (Exception exception) { logger.LogWarning(exception, "Report processing lease could not be renewed for {JobId}", args.JobId); }
	}
}
