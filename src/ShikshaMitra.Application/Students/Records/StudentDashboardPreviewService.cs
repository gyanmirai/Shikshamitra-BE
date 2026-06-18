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

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentDashboardPreviewService(
	IRepository<StudentDashboardPreview, Guid> previewRepository,
	IBackgroundJobManager backgroundJobManager,
	IDataFilter dataFilter,
	StudentDashboardPayloadBuilder payloadBuilder,
	StudentDashboardPreviewProjectionService projectionService,
	StudentDashboardScopeService scopeService)
	: ApplicationService
{
	private const int PayloadVersion = 1;
	private static readonly TimeSpan RefreshStaleAfter = TimeSpan.FromMinutes(15);

	public async Task<StudentDashboardPreviewDto> GetPreviewAsync(string dashboardKey)
	{
		dashboardKey = NormalizeDashboardKey(dashboardKey);
		var scope = await scopeService.EnsureCanReadStudentDashboardAsync();
		var scopeKey = StudentDashboardScopeService.BuildScopeKey(scope);

		var preview = await FindPreviewAsync(dashboardKey, scopeKey);
		preview = await EnsureRefreshQueuedAsync(preview, dashboardKey, scopeKey);
		return projectionService.ToDto(preview, Clock.Now);
	}

	public async Task<StudentDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey)
	{
		dashboardKey = NormalizeDashboardKey(dashboardKey);
		var scope = await scopeService.EnsureCanReadStudentDashboardAsync();
		var scopeKey = StudentDashboardScopeService.BuildScopeKey(scope);

		var preview = await FindPreviewAsync(dashboardKey, scopeKey);
		if (preview == null)
		{
			preview = new StudentDashboardPreview(GuidGenerator.Create(), CurrentTenant.Id, dashboardKey, scopeKey);
			await previewRepository.InsertAsync(preview, autoSave: true);
		}

		await QueueRefreshAsync(preview, dashboardKey, scopeKey, CurrentUser.Id);

		return projectionService.ToDto(preview, Clock.Now);
	}

	public async Task RefreshExistingPreviewsAsync()
	{
		var now = Clock.Now;
		var scheduledRefreshAt = StudentDashboardPreviewSchedule.GetCurrentScheduledRefreshAt(now);
		var refreshStaleBefore = now - RefreshStaleAfter;

		using (dataFilter.Disable<IMultiTenant>())
		{
			var previews = await AsyncExecuter.ToListAsync((await previewRepository.GetQueryableAsync())
				.Where(x => x.TenantId == CurrentTenant.Id &&
					x.ScopeKey == StudentDashboardScopeKeys.StudentWide &&
					x.DashboardKey == StudentDashboardKeys.Reporting &&
					(!x.LastRefreshedAt.HasValue || x.LastRefreshedAt.Value < scheduledRefreshAt) &&
					(x.Status != StudentDashboardPreviewStatus.Refreshing ||
					!x.RefreshStartedAt.HasValue ||
					x.RefreshStartedAt.Value <= refreshStaleBefore)));

			foreach (var preview in previews)
			{
				preview.MarkRefreshing(null, now);
				await previewRepository.UpdateAsync(preview, autoSave: true);
				await backgroundJobManager.EnqueueAsync(new StudentDashboardPreviewRefreshJobArgs
				{
					TenantId = preview.TenantId,
					PreviewId = preview.Id,
					DashboardKey = preview.DashboardKey,
					ScopeKey = preview.ScopeKey
				});
			}
		}
	}

	public async Task ExecutePreviewRefreshAsync(StudentDashboardPreviewRefreshJobArgs args)
	{
		var preview = await previewRepository.FindAsync(args.PreviewId);
		if (preview == null)
		{
			return;
		}

		try
		{
			var refreshScope = await scopeService.ResolveRefreshScopeAsync(args);
			if (!string.Equals(preview.ScopeKey, StudentDashboardScopeService.BuildScopeKey(refreshScope), StringComparison.Ordinal))
			{
				throw new BusinessException(StudentDashboardErrorCodes.ScopeChanged);
			}

			var payloadJson = await payloadBuilder.BuildAsync(preview.DashboardKey, refreshScope);
			preview.MarkReady(payloadJson, PayloadVersion, Clock.Now);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"Student dashboard preview refresh failed. PreviewId={PreviewId}, DashboardKey={DashboardKey}, ScopeKey={ScopeKey}",
				preview.Id,
				preview.DashboardKey,
				preview.ScopeKey);
			preview.MarkFailed(StudentDashboardRefreshErrorFormatter.Format(ex));
		}

		await previewRepository.UpdateAsync(preview, autoSave: true);
	}

	private async Task<StudentDashboardPreview?> FindPreviewAsync(string dashboardKey, string scopeKey)
	{
		return await AsyncExecuter.FirstOrDefaultAsync((await previewRepository.GetQueryableAsync())
			.Where(x =>
				x.DashboardKey == dashboardKey &&
				x.ScopeKey == scopeKey));
	}

	private async Task<StudentDashboardPreview> EnsureRefreshQueuedAsync(
		StudentDashboardPreview? preview,
		string dashboardKey,
		string scopeKey)
	{
		if (preview == null)
		{
			preview = new StudentDashboardPreview(GuidGenerator.Create(), CurrentTenant.Id, dashboardKey, scopeKey);
			await previewRepository.InsertAsync(preview, autoSave: true);
		}

		var dto = projectionService.ToDto(preview, Clock.Now);
		if (dto.Status is StudentDashboardPreviewStatus.Empty or StudentDashboardPreviewStatus.Failed || dto.IsStale)
		{
			await QueueRefreshAsync(preview, dashboardKey, scopeKey, CurrentUser.Id);
		}

		return preview;
	}

	private async Task QueueRefreshAsync(
		StudentDashboardPreview preview,
		string dashboardKey,
		string scopeKey,
		Guid? requestedByUserId)
	{
		var now = Clock.Now;
		var isAlreadyRefreshing = preview.Status == StudentDashboardPreviewStatus.Refreshing &&
			preview.RefreshStartedAt.HasValue &&
			now - preview.RefreshStartedAt.Value < RefreshStaleAfter;
		if (isAlreadyRefreshing)
		{
			return;
		}

		preview.MarkRefreshing(requestedByUserId, now);
		await previewRepository.UpdateAsync(preview, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new StudentDashboardPreviewRefreshJobArgs
		{
			TenantId = CurrentTenant.Id,
			PreviewId = preview.Id,
			DashboardKey = dashboardKey,
			ScopeKey = scopeKey,
			RequestedByUserId = requestedByUserId
		});
	}

	private static string NormalizeDashboardKey(string dashboardKey)
	{
		if (string.Equals(dashboardKey, StudentDashboardKeys.Reporting, StringComparison.OrdinalIgnoreCase))
		{
			return StudentDashboardKeys.Reporting;
		}

		throw new BusinessException(StudentDashboardErrorCodes.InvalidKey)
			.WithData("DashboardKey", dashboardKey);
	}
}
