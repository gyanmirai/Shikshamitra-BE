using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
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

public class InstituteReportExportJob(IRepository<InstituteReportJob, Guid> jobs, ICurrentTenant tenant,
	IUnitOfWorkManager units, InstituteReportData data, InstituteReportFiles files, InstituteReportAccess access,
	HrReportingAccess identities, ICurrentPrincipalAccessor principal, IUserScopeService scopes,
	IBlobContainer blobs, ILogger<InstituteReportExportJob> logger) : AsyncBackgroundJob<InstituteReportJobArgs>, ITransientDependency
{
	[UnitOfWork(IsDisabled = true)]
	public override async Task ExecuteAsync(InstituteReportJobArgs args)
	{
		using var school = tenant.Change(args.TenantId);
		Guid user;
		InstituteReportRequest request;
		using (var start = units.Begin(requiresNew: true, isTransactional: true))
		{
			var job = await jobs.FindAsync(x => x.Id == args.JobId && x.TenantId == args.TenantId);
			if (job == null || job.Status != "Queued" || job.ExpiresAt <= DateTime.UtcNow) return;
			job.Start(); user = job.RequestedBy; request = InstituteReportAppService.Read(job);
			await jobs.UpdateAsync(job, autoSave: true);
			await start.CompleteAsync();
		}
		string? blob = null;
		try
		{
			using var work = units.Begin(requiresNew: true, isTransactional: false);
			using var actor = principal.Change(await identities.PrincipalAsync(user));
			await access.EnsureAsync(user, request.ReportType, generate: true);
			var scopeHash = InstituteReportFiles.ScopeHash(await scopes.GetUserScopeAsync(user));
			var report = await data.GenerateAsync(request, user, all: true);
			var bytes = await files.CreateAsync(report, request);
			await access.EnsureAsync(user, request.ReportType, generate: true);
			if (scopeHash != InstituteReportFiles.ScopeHash(await scopes.GetUserScopeAsync(user))) throw new InvalidOperationException("Report scope changed during generation.");
			blob = $"institute-reports/{args.TenantId:N}/{args.JobId:N}/{scopeHash}/{Guid.NewGuid():N}";
			await blobs.SaveAsync(blob, bytes, overrideExisting: false);
			var job = await jobs.GetAsync(args.JobId);
			job.Complete(blob);
			await jobs.UpdateAsync(job, autoSave: true);
			await work.CompleteAsync();
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Institute report generation failed for {JobId}", args.JobId);
			using var failure = units.Begin(requiresNew: true, isTransactional: true);
			var job = await jobs.GetAsync(args.JobId);
			if (job.Status == "Completed") return;
			if (blob != null) await blobs.DeleteAsync(blob);
			job.Fail();
			await jobs.UpdateAsync(job, autoSave: true);
			await failure.CompleteAsync();
		}
	}
}
