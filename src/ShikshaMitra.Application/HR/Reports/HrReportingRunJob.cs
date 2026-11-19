using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportingRunJobArgs
{
	public Guid RunId { get; set; }
	public Guid? TenantId { get; set; }
	public Guid AttemptId { get; set; }
}

public class HrReportingRunJob(IRepository<HrReportRun, Guid> runs, IRepository<HrReportRunPage, Guid> pages,
	HrReportingAccess access, HrReportingDataReader reader, IHrReportingRunLock runLock,
	HrReportExportScopeAuthorizationService scopeAuthorization, ICurrentTenant tenant, ICurrentPrincipalAccessor principal,
	IUnitOfWorkManager uows, IClock clock, IRepository<PayslipSettings, Guid> settings, ITenantRepository tenants,
	IAsyncQueryableExecuter executor, HrReportingRunCancellation cancellation, ILogger<HrReportingRunJob> logger)
	: AsyncBackgroundJob<HrReportingRunJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(HrReportingRunJobArgs args)
	{
		using var changeTenant = tenant.Change(args.TenantId);
		try
		{
			using (var claim = uows.Begin(requiresNew: true, isTransactional: true))
			{
				await runLock.LockAsync(args.RunId, args.TenantId);
				var run = await runs.FindAsync(args.RunId);
				if (run == null || !run.Start(args.AttemptId, clock.Now)) return;
				await runs.UpdateAsync(run, true); await claim.CompleteAsync();
			}
			using var read = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }, requiresNew: true);
			var current = await runs.GetAsync(args.RunId);
			if (current.Status != "Running" || current.AttemptId != args.AttemptId) return;
			using var actor = principal.Change(await access.PrincipalAsync(current.OwnerUserId));
			await access.EnsureAsync(current.DataSource, current.OwnerUserId);
			await scopeAuthorization.EnsureRecipientCoversSnapshotAsync(JsonSerializer.Deserialize<HrReportExportScopeSnapshot>(current.ScopeJson)!, current.OwnerUserId);
			var d = JsonSerializer.Deserialize<HrReportDesignDto>(current.DefinitionJson, HrReportingEngine.Json)!;
			var p = JsonSerializer.Deserialize<HrReportParametersDto>(current.ParametersJson, HrReportingEngine.Json)!;
			await access.EnsureDesignAsync(d, current.OwnerUserId);
			var capturedScope = JsonSerializer.Deserialize<HrReportExportScopeSnapshot>(current.ScopeJson)!;
			var generationScope = new UserScopeContext { UserId = current.OwnerUserId, TenantId = current.TenantId, IsAdmin = capturedScope.HasTenantWideAccess, HasWildcardAcademicYear = capturedScope.HasTenantWideAccess, MediumStageIds = [.. capturedScope.MediumStageIds] };
			var dataset = await reader.ReadAsync(d, p, generationScope, current.CapturedAt, p.TimeZone, () => cancellation.EnsureActiveAsync(current));
			await cancellation.EnsureActiveAsync(current);
			var result = HrReportingEngine.Execute(d, p, dataset.Rows);
			result.Warnings.AddRange(dataset.Warnings);
			var presentation = await executor.FirstOrDefaultAsync(await settings.GetQueryableAsync());
			result.SchoolName = presentation?.SchoolName ?? (current.TenantId.HasValue ? (await tenants.GetAsync(current.TenantId.Value)).Name : "School");
			result.SchoolAddress = presentation?.SchoolAddress ?? "";
			var count = result.Rows.Count;
			foreach (var (chunk, index) in result.Rows.Chunk(200).Select((chunk, index) => (chunk, index)))
			{
				if (index % 25 == 0) await cancellation.EnsureActiveAsync(current);
				await pages.InsertAsync(new HrReportRunPage(Guid.NewGuid(), current.TenantId, current.Id, index, JsonSerializer.Serialize(chunk, HrReportingEngine.Json), current.ExpiresAt));
			}
			result.Rows = [];
			current.Complete(args.AttemptId, count, JsonSerializer.Serialize(result, HrReportingEngine.Json));
			await runs.UpdateAsync(current, true); await read.CompleteAsync();
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "HR report run {RunId} failed", args.RunId);
			using var failed = uows.Begin(requiresNew: true, isTransactional: true);
			var run = await runs.FindAsync(args.RunId);
			if (run != null)
			{
				var message = exception is Volo.Abp.BusinessException business ? business.Code == HRErrorCodes.ReportPopulationTooLarge
					? "The report exceeds 500,000 source rows. Narrow the reporting period or staff selection."
					: business.Code == HRErrorCodes.ReportDesignInvalid ? business.Data["errors"]?.ToString() : null : null;
				run.Fail(args.AttemptId, message); await runs.UpdateAsync(run, true);
			}
			await failed.CompleteAsync();
		}
	}
}
