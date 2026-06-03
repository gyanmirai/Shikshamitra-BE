using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
public class HRDashboardPreviewService(
	IRepository<HrDashboardPreview, Guid> previewRepository,
	IBackgroundJobManager backgroundJobManager,
	IDataFilter dataFilter,
	HRDashboardKeyAccessService keyAccessService,
	HRDashboardPreviewPayloadBuilder payloadBuilder,
	HRDashboardPreviewProjectionService projectionService,
	HRDashboardScopeService scopeService)
	: ApplicationService
{
	private const int PayloadVersion = 1;
	private static readonly TimeSpan RefreshStaleAfter = TimeSpan.FromMinutes(15);

	public async Task<HRDashboardPreviewDto> GetPreviewAsync(string dashboardKey)
	{
		dashboardKey = await keyAccessService.NormalizeAndAuthorizeAsync(dashboardKey);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var scopeKey = HRDashboardScopeService.BuildScopeKey(scope);

		var preview = await FindPreviewAsync(dashboardKey, scopeKey);
		return preview == null
			? projectionService.Empty(dashboardKey, scopeKey, Clock.Now)
			: projectionService.ToDto(preview, Clock.Now);
	}

	public async Task<HRDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey)
	{
		dashboardKey = await keyAccessService.NormalizeAndAuthorizeAsync(dashboardKey);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var scopeKey = HRDashboardScopeService.BuildScopeKey(scope);

		var now = Clock.Now;
		var preview = await FindPreviewAsync(dashboardKey, scopeKey);
		if (preview == null)
		{
			preview = new HrDashboardPreview(GuidGenerator.Create(), CurrentTenant.Id, dashboardKey, scopeKey);
			await previewRepository.InsertAsync(preview, autoSave: true);
		}

		var isAlreadyRefreshing = preview.Status == HrDashboardPreviewStatus.Refreshing &&
			preview.RefreshStartedAt.HasValue &&
			now - preview.RefreshStartedAt.Value < RefreshStaleAfter;
		if (!isAlreadyRefreshing)
		{
			preview.MarkRefreshing(CurrentUser.Id, now);
			await previewRepository.UpdateAsync(preview, autoSave: true);
			await backgroundJobManager.EnqueueAsync(new HrDashboardPreviewRefreshJobArgs
			{
				TenantId = CurrentTenant.Id,
				PreviewId = preview.Id,
				DashboardKey = dashboardKey,
				ScopeKey = scopeKey,
				RequestedByUserId = CurrentUser.Id
			});
		}

		return projectionService.ToDto(preview, Clock.Now);
	}

	public async Task RefreshExistingPreviewsAsync()
	{
		await EnsureScheduledPreviewsAsync();

		var refreshStaleBefore = Clock.Now - RefreshStaleAfter;
		using (dataFilter.Disable<IMultiTenant>())
		{
			// Scheduled refresh intentionally crosses the tenant filter; keep the explicit tenant boundary here.
			var previews = await AsyncExecuter.ToListAsync((await previewRepository.GetQueryableAsync())
				.Where(x => x.TenantId == CurrentTenant.Id &&
					x.ScopeKey == HrDashboardScopeKeys.HrWide &&
					(x.Status != HrDashboardPreviewStatus.Refreshing ||
					!x.RefreshStartedAt.HasValue ||
					x.RefreshStartedAt.Value <= refreshStaleBefore)));

			foreach (var preview in previews)
			{
				preview.MarkRefreshing(null, Clock.Now);
				await previewRepository.UpdateAsync(preview, autoSave: true);
				await backgroundJobManager.EnqueueAsync(new HrDashboardPreviewRefreshJobArgs
				{
					TenantId = preview.TenantId,
					PreviewId = preview.Id,
					DashboardKey = preview.DashboardKey,
					ScopeKey = preview.ScopeKey
				});
			}
		}
	}

	public async Task ExecutePreviewRefreshAsync(HrDashboardPreviewRefreshJobArgs args)
	{
		var preview = await previewRepository.FindAsync(args.PreviewId);
		if (preview == null)
		{
			return;
		}

		try
		{
			var refreshScope = await scopeService.ResolveRefreshScopeAsync(args);
			if (!string.Equals(preview.ScopeKey, HRDashboardScopeService.BuildScopeKey(refreshScope), StringComparison.Ordinal))
			{
				throw new BusinessException(HRErrorCodes.DashboardScopeChanged);
			}

			var payloadJson = await payloadBuilder.BuildAsync(preview.DashboardKey, refreshScope);
			preview.MarkReady(payloadJson, PayloadVersion, Clock.Now);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"HR dashboard preview refresh failed. PreviewId={PreviewId}, DashboardKey={DashboardKey}, ScopeKey={ScopeKey}",
				preview.Id,
				preview.DashboardKey,
				preview.ScopeKey);
			preview.MarkFailed(HRDashboardRefreshErrorFormatter.Format(ex), Clock.Now);
		}

		await previewRepository.UpdateAsync(preview, autoSave: true);
	}

	private async Task EnsureScheduledPreviewsAsync()
	{
		foreach (var dashboardKey in HrDashboardKeys.All)
		{
			var preview = await FindPreviewAsync(dashboardKey, HrDashboardScopeKeys.HrWide);
			if (preview != null)
			{
				continue;
			}

			await previewRepository.InsertAsync(
				new HrDashboardPreview(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					dashboardKey,
					HrDashboardScopeKeys.HrWide),
				autoSave: true);
		}
	}

	private async Task<HrDashboardPreview?> FindPreviewAsync(string dashboardKey, string scopeKey)
	{
		return await AsyncExecuter.FirstOrDefaultAsync((await previewRepository.GetQueryableAsync())
			.Where(x =>
				x.DashboardKey == dashboardKey &&
				x.ScopeKey == scopeKey));
	}

}
